# ctx-launcher (wl) - Named Copilot workspaces you can relaunch

[![npm](https://img.shields.io/npm/v/@ctx-launcher/wl)](https://npmjs.com/package/@ctx-launcher/wl)
[![CI](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml/badge.svg)](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/fotis89/ctx-launcher)](LICENSE)

`wl` saves your project setup (repos, folders, instructions, skills) under a name,
then opens it in [GitHub Copilot CLI](https://docs.github.com/copilot/how-tos/copilot-cli).
Switch between projects without rebuilding context, and resume your previous session.

Your repository's shared instructions stay the team's. `wl` keeps your personal
context outside the repository in `~/.wl-workspaces/<name>`.

## Install

Requires Node.js and GitHub Copilot CLI 1.0.86 or newer on your `PATH`:

```powershell
npm install -g @ctx-launcher/wl
copilot --version
```

Prebuilt binaries are available for Windows x64, Linux x64, and macOS arm64.
npm downloads only your platform's binary. Other platforms can
[build from source](#build-from-source). On Windows without npm access, use the
[local install script](#local-install-from-source-windows).

## Quick start

```powershell
cd D:\repos\my-project
wl create my-project
wl launch my-project
wl launch my-project --new
wl launch my-project --temp
```

`wl create` asks Copilot to propose a workspace and waits for your approval.
For a minimal configuration without invoking Copilot:

```powershell
wl create my-project --basic
```

The first create or launch installs the bundled workspace skills. `wl setup`
refreshes them explicitly, checks Copilot availability, and prints tab-completion
instructions.

## Commands

| Command | Purpose |
| --- | --- |
| `wl create [name]` | Ask Copilot to propose and create a workspace |
| `wl create <name> --basic` | Write a minimal schema-2 workspace without invoking Copilot |
| `wl launch` | Launch Copilot in the current folder with shared context |
| `wl launch <name>` | Launch a workspace |
| `wl launch <name> --new` | Start a fresh session instead of resuming the saved one |
| `wl launch <name> --temp` | Start a throwaway session without changing the saved one |
| `wl launch <name> -- <args>` | Pass remaining arguments directly to Copilot |
| `wl which <name>` | Preview resolved paths, preparation, environment, and launch command without writing files |
| `wl paths set <name> <value>` | Define a machine-local path variable |
| `wl paths list` | Show defined and referenced variables |
| `wl paths init` | Prompt for undefined variables |
| `wl clone <git-url>` | Clone workspace definitions, run setup, then initialize path variables |
| `wl setup` | Install bundled Copilot skills and show completion setup |

Invalid configuration and failed Copilot processes produce nonzero exit codes. A failed Copilot process does not
replace the saved session or last-workspace pointer.

## Workspace layout

```text
~/.wl-workspaces/
|-- .paths.json                  (machine-local variables)
|-- .shared/
|   |-- AGENTS.md            (optional, instructions for every workspace)
|   `-- .copilot/
|       |-- plugin.json          (generated)
|       `-- skills/
`-- my-project/
    |-- workspace.json
    |-- AGENTS.md                (workspace instructions)
    |-- .last-session            (machine-local Copilot session reference)
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
  "copilotArgs": ["--yolo"]
}
```

`schemaVersion` must explicitly be `2`. `name` and `primaryRepo` must be non-empty.
`additionalDirs` defaults to an empty array; its entries must be non-empty paths.
The primary repository must be a directory. Missing additional directories are
reported and skipped. `copilotArgs` defaults to an empty array and is appended to every Copilot launch.

### Instructions and skills

Edit `AGENTS.md` in the workspace folder for workspace instructions, and
`.shared/AGENTS.md` for instructions that apply to every workspace. Each launch
appends `.shared` (when `.shared/AGENTS.md` exists) and then the workspace folder
to `COPILOT_CUSTOM_INSTRUCTIONS_DIRS`, preserving inherited instruction directories
and removing duplicate entries.
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

Pass a one-off Copilot prompt after `--`, for example
`wl launch my-project -- -i "investigate the failing test"`.

### Sessions

Fresh sessions receive an explicit UUID via `--session-id` and a readable name.
After a successful workspace launch, `.last-session` stores the UUID as plain text; resume
uses that ID so renaming the session in Copilot does not break it. Folder-mode
launches store sessions in `.folder-sessions.json`.
Session history is local to Copilot on that machine; synchronizing workspace
definitions does not synchronize conversations. Closing the terminal before
Copilot exits successfully can leave the previous pointer unchanged.

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

Setup ignores machine-local `.last-session`, `.folder-sessions.json`, `.version`, `.paths.json`,
plus generated `*/.copilot/plugin.json`, `.shared/.copilot/plugin.json`, and the two bundled
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

### Local install from source (Windows)

Use this when npm isn't available (for example behind a registry proxy that lags
releases). It runs in Windows PowerShell 5.1 or PowerShell 7 and needs the
prerequisites above:

```powershell
.\scripts\install-local.ps1                # HEAD of this checkout (e.g. latest master after git pull)
.\scripts\install-local.ps1 -Ref v0.9.0    # a release tag (or any branch/commit)
.\scripts\install-local.ps1 -List          # installed versions; * marks the active one
.\scripts\install-local.ps1 -Use 0.9.0     # switch or roll back without rebuilding
```

The script builds that commit in a temporary git worktree (uncommitted changes are
not included), runs the unit and E2E tests against the native binary, installs it
to `%LOCALAPPDATA%\Programs\wl\<version>\` (change with `-InstallRoot`), and points
the `current` junction there. Release tags install as their version (`0.9.0`); other
commits get a MinVer pre-release version such as `0.9.1-dev.0.1`, so they never
overwrite a release. `current` is added to your user PATH once (skip with `-NoPath`).
Upgrades and rollbacks only move the junction, so running sessions keep their binary
and PATH never changes again. Run `wl setup` after switching.

Remove any other `wl` first (for example `npm uninstall -g @ctx-launcher/wl`).
The script warns if another `wl` still takes precedence on PATH. For all options, run
`Get-Help .\scripts\install-local.ps1 -Detailed`.
## Contributing and license

Issues and PRs welcome at [fotis89/ctx-launcher](https://github.com/fotis89/ctx-launcher).
Licensed under [MIT](LICENSE).
