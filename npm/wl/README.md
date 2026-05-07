# @ctx-launcher/wl

Workspace manager for Claude Code and GitHub Copilot CLI sessions — assembles multi-repo context, saved instructions, and skills into a single `claude` or `copilot` launch.

## Install

```bash
npm install -g @ctx-launcher/wl
```

This gives you the `wl` command. Prebuilt binaries are published for:

- Windows x64 (`win32-x64`)
- Linux x64 (`linux-x64`)
- macOS Apple Silicon (`darwin-arm64`)

npm will only download the binary matching your platform. For other platforms, [build from source](https://github.com/fotis89/ctx-launcher#build-from-source).

## Quick start

```bash
wl setup                 # install workspace skills
wl create my-project     # create a workspace (auto-detects Claude or Copilot, or pass --tool)
wl launch my-project     # start a session in the configured tool
```

See the [full documentation](https://github.com/fotis89/ctx-launcher) for more.
