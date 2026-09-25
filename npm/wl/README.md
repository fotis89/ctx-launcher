# @ctx-launcher/wl

Workspace manager for GitHub Copilot CLI sessions — assembles multi-repo context, saved instructions, and skills into a single `copilot` launch.

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
wl create my-project     # ask Copilot to propose a workspace
wl launch                # Copilot in the current folder, with shared skills
wl launch my-project     # start or resume a Copilot session
```

Requires GitHub Copilot CLI 1.0.86 or newer on PATH.

See the [README](https://github.com/fotis89/ctx-launcher#readme), the [reference](https://github.com/fotis89/ctx-launcher/blob/master/docs/reference.md) (files, sessions, troubleshooting), and the [changelog](https://github.com/fotis89/ctx-launcher/blob/master/CHANGELOG.md).
