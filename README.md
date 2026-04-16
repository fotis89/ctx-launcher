# ctx-launcher - named workspaces for Claude Code

[![npm](https://img.shields.io/npm/v/ctx-launcher)](https://npmjs.com/package/ctx-launcher)
[![CI](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml/badge.svg)](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/fotis89/ctx-launcher)](LICENSE)

> `CLAUDE.md` gives you repo context. Claude sessions give you history. Workspaces give you both - context and history per workstream.

`ctx-launcher` (`wl`) is a workspace launcher for Claude Code. It keeps workspace files outside your repos and relaunches Claude with the right primary repo, attached directories, workspace context, and session state for that workstream.

`CLAUDE.md` is repo-scoped. Claude's built-in resume is session-scoped. `wl` gives you a workstream-scoped layer you can name, switch, and reopen across one repo or many.

- Resume an ongoing Claude workstream by name
- Attach multiple repos or folders to one session
- Keep workspace instructions and skills out of the repo
- Save reusable prompts per workspace
- Preview the exact resolved launch command with `wl which`

![demo](docs/demo.gif)

## Works with Claude

`wl setup` installs two skills that power the main flows:

- `/wl-create-workspace` - used by `wl create`, and also available directly from any Claude session when you want to turn the current session into its own workspace. Claude will:
  - inspect the repo and current session
  - propose a workspace name and attached directories
  - draft `instructions.md` without duplicating repo `CLAUDE.md`
  - suggest skills based on the observed workflow
  - wait for your approval before writing files

- `/wl-update-workspace` - invoke from inside a launched session. Claude will:
  - diff the workspace against the current repo state
  - flag drift such as outdated instructions, stale commands, or renamed paths
  - propose updates for review
  - wait for your approval before writing files

## Install (Windows)

Requires Windows x64, [Node.js](https://nodejs.org/), and [Claude Code](https://code.claude.com/docs/en/quickstart#step-1-install-claude-code) on your `PATH`.

```bash
npm install -g ctx-launcher
wl setup
```

`wl setup` installs the Claude skills and prints a PowerShell or Bash tab-completion snippet you can add to your shell profile.

Verify Claude Code is on `PATH` by running `claude --version` in a new terminal.

## Quick start

1. In the repo you want to turn into a workspace:
   ```bash
   wl create
   # or
   wl create my-project
   ```
   This opens Claude and runs `/wl-create-workspace`. Claude inspects the current repo and session, proposes a workspace, and waits for your approval before writing files.

2. Launch it:
   ```bash
   wl launch my-project
   ```

3. Come back later and resume the same workstream:
   ```bash
   wl launch my-project --resume
   ```

4. From inside a launched session, refresh the workspace when the repo drifts:
   ```text
   /wl-update-workspace
   ```

## Commands

| Command | What it does |
| --- | --- |
| `wl create [name]` | Starts `/wl-create-workspace`; if `name` is omitted, Claude proposes a slug |
| `wl launch [name]` | Launches a workspace; if `name` is omitted, uses the last-used workspace |
| `wl launch <name> --resume` | Resumes the previous Claude session for that workspace |
| `wl launch <name> --new` | Starts a fresh session even if the workspace defaults to resume |
| `wl launch <name> --yolo` | Skips Claude permission prompts |
| `wl launch <name> -p <slug-or-text>` | Starts with a saved prompt slug or raw prompt text |
| `wl list` | Lists all workspaces |
| `wl which <name>` | Validates paths and shows the resolved `claude` command |
| `wl edit <name>` | Opens the workspace folder in your system file explorer |
| `wl setup` | Installs or updates skills and prints tab-completion setup |

## Workspace files

Workspaces live under `~/.wl-workspaces/`, separate from your repos.

```text
~/.wl-workspaces/my-project/
|-- workspace.json
|-- instructions.md        (optional)
|-- prompts/               (optional)
|   `-- review.md
`-- .claude/               (optional)
    `-- skills/
        `-- wl-review/
            `-- SKILL.md
```

### `workspace.json`

This is the only required file. It defines the workspace name, the primary repo Claude starts in, any extra attached directories, and launch defaults.

```json
{
  "name": "My Project",
  "primaryRepo": "~/repos/backend-api",
  "additionalDirs": [
    "~/repos/frontend-app",
    "~/repos/shared-lib"
  ],
  "yolo": false,
  "resume": true
}
```

- `primaryRepo` - Claude's working directory
- `additionalDirs` - extra repos, docs, specs, or folders to attach with `--add-dir`
- `yolo` - defaults launches to `--dangerously-skip-permissions`
- `resume` - defaults launches to the last saved session for that workspace

### `instructions.md`

If present, `wl launch` passes this file to Claude with `--append-system-prompt-file`. Use it for workspace-only context such as multi-repo relationships, cross-repo workflows, and notes you do not want in a repo `CLAUDE.md`.

### `prompts/*.md`

Saved prompts let you keep reusable launch prompts with the workspace.

```md
---
label: Review changes
---
Review the latest changes, summarize the risk, and call out anything that needs manual testing.
```

Launch with the saved prompt:

```bash
wl launch my-project -p review
```

Or pass raw text directly:

```bash
wl launch my-project -p "investigate the failing test and explain the root cause"
```

### `.claude/skills/`

Workspace skills work like repo-level Claude skills, but they live outside the repo and travel with the workspace instead of your git history.

## Useful details

- `wl which <name>` is the fastest way to confirm path resolution, see missing attached directories, and inspect the exact `claude ...` command `wl` will run.
- `wl edit <name>` opens the workspace folder so you can tweak `instructions.md`, prompts, or workspace-local skills directly.
- `wl setup` should be rerun after upgrading `ctx-launcher` so installed skills stay in sync with the binary version.

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/fotis89/ctx-launcher.git
cd ctx-launcher
dotnet build wl.slnx --verbosity quiet
dotnet test wl.slnx --verbosity quiet
```

### Publish a native binary

The published npm package is Windows x64 only. If you want to build native binaries yourself:

**Windows** - requires MSVC build tools ([Visual Studio](https://visualstudio.microsoft.com/) C++ workload or standalone [Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)):

```bash
dotnet publish src/wl -c Release -r win-x64
```

**Linux/macOS** - requires `clang` or `gcc`:

```bash
dotnet publish src/wl -c Release -r linux-x64
dotnet publish src/wl -c Release -r osx-x64
dotnet publish src/wl -c Release -r osx-arm64
```

Copy the published binary to a directory on your `PATH`.

## License

[MIT](LICENSE)
