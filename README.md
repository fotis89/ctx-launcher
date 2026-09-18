# ctx-launcher (wl) - Named Copilot workspaces you can relaunch

[![npm](https://img.shields.io/npm/v/@ctx-launcher/wl)](https://npmjs.com/package/@ctx-launcher/wl)
[![CI](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml/badge.svg)](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/fotis89/ctx-launcher)](LICENSE)

`wl` saves your project setup (repos, folders, instructions, skills) under a name,
then opens it in [GitHub Copilot CLI](https://docs.github.com/copilot/how-tos/copilot-cli).
Switch between projects without rebuilding context, and resume your previous session.

Your repository's shared instructions stay the team's. `wl` keeps your personal
context outside the repository in `~/.wl-workspaces/<name>`.

**Copilot-only breaking change:** v0.9.0 requires workspace schema 2.
Existing users must follow the [manual upgrade guide](#upgrading-existing-workspaces).
The older 0.8.x releases support both runtimes; v0.9.0 supports only Copilot.

## Install

Requires Node.js and GitHub Copilot CLI 1.0.86 or newer on your `PATH`:

```powershell
npm install -g @ctx-launcher/wl
copilot --version
```

Prebuilt binaries are available for Windows x64, Linux x64, and macOS arm64.
npm downloads only your platform's binary. Other platforms can
[build from source](#build-from-source).

## Quick start

```powershell
cd D:\repos\my-project
wl create my-project
wl launch my-project
wl launch my-project --resume
```

`wl create` asks Copilot to propose a workspace and waits for your approval.
For a minimal configuration without invoking Copilot:

```powershell
wl create my-project --basic
```

The first create or launch installs the bundled workspace skills. `wl setup`
refreshes them explicitly, checks Copilot availability, and prints tab-completion
instructions. There is no tool selection or fallback runtime.

## Commands

| Command | Purpose |
| --- | --- |
| `wl create [name]` | Ask Copilot to propose and create a workspace |
| `wl create <name> --basic` | Write a minimal schema-2 workspace without invoking Copilot |
| `wl launch [name]` | Launch a workspace; omit the name to use the last successfully launched workspace |
| `wl launch <name> --resume` | Resume the saved Copilot session; fail if none exists |
| `wl launch <name> --new` | Start fresh, overriding the workspace's resume default |
| `wl launch <name> --yolo` | Skip Copilot permission prompts |
| `wl launch <name> -p <name-or-text>` | Use a saved prompt or literal prompt text |
| `wl list` | List workspaces, including incompatible ones with diagnostics |
| `wl which <name>` | Preview resolved paths, preparation, environment, and launch command without writing files |
| `wl edit <name>` | Open the workspace folder, including legacy workspaces needing repair |
| `wl paths set <name> <value>` | Define a machine-local path variable |
| `wl paths list` | Show defined and referenced variables |
| `wl paths init` | Prompt for undefined variables |
| `wl clone <git-url>` | Clone workspace definitions, run setup, then initialize path variables |
| `wl setup` | Install bundled Copilot skills and show completion setup |

`--new` and `--resume` cannot be combined. Invalid configuration and failed
Copilot processes produce nonzero exit codes. A failed Copilot process does not
replace the saved session or last-workspace pointer.

## Workspace layout

```text
~/.wl-workspaces/
|-- .paths.json                  (machine-local variables)
|-- .shared/
|   `-- .copilot/
|       |-- plugin.json          (generated)
|       `-- skills/
`-- my-project/
    |-- workspace.json
    |-- instructions.md         (optional, editable source)
    |-- AGENTS.md                (generated from instructions.md)
    |-- .last-session            (machine-local Copilot session reference)
    |-- prompts/
    |   `-- review.md
    `-- .copilot/
        |-- plugin.json          (generated)
        `-- skills/
            `-- wl-review/
                `-- SKILL.md
```

Set `WL_WORKSPACES_ROOT` to use another workspace root. All workspace storage,
shared skills, variables, and session pointers use that root. This is also how
the E2E suite isolates its files from your real profile.

### workspace.json

```json
{
  "schemaVersion": 2,
  "name": "My project",
  "primaryRepo": "$REPOS_ROOT/my-project",
  "additionalDirs": ["~/notes"],
  "yolo": false,
  "resume": true
}
```

`schemaVersion` must explicitly be `2`. `name` and `primaryRepo` must be non-empty.
`additionalDirs` defaults to an empty array; its entries must be non-empty paths.
The primary repository must be a directory. Missing additional directories are
reported and skipped. `yolo` and `resume` default to false.

The `tool` field and `--tool` option no longer exist, including `tool: copilot`.
Machine-local `defaultTool` settings are rejected rather than silently ignored.

### Instructions and skills

Edit `instructions.md`, not the generated `AGENTS.md`. Each launch mirrors the
instructions and appends the workspace folder to `COPILOT_CUSTOM_INSTRUCTIONS_DIRS`,
preserving inherited instruction directories and removing duplicate entries.
Repository instructions remain separate from your personal workspace context.

Workspace and shared `.copilot` folders with skills are exposed using explicit
`--plugin-dir` arguments and generated `plugin.json` manifests. No global Copilot
settings are modified. Generated workspace plugin names include a stable hash
and stay within Copilot's 64-character limit.
Skills require `name` and `description` frontmatter; use a `wl-` prefix for workspace skills.
`allowed-tools` is optional permission pre-approval, not required metadata.
The bundled skills do not pre-approve tools. Only add narrow approvals after
reviewing and trusting a skill and its scripts.

The bundled **wl-create-workspace** and **wl-update-workspace** skills propose
changes before writing files. Ask Copilot to use them by name or describe the
task, or use a prompt such as `Use the /wl-update-workspace skill`.

### Saved prompts

Save a prompt under `prompts/review.md`:

```markdown
---
label: Review changes
---
Review the changes and identify anything needing manual verification.
```

Use `wl launch my-project -p review`, or pass literal text with
`wl launch my-project -p "investigate the failing test"`.

### Sessions

Fresh sessions receive an explicit UUID via `--session-id` and a readable name.
After a successful exit, `.last-session` stores the UUID as plain text; resume
uses that ID so renaming the session in Copilot does not break it. Existing
plain-text session names still work, but remain sensitive to renaming until
you start a fresh session or manually replace the name with its Copilot UUID.
Session history is local to Copilot on that machine; synchronizing workspace
definitions does not synchronize conversations. Closing the terminal before
Copilot exits successfully can leave the previous pointer unchanged.

## Upgrading existing workspaces

There is **no automatic migration**. Back up your workspace root before editing.

1. In each `workspace.json`, set `"schemaVersion": 2` and remove `tool`.
2. Remove `defaultTool` from the root `.config.json`, or delete that file if it
   contains nothing else. `wl` no longer reads runtime defaults.
3. Move workspace and shared skills from `.claude/skills` to `.copilot/skills`.
   Remove the now-empty legacy `skills` directory. Do not overwrite colliding
   skill names without reviewing them. wl rejects remaining legacy skill folders.
4. For `.last-session`, copy only the old JSON map's `copilot` value into the file
   as plain text, or delete the pointer to start fresh. Claude conversations
   cannot be resumed in Copilot. Do not copy the old `claude` entry or an
   unidentified legacy UUID.
5. Run `wl setup`, then `wl which <name>` to confirm the configuration.

Old generated `.claude/plugin.json` files can be removed manually. Update your
workspace repository's ignore rules for the new generated manifest paths;
`wl setup` appends the required patterns without deleting old user content.
If you previously registered skill directories in global Copilot settings,
review/remove those legacy registrations yourself; wl does not edit them.

Users still on the old unscoped npm package should uninstall `ctx-launcher` and
install `@ctx-launcher/wl`. The workspace root stays the same.

## Syncing across PCs

Use `$VAR` or `${VAR}` references for machine-specific roots, and `~/` for
home-relative paths. Define variables per machine:

```powershell
wl paths set REPOS_ROOT D:\repos
wl paths set DOCS_ROOT ~\Documents
wl paths list
```

Values live in `.paths.json`. Keep workspace definitions and user-authored skills
in a private git repository, then use `wl clone <git-url>` on another machine.
Legacy clones must be upgraded manually before setup/launch will work.

Setup ignores machine-local `.last-session`, `.last`, `.version`, `.paths.json`,
and legacy `.config.json`, plus generated `*/AGENTS.md`,
`*/.copilot/plugin.json`, `.shared/.copilot/plugin.json`, and the two bundled
skill directories under `.shared/.copilot/skills`. Other shared skills stay tracked.

## Build from source

Requires .NET 10 SDK:

```powershell
dotnet build wl.slnx --verbosity quiet
dotnet test tests\wl.tests\wl.tests.csproj --verbosity quiet
```

Native AOT publishing also requires MSVC C++ build tools on Windows, or the native
toolchain on Linux/macOS:

```powershell
dotnet publish src\wl\wl.csproj -c Release -r win-x64
$env:WL_BINARY_PATH = (Resolve-Path src\wl\bin\Release\net10.0\win-x64\publish\wl.exe).Path
dotnet test tests\wl.e2e.tests\wl.e2e.tests.csproj --verbosity quiet
```

Use `linux-x64` or `osx-arm64` and native path separators on the other supported
platforms. CI runs unit tests and E2E tests against the native binary on all three.
No real Copilot account is needed for the automated shim-based E2E suite.

## Contributing and license

Issues and PRs welcome at [fotis89/ctx-launcher](https://github.com/fotis89/ctx-launcher).
Licensed under [MIT](LICENSE).
