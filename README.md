# ctx-launcher

> Like `docker-compose` for AI coding sessions. Define your workspace once, start a fully configured session anywhere.

Create a workspace from any existing Claude Code session by asking Claude to `/create-workspace`, then launch it:

```
wl launch fullstack
```

Each workspace is a persistent AI development environment that lives outside your repositories — combining multi-repo context, external folders, instructions, skills, and saved prompts. Workspaces can represent projects or workflows, such as feature development, bug fixing, or live issue debugging, each with its own tailored context.
---

## Why not just CLAUDE.md?

`CLAUDE.md` works for single repos. Real-world workflows are multi-context:

- **Multiple repos** (backend + frontend, service + shared libs)
- **External folders** outside any repo (specs, docs, design files)
- **Skills and prompts** you don't want committed to the repo
- **Frequent project switching** throughout the day

ctx-launcher bundles all of this into a workspace that lives outside your repos and starts a fully configured session in one command.

---

## Before and after

**Before:** Re-open Claude Code, re-add repos, re-load specs, re-explain architecture, re-state conventions — every session starts from scratch.

**After:** `wl launch fullstack`

```
  Launching: Fullstack Platform
  Repo: D:\repos\backend-api
  Instructions: 12 lines
  Skills: /run-tests
  Dirs: +2 additional
```

Switch between workspaces instantly:

```
$ wl launch data-pipeline
$ wl launch fullstack
$ wl launch              # re-opens last workspace
```

---

## How it works

Workspaces live under `~/.ai-workspaces/`:

```
~/.ai-workspaces/fullstack/
├── workspace.json           # repos + related folders
├── instructions.md          # instructions loaded into the session
├── prompts/                 # reusable task prompts
└── .claude/skills/          # skills, not committed to your repo
```

When you run `wl launch`, ctx-launcher:

1. Reads the workspace definition
2. Resolves and validates all paths
3. Attaches repos, folders, instructions, and skills
4. Starts a fully configured Claude Code session

Your repos stay clean — no AI config committed, no team friction.

<details>
<summary>What happens under the hood</summary>

```
claude --add-dir "D:\repos\frontend-app" \
       --add-dir "C:\...\api-specs" \
       --add-dir "~/.ai-workspaces/fullstack" \
       --append-system-prompt-file "~/.ai-workspaces/fullstack/instructions.md"
```

Use `wl which <name>` to preview the resolved configuration for any workspace.

</details>

ctx-launcher is not a script runner — it's a workspace composition system for AI coding sessions. It manages a persistent layer of repos, folders, instructions, skills, and prompts that survives across sessions and projects. Define context once, reuse it forever.

---

## Quick start

```bash
cd your-repo
wl create my-project
# Edit ~/.ai-workspaces/my-project/instructions.md
wl launch my-project
```

## Setup

Run `wl setup` to install:

- **Tab completion** for PowerShell or Bash (workspace names, prompt slugs, flags)
- **`/create-workspace` Claude skill** (mentioned above)

## Commands

### Core

```
wl launch [name]          # start a workspace session (or last-used if no name)
wl create <name>          # scaffold a new workspace from current repo
wl list                   # list all workspaces
```

### Inspect and manage

```
wl which <name>           # preview the resolved workspace config, validate paths
wl edit <name>            # open workspace folder in file explorer
```

### Advanced

```
wl launch <name> -p <prompt>   # start session with a saved prompt or raw text
```

---

## Workspace format

### workspace.json

```json
{
  "name": "Fullstack Platform",
  "primaryRepo": "D:\\repos\\backend-api",
  "additionalDirs": [
    "D:\\repos\\frontend-app",
    "C:\\Users\\you\\Documents\\api-specs"
  ]
}
```

### instructions.md

Plain markdown loaded as instructions into the session.

### prompts/{slug}.md

Reusable task prompts:

```markdown
---
label: Review latest changes
---
Review the latest changes and suggest improvements.
```

---

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and MSVC build tools (Visual Studio C++ workload).

```bash
git clone https://github.com/fotis89/ctx-launcher.git
cd ctx-launcher
dotnet publish src/wl -c Release -r win-x64
```

Output: `src/wl/bin/Release/net10.0/win-x64/publish/wl.exe` (~4 MB, Native AOT) — copy it to a directory in your PATH.

## Running tests

```bash
dotnet test
```

## Status

v0.1.0 — initial release

## License

[MIT](LICENSE)
