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
wl setup                 # install workspace skills
wl create my-project     # ask Copilot to propose a workspace
wl launch my-project     # start a Copilot session
```

Requires GitHub Copilot CLI 1.0.86 or newer on PATH. Copilot-only workspaces use explicit
`schemaVersion: 2` and `.copilot/skills`. The `tool` field and `--tool` flag are
removed; existing workspaces require a manual upgrade.

See the [full documentation and upgrade guide](https://github.com/fotis89/ctx-launcher#upgrading-existing-workspaces).
