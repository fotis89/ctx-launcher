# wl v1: simplify around real usage

## Summary

Cut wl down to what is actually used day to day, and add the one missing flow:
running `wl` in any folder with shared skills and instructions, without creating
a workspace. Four entry points replace fourteen command variants. Workspaces stay
plain folders that users edit with VS Code or Copilot, not through CLI commands.
This is a breaking release; recommend v1.0.0.

## Evidence

Author usage (the primary user; at most one other known user):

- Used: `launch <name>`, `--resume`/`--new`, `create` (Copilot proposes), `clone`,
  `instructions.md` → `AGENTS.md`, workspace skills, shared skills, `additionalDirs`.
- Unused: `launch` with no name, `--yolo`, `-p` (saved or literal), `create --basic`,
  `list`, `which`, `edit`, `paths set/list/init`, `setup`.
- Pain: wanting shared skills in an arbitrary folder requires creating a workspace first.

## Decisions

- **Folder mode is the default.** `wl` with no name launches Copilot in the
  current folder with shared skills and shared instructions attached.
- **Launching is the default verb.** `wl <name>` launches a workspace; the
  `launch` subcommand is removed.
- **Resume is the default.** If a saved session exists it is resumed; `--new`
  starts fresh and makes that the remembered session. `--resume` and the
  `resume` field are removed.
- **Throwaway sessions.** `--temp` starts a fresh session and never touches the
  saved pointer, so the next plain launch resumes the remembered session as if
  the throwaway never happened. Useful for quick questions or experiments in a
  workspace's context. `--temp` and `--new` are mutually exclusive.
- **Pass-through replaces wl-specific flags.** Everything after `--` goes to
  Copilot unchanged (for example `wl api -- --yolo -p "fix the build"`). This
  replaces `--yolo`, `-p`, and saved prompts.
- **Workspaces are managed as files, not through CLI commands.** Users edit
  `~/.wl-workspaces/<name>` in VS Code or ask Copilot (via the bundled skill) to
  change it. Deleting a workspace means deleting its folder. `list`, `which`, and
  `edit` are removed.
- **Setup and path variables are automatic.** Bundled skills refresh
  automatically when the wl version changes. Undefined `$VARS` are prompted for
  at launch or clone time and saved to `.paths.json`; to change one, edit that
  file.
- **No manual migration.** Keep `schemaVersion: 2`. Obsolete fields (`yolo`,
  `resume`) and a `prompts/` folder are ignored, with a one-line notice naming the
  file to clean up. They are never rejected. The breaking change is the command
  surface only.

## Command surface

| Command | Behavior |
| --- | --- |
| `wl [--new \| --temp] [-- <copilot args>]` | Folder mode in the current directory (see below). |
| `wl <name> [--new \| --temp] [-- <copilot args>]` | Launch workspace `<name>`: resume its last session by default, start and remember a fresh one with `--new`, or start a throwaway one with `--temp`. |
| `wl create [name]` | Copilot proposes a workspace via the bundled skill, defaulting `primaryRepo` to the current folder. Prints the created folder path. |
| `wl clone <git-url>` | Clone workspace definitions, install bundled skills, prompt for every referenced undefined variable. |
| `wl --version`, `wl --help` | Unchanged. |

`create` and `clone` become reserved workspace names; `create` rejects them.
An unknown name prints the available workspaces (this replaces `list`) and the
path of the workspaces root. Tab completion moves to the root name argument.
The shell registration snippets `setup` used to print move to the README
(PowerShell and bash).
Removed commands (`launch`, `list`, `which`, `edit`, `paths`, `setup`) print a
one-line pointer to the new form and exit nonzero.

## Folder mode

- The primary directory is the current working directory. No `workspace.json`
  is read or written.
- Attach the shared plugin dir (`.shared/.copilot`) exactly as workspace
  launches do today.
- **Shared instructions (new):** `.shared/instructions.md` is mirrored to
  `.shared/AGENTS.md` and `.shared` is appended to `COPILOT_CUSTOM_INSTRUCTIONS_DIRS`,
  using the same mirroring and deduplication as workspace instructions. Shared
  instructions also apply to workspace launches, before workspace instructions.
- **Session memory per folder:** store folder sessions in a machine-local
  `<root>/.folder-sessions.json` map keyed by the normalized full path
  (case-insensitive on Windows). Same UUID and atomic-write rules as `.last-session`.
  Add the file to the setup `.gitignore` patterns.
- **Workspace auto-detection:** if the current folder equals exactly one
  workspace's resolved `primaryRepo`, launch that workspace instead and print
  `Using workspace '<name>' (primaryRepo match)`. With several matches, use
  folder mode and list the matching names. Use `wl <name>` to be explicit.
