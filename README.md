# ctx-launcher

> Stop re-explaining your project every time you start an AI session.

Without it, every session starts the same way: re-attach directories, re-explain context, hope you don't mix tasks.

With it:
- one command per workspace
- isolated context per task
- instant resume

A workspace is a named AI context: repos + instructions + notes + session state.

Like `docker-compose` for AI coding sessions — define your workspace once, launch a fully configured session anywhere. Each workspace is isolated — switching workspaces is like switching to a different AI "brain".

```bash
# Feature work
wl launch feature-auth

# Debugging session
wl launch bug-login-race

# Switch back instantly (resume previous session)
wl launch feature-auth --resume
```

![demo](docs/demo.gif)

---

## Claude integration

After running `wl setup`, Claude can create and update workspaces from inside any session:

> "This is getting complex — create a workspace for this"

Claude will automatically scaffold a workspace from the current session, so the context, notes, and session state are preserved instead of staying inside a single chat.

---

## What it solves

**Separation** — Different tasks get different contexts. Same repos, different instructions, skills, and notes. Nothing gets committed to your repositories.

**Persistence** — Each workspace tracks its own session independently. Come back tomorrow, switch to a different task, switch back — the context is still there.

**Evolution** — `/wl-update-workspace` uses the current session's lessons learned to update your instructions, skills, and folders — so improvements carry forward into future sessions.

---

## Install

Download `wl.exe` from the [latest release](https://github.com/fotis89/ctx-launcher/releases/latest) and add it to your PATH.

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
├── .claude/skills/          # skills, not committed to your repo
└── ...                      # notes, specs, scratch — anything you need
```

**workspace.json** — repos, directories, and settings (`yolo`, `resume`).

**instructions.md** — system instructions loaded into every session. Architecture context, conventions, how the repos relate to each other.

**skills/** — workspace-scoped skills.

Use `wl which <name>` to preview the resolved configuration for any workspace.

<details>
<summary>workspace.json example</summary>

```json
{
  "name": "Fullstack Platform",
  "primaryRepo": "~/repos/backend-api",
  "additionalDirs": [
    "~/repos/frontend-app",
    "~/repos/shared-lib",
    "~/specs/api-docs"
  ],
  "yolo": false,
  "resume": false
}
```

Set `"yolo": true` to skip Claude's permission prompts (or pass `--yolo`).
Set `"resume": true` to always resume the previous session (or pass `--resume`).

</details>

<details>
<summary>What happens under the hood</summary>

```
claude --add-dir "~/repos/frontend-app" \
       --add-dir "~/repos/shared-lib" \
       --add-dir "~/specs/api-docs" \
       --add-dir "~/.wl-workspaces/fullstack-platform" \
       --append-system-prompt-file "~/.wl-workspaces/fullstack-platform/instructions.md"
```

</details>

---

## Commands

```
wl launch [name]           # start a session (or last-used if no name)
  --resume, -r             # resume the previous session
  --yolo                   # skip Claude permission prompts

wl create <name>           # scaffold a new workspace for the current folder
wl list                    # list all workspaces
wl which <name>            # preview resolved config, validate paths
wl edit <name>             # open workspace folder in file explorer

wl setup                   # install tab completion and /wl-create-workspace skill
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

v0.3.0 — [latest release](https://github.com/fotis89/ctx-launcher/releases/latest)

## License

[MIT](LICENSE)
