# ctx-launcher (wl) - Named Claude Code setups you can relaunch

[![npm](https://img.shields.io/npm/v/ctx-launcher)](https://npmjs.com/package/ctx-launcher)
[![CI](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml/badge.svg)](https://github.com/fotis89/ctx-launcher/actions/workflows/ci.yml)
[![license](https://img.shields.io/github/license/fotis89/ctx-launcher)](LICENSE)

> Companion to [Claude Code](https://code.claude.com). Windows x64 prebuilt; macOS/Linux build from source.

Switching between Claude Code projects is slow. Every switch means re-attaching folders, re-explaining context, and often starting a fresh session — even if you were in the middle of something yesterday.

`wl` saves each Claude Code setup (repos, folders, instructions, skills) under a name you pick. Switch between them with one command; resume the previous session when you want.

Your repo's `CLAUDE.md` stays the team's shared context. `wl` adds your personal layer on top — not committed, not shared.

A workspace is a local folder outside your repos, storing the Claude launch config, optional instructions, optional prompts, optional skills, and a pointer to the last Claude session.

This is what using `wl` looks like:

## Example

From your project folder:

```bash
cd ~/repos/ctx-launcher
wl create wl-dev
```

Open Claude with that workspace from any directory:

```bash
wl launch wl-dev
```

Come back later and continue the same session:

```bash
wl launch wl-dev --resume
```

![demo](docs/demo.gif)

## What you can do with it

- Launch Claude with a saved workspace by name, from any directory
- Switch between projects without re-explaining context or re-attaching folders
- See which workspace is active at a glance — Claude Code shows the name in its statusline and terminal tab
- Come back to a task and pick up where you left off
- Work across multiple repos or folders in one Claude session
- Give Claude notes, instructions, and skills that travel with the workspace, not the repo
- Let Claude create the workspace for you - no JSON to write by hand

**Why not just a bash alias?** An alias can attach folders and instructions to `claude`. What it can't do: track which Claude session belongs to which project (`wl` saves a per-workspace session pointer), carry workspace-local skills Claude auto-invokes, or preview the exact launched command before running. Those are `wl`'s real differentiators.

## Install

### Windows (prebuilt)

Requires Windows x64, [Node.js](https://nodejs.org/), and [Claude Code](https://code.claude.com/docs/en/quickstart#step-1-install-claude-code) on your `PATH`.

```bash
npm install -g ctx-launcher
```

The first `wl launch` or `wl create` installs the Claude skills `wl` depends on (auto-refreshed on upgrade). Run `wl setup` if you also want tab completion or want to verify `claude` is reachable.

### macOS / Linux

No prebuilt package yet — [build from source](#build-from-source). Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and `clang` or `gcc`.

## Quick start

1. In the folder you want as Claude's primary working directory:
   ```bash
   cd ~/repos/ctx-launcher
   wl create wl-dev
   ```
   Opens Claude, which suggests a workspace for you to approve. *(Or run `wl create wl-dev --basic` to skip Claude and write a minimal `workspace.json` yourself.)*

2. Launch the workspace:
   ```bash
   wl launch wl-dev
   ```
   Opens Claude with the workspace's primary repo, attached folders, and instructions. Works from any directory.

3. Come back later and continue the same session:
   ```bash
   wl launch wl-dev --resume
   ```
   Same as step 2, but continues the previous session instead of starting fresh.

## Commands

| Command | What it does |
| --- | --- |
| `wl create [name]` | Creates a workspace from the current repo (asks Claude to fill it in) |
| `wl create <name> --basic` | Creates a minimal `workspace.json` without invoking Claude |
| `wl launch [name]` | Launches a workspace; omit `name` to use the last one launched |
| `wl launch <name> --resume` | Resumes the previous Claude session for that workspace |
| `wl launch <name> --new` | Starts a fresh session even if the workspace defaults to resume |
| `wl launch <name> --yolo` | Skips Claude's permission prompts |
| `wl launch <name> -p <name-or-text>` | Starts with a saved prompt, or with raw prompt text |
| `wl list` | Lists all workspaces |
| `wl which <name>` | Shows the exact `claude` command `wl` will run, and checks paths exist |
| `wl edit <name>` | Opens the workspace folder in your system file explorer |
| `wl setup` | (Optional) prints a tab-completion snippet and verifies `claude` is reachable |

## What's a workspace?

A workspace is a folder at `~/.wl-workspaces/<name>/`, separate from your repos. It holds a saved setup: a primary repo, any additional folders to attach, and optional project-specific instructions.

```text
~/.wl-workspaces/wl-dev/
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

The only required file. It defines the workspace name, the repo Claude starts in, any additional folders to attach, and launch defaults.

```json
{
  "name": "wl dev",
  "primaryRepo": "~/repos/ctx-launcher",
  "additionalDirs": [
    "~/docs/wl-notes"
  ],
  "yolo": true,
  "resume": true
}
```

- `primaryRepo` - Claude's working directory when the session starts
- `additionalDirs` - extra repos or folders attached with `--add-dir`
- `yolo` - default `wl launch` to `--dangerously-skip-permissions`
- `resume` - default `wl launch` to resuming the last session

### `instructions.md`

If present, `wl launch` passes this file to Claude with `--append-system-prompt-file`. Use it for notes that don't belong in a repo's `CLAUDE.md`: multi-repo relationships, cross-repo workflows, project-specific context.

### `prompts/*.md`

Reusable launch prompts kept with the workspace.

```md
---
label: Review changes
---
Review the latest changes, summarize the risk, and call out anything that needs manual testing.
```

Launch with the saved prompt:

```bash
wl launch wl-dev -p review
```

Or pass raw text directly:

```bash
wl launch wl-dev -p "investigate the failing test and explain the root cause"
```

### `.claude/skills/`

Claude Code skills that travel with the workspace instead of with the repo's git history. `wl launch` attaches them to the session automatically.

## What's behind a launch

Run `wl which <name>` any time to see exactly what `wl launch` will do:

```
$ wl which wl-dev

  Workspace:    wl-dev
  Repo:         ~/repos/ctx-launcher (ok)
  Dir:          ~/docs/wl-notes (ok)
  Shared:       ~/.wl-workspaces/.shared (ok)

  wl skills:    /wl-update-workspace
  Skills:       /wl-review

  Instructions: instructions.md (38 lines)
  Prompts:      review

  Permissions:  yolo
  Resume:       auto

  Command:
    claude `
      --resume <session-id> `
      --add-dir ~/docs/wl-notes `
      --add-dir ~/.wl-workspaces/.shared `
      --add-dir ~/.wl-workspaces/wl-dev `
      --append-system-prompt-file ~/.wl-workspaces/wl-dev/instructions.md `
      --dangerously-skip-permissions
```

No magic — `wl launch` just spawns `claude` with the composed flags. `wl which` previews path resolution, skill discovery, and the exact command before you run it.

## Claude skills shipped with wl

`wl` ships two Claude Code skills, installed automatically on first use:

- **`/wl-create-workspace`** — used by `wl create`. Inspects the current repo and session, proposes a name, the folders to attach, and a draft `instructions.md`, and waits for your approval before writing files. You can also invoke it directly inside any Claude Code session.
- **`/wl-update-workspace`** — run from inside a launched session when the workspace no longer matches the project or how you work. It diffs the workspace against the current state, proposes updates, and waits for your approval.

Both skills auto-refresh after `wl` upgrades. Run `wl setup` to force a re-install.

## Useful details

- `wl which <name>` is the fastest way to confirm path resolution, spot missing attached folders, and inspect the exact `claude ...` command `wl` will run.
- `wl edit <name>` opens the workspace folder so you can tweak `instructions.md`, prompts, or workspace-local skills by hand.

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

## Contributing

Issues and PRs welcome at [github.com/fotis89/ctx-launcher/issues](https://github.com/fotis89/ctx-launcher/issues). Run `dotnet test` before submitting.

## License

[MIT](LICENSE)