- The repository's own instructions and skills keep working unchanged;
  folder mode only adds the personal shared layer.

## Removed

- Commands: `launch`, `list`, `which`, `edit`, `paths` (all subcommands), `setup`.
- Options: `--resume`, `--yolo`, `-p`/`--prompt`, `create --basic`.
- Launching with no name meaning "the last workspace" (replaced by folder mode).
  Remove the `.last` pointer.
- Workspace fields `yolo` and `resume` (ignored with a notice). Saved prompts
  and `PromptService`.
- Separate `wl-create-workspace` and `wl-update-workspace` skills: merge them
  into one `wl-workspace` skill covering create and update. Automatic setup
  removes only the two old bundled skill folders it owns.

## Kept unchanged

`workspace.json` (`name`, `primaryRepo`, `additionalDirs`), `instructions.md` →
`AGENTS.md` mirroring, workspace and shared skills via explicit `--plugin-dir`
with generated `plugin.json`, UUID sessions in `.last-session`, `$VAR`/`~/`
path resolution, `.paths.json`, `WL_WORKSPACES_ROOT` (kept for tests but not
documented in the README), npm distribution and native binaries.

## Errors

- Undefined variable in a non-interactive shell: error naming the variable and
  `.paths.json`, and exit nonzero. Never guess a path.
- `--new` with a saved-session pointer that fails to parse: start fresh. Without
  `--new`: error with the file path (as today). A missing pointer silently
  starts fresh.
- A failed Copilot process never overwrites the saved session (as today).

## Session modes

| Mode | Copilot session | Saved pointer after a successful exit |
| --- | --- | --- |
| default | resume the pointer's session, or start a fresh one if there is no pointer | unchanged when resuming; set when a fresh session started |
| `--new` | fresh, `--name=<folder>-<hex>` | replaced with the new session UUID |
| `--temp` | fresh, `--name=<folder>-temp-<hex>` | never read, never written |

- Throwaway sessions get an explicit UUID and a `-temp-` name, so they are easy
  to recognize and resume manually from Copilot's own session list if needed.
- `--temp` works in folder mode as well: the per-folder entry in
  `.folder-sessions.json` is left untouched.
- `--temp` skips reading the pointer entirely, so a malformed pointer does not
  block a throwaway launch.

## Affected surfaces

- `Program.cs`: new root command with an optional name argument, `--new`/`--temp`
  (mutually exclusive), and `--` pass-through; keep `create` and `clone`; add
  stubs for removed commands.
- Delete `ListCommand`, `WhichCommand`, `EditCommand`, `PathsCommand`,
  `SetupCommand`, `PromptService`, `SavedPrompt`. Fold what's left of setup into
  a version-triggered step in `SetupService`.
- `LaunchService`/`CopilotService`: accept a folder-mode spec (no `Workspace`);
  add shared-instructions mirroring; append pass-through args last.
- `WlPaths`: add `SharedInstructions`, `SharedAgents`, `FolderSessionsFile`; drop
  `PromptsDirName` and `LastWorkspaceFile`.
- `PathsService`: add an interactive "prompt and save undefined" path used by
  launch and clone.
- Resources: replace the two skill files with `wl-workspace.md`.
- README: target about a third of the current length. Sections: Install (including
  tab-completion snippets), Quick start (`wl`, `wl create`, `wl <name>`),
  Workspaces are folders, Sync across PCs, Build.

## Tests

- Unit: arguments in folder mode, shared-instructions mirroring and env
  ordering, per-folder session map (normalization, atomic write), auto-detection
  (0, 1, or several matches), pass-through ordering, notices for ignored fields,
  and pointer messages for removed commands.
- E2E (shim Copilot): `wl` in a temp folder resumes on the second run, `--new`
  forces a fresh session, `wl <name>` resumes, `--temp` launches a fresh
  `-temp-` session and the next plain launch still resumes the original,
  `--temp --new` is rejected, `-- --yolo` reaches the shim,
  an undefined variable prompts once and persists, `clone` prompts for variables.

## Trade-offs

Removing `list`/`which`/`edit` loses CLI discoverability for new users.
Unknown-name output and tab completion cover most of it. Pass-through makes
wl-specific flags unnecessary, but users must know Copilot's own flags.
Auto-detecting workspaces by `primaryRepo` removes a decision for the common
case; the printed notice keeps it transparent.

## Open questions

1. Keep `wl launch <name>` as a hidden alias for one minor release, to avoid
   breaking muscle memory and scripts?
2. Should the per-folder session also be offered for folders inside a
   workspace's `primaryRepo` (subfolders), or only exact matches?
