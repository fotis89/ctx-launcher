# ctx-launcher

A CLI tool that launches Claude Code with multi-repo context, external folders, system instructions, and reusable prompts — without polluting your repositories.

```
wl launch my-api
```

---

## Why not just CLAUDE.md?

`CLAUDE.md` works for single repos.

Real workflows are multi-context:

- **Multiple repos** working together (backend + frontend, service + shared libs)
- **External folders** outside any repo (specs, shared docs, design files)
- **Custom skills and prompts** you don't want committed
- **Switching between projects** throughout the day

ctx-launcher solves all of these. Your workspace config lives outside your repos, wires everything together, and launches Claude Code with full context in one command.

---

## Before and after

**Before:** Re-explain your project every session. Or commit AI-specific config to repos your team shares.

**After:**

```
$ wl launch my-api

  Launching: My API
  Repo: D:\repos\my-api
  Instructions: 12 lines
  Skills: /run-tests
  Dirs: +1 additional
```

Switch projects instantly:

```
$ wl launch data-pipeline
$ wl launch my-api
$ wl launch              # re-launches last used
```

---

## How it works

Each workspace lives under `~/.ai-workspaces/` — outside any repo:

```
~/.ai-workspaces/my-api/
├── workspace.json           # repos + extra dirs
├── instructions.md          # system prompt for Claude
├── prompts/                 # reusable task prompts
└── .claude/skills/          # skills, not committed to your repo
```

`wl launch` turns this into:

```
claude --add-dir "C:\...\api-specs" \
       --add-dir "~/.ai-workspaces/my-api" \
       --append-system-prompt-file "~/.ai-workspaces/my-api/instructions.md"
```

Your repos stay clean.

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
- **`/create-workspace` Claude skill** — from any existing Claude Code session, ask Claude to create a workspace from your current context. It generates `workspace.json`, `instructions.md`, prompts, and skills — ready to use, yours to tweak.

## Commands

### Core

```
wl launch [name]          # launch workspace (or last-used if no name)
wl create <name>          # scaffold workspace from current repo
wl list                   # list all workspaces
```

### Inspect and manage

```
wl which <name>           # dry run — show resolved command, validate paths
wl edit <name>            # open workspace folder in file explorer
```

### Advanced

```
wl launch <name> -p <prompt>   # launch with saved prompt or raw text
```

---

## Workspace format

### workspace.json

```json
{
  "name": "My API",
  "primaryRepo": "D:\\repos\\my-api",
  "additionalDirs": [
    "C:\\Users\\you\\Documents\\api-specs"
  ]
}
```

### instructions.md

Plain markdown appended to Claude's system prompt.

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
