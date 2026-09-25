# Changelog

## 0.10.0 — 2026-09-25

Breaking. wl is reduced to four commands (`launch`, `which`, `create`, `clone`)
and gains folder mode. Existing workspaces keep loading (`schemaVersion` is
still `2`), but a few fields and files change meaning — follow **Upgrading**.

### Upgrading from 0.9.x

| Before | Now |
| --- | --- |
| `<workspace>/instructions.md` (copied to a generated `AGENTS.md`) | Edit `<workspace>/AGENTS.md` directly. Rename `instructions.md` to `AGENTS.md` (delete the old generated copy first) and remove `*/AGENTS.md` from `.gitignore`. |
| `"yolo": true` | `"copilotArgs": ["--yolo"]`. `yolo` is now ignored, so without this Copilot asks for permissions again. |
| `"resume": true`, `--resume` | Remove them; every launch resumes the remembered session. Use `--new` for a fresh one. |
| `wl launch` (no name) reopened the last workspace | `wl launch` now opens Copilot in the current folder (folder mode). Use `wl launch <name>`. |
| `-p <prompt>`, `prompts/*.md` | Pass Copilot's own flag: `wl launch <name> -- -i "text"`. Turn reusable prompts into skills. |
| `--yolo` flag | `wl launch <name> -- --yolo`, or `copilotArgs`. |
| `wl list` | Type any unknown name, or look in `~/.wl-workspaces`. |
| `wl edit <name>` | Open `~/.wl-workspaces/<name>` in your editor. |
| `wl paths set/list/init` | wl asks for undefined variables at launch/clone; edit `.paths.json` to change them. |
| `wl setup` | Automatic on the first launch/create/clone after an upgrade. Tab-completion snippets are in the README. |
| `wl create <name> --basic` | `wl create [name]`, or write `workspace.json` by hand. |
| `wl-create-workspace`, `wl-update-workspace` skills | One `wl-workspace` skill. Delete the two old folders under `.shared/.copilot/skills/` and their `.gitignore` lines. |
| `.last` file | No longer used; delete it. |

### Added

- Folder mode: `wl launch` in any folder, with shared skills and `.shared/AGENTS.md`;
  sessions remembered per folder in `.folder-sessions.json`.
- Shared instructions: `.shared/AGENTS.md` applies to every launch.
- `--temp` for throwaway sessions that don't replace the remembered one.
- `copilotArgs` in `workspace.json`, and `--` to pass arguments straight to Copilot.
- `wl which` accepts the same options as `launch`, and works in folder mode.
- Bare `wl` prints help.
- [docs/reference.md](docs/reference.md): files, launch steps, sessions, troubleshooting.

### Fixed

- Windows: an npm-installed Copilot (`copilot.cmd`) is run through `node` directly,
  so prompt text containing quotes, `%` or `&` is passed intact instead of being
  reinterpreted by `cmd.exe`.
- Windows: `copilot` is found in PATH order, matching the shell.
- A broken `.paths.json` is no longer overwritten (losing your other variables)
  the next time a variable is saved.
- Relative paths in `workspace.json` resolve from the workspace folder, the same
  for wl and Copilot, wherever you run wl from.

### Removed

- Claude-era migration checks (`tool`, `defaultTool`, `.config.json`, `.claude/skills`).

## Earlier releases

See [GitHub releases](https://github.com/fotis89/ctx-launcher/releases).
