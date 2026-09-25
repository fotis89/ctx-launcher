# wl - GitHub Copilot workspace launcher

[![npm](https://img.shields.io/npm/v/@ctx-launcher/wl)](https://npmjs.com/package/@ctx-launcher/wl)
[![CI](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml/badge.svg)](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/fotis89/ctx-launcher)](LICENSE)

`wl` launches GitHub Copilot CLI with repeatable workspace context: extra folders,
workspace AGENTS.md instructions, local skills, saved sessions, and portable path
variables. It keeps personal context outside your repos in `~/.wl-workspaces`.

## Install

Requires GitHub Copilot CLI 1.0.86 or newer on `PATH`.

```powershell
npm install -g @ctx-launcher/wl
copilot --version
```

Prebuilt npm binaries are published for Windows x64, Linux x64, and macOS arm64.
Other platforms can build from source.

### Tab completion

PowerShell profile:

```powershell
Register-ArgumentCompleter -CommandName wl -Native -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    wl "[suggest:$cursorPosition]" "$($commandAst.ToString())" |
        ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) }
}
```

bash:

```bash
_wl() {
  local c=$(wl "[suggest:${COMP_POINT}]" "${COMP_LINE}" 2>/dev/null)
  COMPREPLY=($(compgen -W "$c" -- "${COMP_WORDS[$COMP_CWORD]}"))
}
complete -F _wl wl
```

## Quick start

```powershell
cd D:\repos\my-project
wl launch                         # folder mode: current folder + shared context
wl create my-project              # Copilot proposes workspace files
wl launch my-project              # named workspace, resumes by default
wl launch my-project --new        # start and remember a fresh session
wl launch my-project --temp       # throwaway session; saved pointer unchanged
wl launch my-project -- -i "fix the failing test"
```

Useful commands:

| Command | Purpose |
| --- | --- |
| `wl launch [name] [--new \| --temp] [-- <copilot args>]` | Launch folder mode or a named workspace |
| `wl which [name] [--new \| --temp] [-- <copilot args>]` | Show the exact launch without writing files |
| `wl create [name]` | Ask Copilot to create a workspace using the bundled skill |
| `wl clone <git-url>` | Clone workspace definitions and initialize missing path variables |

Bundled skills install or refresh automatically on first launch, create, or clone.

For the details — every file wl uses, what a launch does, sessions, and
troubleshooting — see [docs/reference.md](docs/reference.md). Upgrading from
0.9.x? See the [changelog](CHANGELOG.md).

## Workspaces are folders

A workspace is just files under `~/.wl-workspaces/<name>`:

```text
~/.wl-workspaces/
|-- .paths.json                 (machine-local path variables)
|-- .folder-sessions.json       (machine-local folder-mode sessions)
|-- .shared/
|   |-- AGENTS.md               (optional shared instructions)
|   `-- .copilot/skills/...
`-- my-project/
    |-- workspace.json
    |-- AGENTS.md               (workspace instructions)
    |-- .last-session           (machine-local workspace session)
    `-- .copilot/skills/...
```

Example `workspace.json`:

```json
{
  "schemaVersion": 2,
  "name": "My project",
  "primaryRepo": "$REPOS_ROOT/my-project",
  "additionalDirs": ["~/notes"],
  "copilotArgs": ["--yolo"]
}
```

`primaryRepo` is Copilot's working directory. `additionalDirs` become `--add-dir`
arguments. `copilotArgs` are appended to every launch before one-off pass-through
arguments. Unknown fields are ignored; `schemaVersion` must be `2`.

Edit workspace `AGENTS.md` for personal workspace instructions. Edit
`.shared/AGENTS.md` for instructions that apply to every workspace. Skills are
regular Copilot skills under `.copilot/skills/<name>/SKILL.md`; workspace skill
names should use a `wl-` prefix. Generated `plugin.json` files are ignored.

Sessions resume by default. `--new` replaces `.last-session` after Copilot exits
successfully. `--temp` never reads or writes the saved pointer. Folder-mode
sessions are keyed by folder path in `.folder-sessions.json`.

## Sync across PCs

Keep workspace folders in a private git repo and clone them on another machine:

```powershell
wl clone https://github.com/you/workspaces.git
```

Use `~/` for home-relative paths and `$VAR`/`${VAR}` for machine-specific roots.
`wl launch` and `wl clone` prompt for undefined variables and save them in
`.paths.json`; edit that file to change values later. `wl which` is read-only and
reports unset variables without prompting.

Add these generated or machine-local files to that repo's ignore rules:

```gitignore
.last-session
.folder-sessions.json
.version
.paths.json
*/.copilot/plugin.json
.shared/.copilot/plugin.json
.shared/.copilot/skills/wl-workspace/
```

## Build

Requires .NET 10 SDK:

```powershell
dotnet build
dotnet test tests\wl.tests\wl.tests.csproj --verbosity quiet
```

Native AOT publish and E2E tests on Windows also require MSVC build tools:

```powershell
dotnet publish src\wl\wl.csproj -c Release -r win-x64
$env:WL_BINARY_PATH = (Resolve-Path src\wl\bin\Release\net10.0\win-x64\publish\wl.exe).Path
dotnet test tests\wl.e2e.tests\wl.e2e.tests.csproj --verbosity quiet
```

For a local Windows install from source:

```powershell
.\scripts\install-local.ps1
.\scripts\install-local.ps1 -List
.\scripts\install-local.ps1 -Use <version>
```

Issues and PRs: [fotis89/ctx-launcher](https://github.com/fotis89/ctx-launcher).
Licensed under [MIT](LICENSE).
