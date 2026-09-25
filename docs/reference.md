# wl reference

How wl works: every file it reads or writes, what one launch does, and where
to look when something is off. For install and a quick start, see the
[README](../README.md).

**Fastest way to see the current state:** `wl which <name>` (or `wl which` in a
folder). It prints the resolved paths, skills, instruction folders, and the exact
Copilot command, and it never changes anything.

## Commands

| Command | Does |
| --- | --- |
| `wl` | Print help. |
| `wl launch [--new \| --temp] [-- <copilot args>]` | **Folder mode**: Copilot in the current folder with the shared layer only. |
| `wl launch <name> [--new \| --temp] [-- <copilot args>]` | Launch the workspace `<name>`. |
| `wl which [name] [--new \| --temp] [-- <copilot args>]` | Show what that `launch` would run. Read-only. |
| `wl create [name]` | Run Copilot with the bundled `wl-workspace` skill to propose and create a workspace. |
| `wl clone <git-url>` | Clone a workspaces repo into the (empty) workspaces root, install the bundled skill, and prompt for undefined path variables. |
| `wl --version` | Version plus the commit it was built from. |

- `--new`: start a fresh session and remember it. `--temp`: start a throwaway
  session and leave the remembered one alone. They can't be combined.
- Everything after `--` goes to Copilot unchanged.
- An unknown workspace name prints the workspaces root and the available names.

## The files

### Workspaces root

`~/.wl-workspaces`, or `$WL_WORKSPACES_ROOT` if set. It is usually a private git
repo so it syncs across PCs.

```text
~/.wl-workspaces/
├── .gitignore                 synced   written/merged by wl (see "Git")
├── .paths.json                local    $VAR values for this PC
├── .folder-sessions.json      local    folder mode: folder path → Copilot session
├── .version                   local    wl version that last installed the bundled skill
├── AGENTS.md                  synced   optional; only for sessions *started in* this folder
├── .shared/                            the shared layer, used by every launch
│   ├── AGENTS.md              synced   optional shared instructions
│   └── .copilot/                       exposed to Copilot as a plugin
│       ├── plugin.json        local    generated (name: wl-shared)
│       └── skills/
│           ├── <skill>/SKILL.md          synced   your shared skills
│           └── wl-workspace/SKILL.md     local    bundled by wl, refreshed on upgrade
└── <name>/                             one folder per workspace; the folder name is the slug
    ├── workspace.json         synced   required
    ├── AGENTS.md              synced   optional workspace instructions
    ├── .last-session          local    remembered Copilot session ID
    └── .copilot/                       exposed to Copilot as a plugin
        ├── plugin.json        local    generated (name: wl-<slug>-<hash>)
        └── skills/<wl-skill>/SKILL.md  synced   workspace skills
```

"Local" files are machine-specific or generated and are git-ignored; "synced"
files are the ones you edit and commit.

### `workspace.json`

```json
{
  "schemaVersion": 2,
  "name": "My project",
  "primaryRepo": "$REPOS/my-project",
  "additionalDirs": ["~/notes", "../shared-specs"],
  "copilotArgs": ["--yolo"]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `schemaVersion` | yes | Must be `2`. |
| `name` | yes | Display name. The folder name, not this, is what you type in `wl launch`. |
| `primaryRepo` | yes | Copilot's working directory. Must exist. |
| `additionalDirs` | no | Extra folders passed as `--add-dir`. Missing ones are skipped with a warning. |
| `copilotArgs` | no | Copilot arguments added to every launch, e.g. `["--yolo"]` or `["--allow-tool=shell(git:*)"]`. |

Any other field is ignored.

**Paths** (`primaryRepo`, `additionalDirs`) are resolved in this order:

1. `$VAR` / `${VAR}` → value from `.paths.json`.
2. A leading `~` → your home folder.
3. Still relative → relative to the **workspace folder** (`~/.wl-workspaces/<name>`).
4. On Linux/macOS, `\` becomes `/`, so one file works on every OS.

Conventions: `~/` for anything under home, `$REPOS/...` (or another existing
variable) for checkouts elsewhere, never a hard-coded drive letter.

### `.paths.json`

A flat map of variable names to values for this PC, e.g.
`{ "REPOS": "D:\\repos", "ONEDRIVE": "C:\\Users\\me\\OneDrive" }`. Names match
`^[A-Za-z_][A-Za-z0-9_]*$`.

- `wl launch` and `wl clone` ask for any undefined variable once and save it.
  With no interactive terminal they fail instead, naming the variable.
- `wl which` never asks; it reports unset variables.
- To change a value, edit the file. If the file is broken, wl warns and treats it
  as empty for reading, but refuses to save until you fix or delete it, so your
  other values are never overwritten.

## Instructions: which `AGENTS.md` Copilot reads

wl sets `COPILOT_CUSTOM_INSTRUCTIONS_DIRS` for the Copilot process to:

1. whatever was already in that variable, then
2. `.shared` — only if `.shared/AGENTS.md` exists, then
3. the workspace folder (in folder mode: the current folder).

Copilot loads `AGENTS.md` (and `*.instructions.md`) from those folders, **plus**
the primary repo's own instruction files, which it finds by itself
(`AGENTS.md`, `.github/copilot-instructions.md`, `.github/instructions/`).
Write only workspace-specific context in the workspace `AGENTS.md`; don't repeat
what the repo already says. wl never writes any `AGENTS.md`.

The root `~/.wl-workspaces/AGENTS.md` is *not* part of this. Copilot only reads it
when a session starts in `~/.wl-workspaces` itself (for example the workspace
whose `primaryRepo` is that folder).

## Skills and plugins

Each `.copilot` folder that contains at least one `skills/<name>/SKILL.md` is
passed to Copilot as `--plugin-dir`: first the workspace's, then `.shared`'s.
Before each launch wl writes the matching `plugin.json` (a manifest with just a
name). Folders without skills are not passed. wl never changes Copilot's global
settings or installs plugins globally.

A skill is a folder with a `SKILL.md`:

```markdown
---
name: wl-review
description: What it does and when to use it.
---

