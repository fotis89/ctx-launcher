# ctx-launcher - workspace manager for Claude Code sessions

> CLAUDE.md gives you context per repo. Sessions give you history. Workspaces give you both — context + history.

Claude keeps context, but it doesn't isolate workspaces.

ctx-launcher (`wl`) gives each workspace an isolated, resumable environment:

- Separate instructions, skills, and session per workspace
- Combine multiple repos into one session
- Resume any workspace by name — restores the exact session
- Custom instructions and skills without committing them to your repos

Like `docker-compose` for AI coding sessions — reusable, isolated environments for Claude.

## Works with Claude

After running `wl setup`, you can ask Claude:

> "This is getting complex — create a workspace for this"

Claude will:
- detect your repos and project structure
- generate instructions (without duplicating CLAUDE.md)
- suggest skills based on your workflow
- create the workspace automatically

Workspaces evolve over time — run `/wl-update-workspace` and Claude diffs your workspace against the repo state, flags outdated instructions and skills, and proposes updates for review.

---

```bash
# Create workspaces (you pick the name)
wl create feature-work
wl create incident-response

# Launch a workspace — starts a configured Claude Code session
wl launch feature-work

# Switch to a different workspace
wl launch incident-response

# Resume where you left off
wl launch feature-work --resume
```

![demo](docs/demo.gif)

### Example: debugging a production issue

You're working on a feature, then a production bug comes in. Instead of losing context:

```bash
wl launch incident-response
```

Claude sees your logs, debugging notes, relevant repos, and incident-specific instructions. Fix the issue, then switch back:

```bash
wl launch feature-work --resume
```

Everything is exactly where you left it.

---

## Install

Download `wl.exe` from the [latest release](https://github.com/fotis89/ctx-launcher/releases/latest) and add it to your PATH.

---

## Quick start

### Option A: Let Claude create your workspace

1. Install `wl` and run `wl setup` to install the Claude skills
2. Open Claude Code in your project and ask: `/wl-create-workspace`
3. Claude analyzes the repo, proposes instructions and skills, and creates everything
4. From now on: `wl launch my-project`
5. As your project evolves, run `/wl-update-workspace` inside a session to keep instructions and skills in sync

### Option B: Create manually

```bash
cd your-repo
wl create my-project          # scaffolds ~/.wl-workspaces/my-project/
```

Edit `instructions.md` and add skills, then launch:

```bash
wl launch my-project
```

## Commands

```
wl launch [name]           # start a session (or last-used if no name)
  --resume, -r             # resume previous session (automatic when resume: true in config)
  --new, -n                # start fresh (overrides resume: true)
  --yolo                   # skip Claude permission prompts
  -p <slug>                # start with a saved prompt

wl create <name>           # scaffold a new workspace for the current folder
wl list                    # list all workspaces
wl which <name>            # preview resolved config, validate paths
wl edit <name>             # open workspace folder in file explorer

wl setup                   # install tab completion and Claude skills
```

---

## How it works

Workspaces live under `~/.wl-workspaces/`, outside your repos. When you run `wl launch`, it starts Claude Code with your repos attached and the workspace folder as an additional working directory — Claude can read and write files there.

```
~/.wl-workspaces/my-project/
├── workspace.json
├── instructions.md
└── .claude/skills/
```

**workspace.json** — the workspace definition. This is the only required file. It tells `wl` which repos to attach, which folders Claude can see, and how the session behaves.

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

- `primaryRepo` — Claude's working directory. This is where `git` commands run.
- `additionalDirs` — extra folders Claude can see (other repos, docs, specs).
- `yolo` — skip Claude's permission prompts (`--yolo`).
- `resume` — automatically resume the last session instead of starting fresh (`--resume`).

**instructions.md** — loaded as system instructions every session. Put context that CLAUDE.md doesn't cover — how repos relate, workspace-specific workflows, cross-repo conventions. Don't duplicate what's already in your repos' CLAUDE.md files.

**.claude/skills/** — workspace-scoped slash commands. Work exactly like repo-level skills but live outside the repo. Useful for personal workflows, deploy scripts, review checklists — anything you don't want to commit.

Use `wl which <name>` to preview the full resolved configuration.

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

**Linux/macOS** — requires `clang` or `gcc`:

```bash
dotnet publish src/wl -c Release -r linux-x64    # or osx-x64 / osx-arm64
```

**Windows** — requires MSVC build tools ([Visual Studio](https://visualstudio.microsoft.com/) C++ workload or standalone [Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)):

```bash
dotnet publish src/wl -c Release -r win-x64
```

Output: ~4 MB native binary. Copy it to a directory in your PATH.

## Status

v0.4.0 — [latest release](https://github.com/fotis89/ctx-launcher/releases/latest)

## License

[MIT](LICENSE)
