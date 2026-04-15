# ctx-launcher

> Like `docker-compose` for AI coding sessions. Define your workspace once, start a fully configured session anywhere.

Create a workspace from any existing Claude Code session by asking Claude to `/wl-create-workspace`, then launch it:

```
wl launch fullstack-platform
```

![demo](docs/demo.gif)

Each workspace is a persistent AI development environment that lives outside your repositories. Workspaces can represent projects or workflows — feature development, bug fixing, live issue debugging — each with its own tailored context.

---

## Why not just CLAUDE.md?

`CLAUDE.md` works for single repos. Real-world workflows need more:

**Multi-repo sessions** — You're working across repos that belong together (backend + frontend, service + shared libs) and want Claude to see the whole system — how the pieces fit — without re-attaching directories every session.

**Repo-clean customization** — You want to customize how Claude behaves (instructions, skills, workflows) without committing AI-specific config to your repositories. Useful for personal workflows, shared/company repos, or just keeping things clean.

**Persistent working directory (notes, state, docs)** — You need a place for things that don't belong in a repo: notes, specs, debugging state, scratch work. The workspace acts as a persistent working directory that's always attached to the session and survives across runs.

The "define once, launch anytime" part is the value across all three — you set it up once, and every session starts ready.

---

## Quick start

```bash
cd your-repo
wl create my-project
# Edit ~/.wl-workspaces/my-project/instructions.md
wl launch my-project
```

## How it works

Workspaces live under `~/.wl-workspaces/`. The workspace folder itself is attached to every session as a working directory — Claude can read and write files there.

```
~/.wl-workspaces/fullstack-platform/
├── workspace.json           # repos, folders, settings
├── instructions.md          # system instructions for the session
├── prompts/                 # reusable task prompts
├── .claude/skills/          # skills, not committed to your repo
└── ...                      # notes, specs, scratch — anything you need
```

**workspace.json** — defines the repos and directories attached to the session:

```json
{
  "name": "Fullstack Platform",
  "primaryRepo": "D:\\repos\\backend-api",
  "additionalDirs": [
    "D:\\repos\\frontend-app",
    "D:\\repos\\shared-lib",
    "D:\\specs\\api-docs"
  ],
  "yolo": false
}
```

Set `"yolo": true` to skip Claude's permission prompts for the workspace (or pass `--yolo` on the command line).

**instructions.md** — system instructions loaded into every session. Architecture context, conventions, how the repos relate to each other.

**prompts/{slug}.md** — reusable task prompts with frontmatter:

```markdown
---
label: Review latest changes
---
Review the latest changes and suggest improvements.
```

**.claude/skills/** — workspace-scoped skills (test runners, deploy helpers, review workflows).

Use `wl which <name>` to preview the resolved configuration for any workspace.

<details>
<summary>What happens under the hood</summary>

```
claude --add-dir "D:\repos\frontend-app" \
       --add-dir "D:\repos\shared-lib" \
       --add-dir "D:\specs\api-docs" \
       --add-dir "~/.wl-workspaces/fullstack-platform" \
       --append-system-prompt-file "~/.wl-workspaces/fullstack-platform/instructions.md"
```

</details>

---

## Commands

```
wl launch [name]               # start a session (or last-used if no name)
wl launch <name> -p <prompt>   # start with a saved prompt or raw text
wl launch <name> --yolo        # skip Claude permission prompts
wl create <name>               # scaffold a new workspace for the current folder

wl list                        # list all workspaces
wl which <name>                # preview resolved config, validate paths
wl edit <name>                 # open workspace folder in file explorer

wl setup                       # install /wl-create-workspace skill and optional tab completion
```

---

## Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/fotis89/ctx-launcher.git
cd ctx-launcher
dotnet build        # build
dotnet test         # run tests
```

### Publishing a native binary

Publishing a self-contained native binary (AOT) additionally requires MSVC build tools. You can get these from [Visual Studio](https://visualstudio.microsoft.com/) (C++ workload) or the standalone [Build Tools for Visual Studio](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022).

```bash
dotnet publish src/wl -c Release -r win-x64
```

Output: `src/wl/bin/Release/net10.0/win-x64/publish/wl.exe` (~4 MB), copy it to a directory in your PATH.

> **`vswhere.exe` not recognized?** Run from a [Developer Command Prompt](https://learn.microsoft.com/en-us/visualstudio/ide/reference/command-prompt-powershell) or add `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to your PATH.

## Status

v0.1.0 — initial release

## License

[MIT](LICENSE)