Steps, commands, and paths.
```

`name` and `description` are required; use a `wl-` prefix for workspace skills.
`allowed-tools: shell` is optional and pre-approves shell commands for that skill.
Invoke a skill by name (`/wl-review`) or let Copilot pick it from the description.

The bundled `wl-workspace` skill (create and update workspaces) lives in
`.shared/.copilot/skills/wl-workspace/`. wl rewrites it on the first launch,
create, or clone after the wl version changes; don't edit it there.

## Sessions

| Launch | Copilot gets | Remembered session after Copilot exits with 0 |
| --- | --- | --- |
| plain, remembered session exists | `--resume=<id>` | unchanged |
| plain, nothing remembered | `--name=<slug>-<8 hex> --session-id=<uuid>` | set to the new ID |
| `--new` | `--name=<slug>-<8 hex> --session-id=<uuid>` | replaced with the new ID |
| `--temp` | `--name=<slug>-temp-<8 hex> --session-id=<uuid>` | never read or written |

- Workspaces remember their session in `<name>/.last-session` (plain text).
  Folder mode uses `.folder-sessions.json`, keyed by the folder's full path
  (case-insensitive on Windows). Entries are never pruned; delete the file to reset.
- Folder mode never switches to a workspace, even inside a workspace's repo.
  Use `wl launch <name>` for that.
- If Copilot exits with an error, nothing is saved.
- Sessions live in Copilot on that PC. Syncing the workspaces repo doesn't sync
  conversations.

## What one launch does

For `wl launch <name>`:

1. Load `<name>/workspace.json` (error if missing or `schemaVersion` isn't 2).
2. Ask for undefined path variables; resolve all paths; fail if `primaryRepo`
   doesn't exist.
3. Pick the session (table above).
4. Install or refresh the bundled skill if the wl version changed (this also merges
   `.gitignore`).
5. Write `plugin.json` for each `.copilot` folder with skills.
6. Run Copilot in `primaryRepo` with, in order:
   - `--resume=...`, or `--name=... --session-id=...`
   - `--add-dir` for each existing additional dir, then `.shared`, then the workspace folder
   - `--plugin-dir` for the workspace `.copilot`, then `.shared/.copilot` (if they have skills)
   - `copilotArgs`
   - everything after `--`
7. If Copilot exits with 0 and a new session was started (not `--temp`), save it.

Folder mode does the same without a `workspace.json`: Copilot runs in the current
folder with only `.shared` added (`--add-dir`, `--plugin-dir`, and its `AGENTS.md`).

On Windows, if `copilot` resolves to the npm wrapper `copilot.cmd`, wl runs the
underlying `node` script directly so prompt text isn't mangled by `cmd.exe`.

## Git

On upgrade wl merges these patterns into the workspaces root `.gitignore`,
adding missing lines without removing yours:

```gitignore
.last-session
.folder-sessions.json
.version
.paths.json
.shared/.copilot/skills/wl-workspace/
*/.copilot/plugin.json
.shared/.copilot/plugin.json
```

Commit everything else: `workspace.json`, `AGENTS.md` files, and your skills.

## Troubleshooting

Start with `wl which <name>`.

| Symptom | Check |
| --- | --- |
| `Workspace '<x>' not found` | The name is the folder name under the workspaces root (printed with the error), not `name` in `workspace.json`. |
| `primary repo not found` | `wl which` shows the resolved path. A `$VAR` may be unset (listed as `unset`) or point somewhere else on this PC; fix it in `.paths.json`. |
| An additional dir is skipped | It doesn't exist on this PC; same check as above. |
| `requires "schemaVersion": 2` | Add `"schemaVersion": 2` to that `workspace.json`. |
| Instructions not applied | Is the file named `AGENTS.md` and in the workspace folder (or `.shared`)? `wl which` lists `COPILOT_CUSTOM_INSTRUCTIONS_DIRS`; inside Copilot, `/instructions` shows what loaded. |
| Skills missing | The skill must be `.copilot/skills/<name>/SKILL.md` with `name` and `description`. `wl which` lists the `--plugin-dir` folders; in Copilot, `/skills` or `/env`. |
| Resume fails ("session not found") | The remembered session was deleted in Copilot or created on another PC. Run `wl launch <name> --new`. |
| `.last-session` error | The file has unexpected content. Delete it to start fresh. |
| `'copilot' not found` | Open a new terminal and run `copilot --version`; install GitHub Copilot CLI or fix `PATH`. |
| Refuses to save a path variable | `.paths.json` is not valid JSON. Fix or delete it. |
| Undefined variable in a script or CI | Non-interactive runs can't prompt; add the variable to `.paths.json` first. |
