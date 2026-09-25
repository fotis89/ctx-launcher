---
name: wl-workspace
description: Create or update wl workspace folders. Use when the user wants to create a workspace, capture project context, refresh workspace instructions, add skills, fix paths, or sync workspace config with actual usage.
---

Manage wl workspaces as reviewable files. You can create a new workspace or update an existing one, but you must propose changes first and wait for explicit approval before writing durable workspace files.

## Core rules

- Workspaces live under `WL_WORKSPACES_ROOT` when set, otherwise `~/.wl-workspaces`.
- `workspace.json` must use `"schemaVersion": 2` and may contain `name`, `primaryRepo`, `additionalDirs`, and `copilotArgs`.
- Do not emit removed fields such as `yolo` or `resume`.
- Workspace instructions are written directly to `<workspace>/AGENTS.md`; do not duplicate content already covered by repo-level `AGENTS.md` or `.github/instructions/*`.
- Workspace skills live under `<workspace>/.copilot/skills/<wl-name>/SKILL.md`; use the `wl-` prefix.
- `allowed-tools` is optional permission pre-approval. Omit it unless the user explicitly asks for narrow approvals.
- Use portable paths: `~/` for home paths, existing variables from `.paths.json` when available, and new `$VAR` references for machine-specific absolute roots.
- If you add a new `$VAR`, tell the user to add it to `.paths.json` or let `wl launch`/`wl clone` prompt for it.

## Create flow

1. Inspect the current repo/session, existing repo `AGENTS.md`, `.github/instructions/*`, and existing workspace folders.
2. Decide whether the workspace is minimal (config plus a short `AGENTS.md`) or needs cross-repo context and workspace skills.
3. Present a proposal:

```
Proposed workspace: <slug>

  Name:         <display name>
  Primary repo: <path>
  Additional:   <none or paths>
  Copilot args: <none or JSON array, e.g. ["--yolo"]>

  AGENTS.md will cover:
    - <workspace-only context not already in repo instructions>

  Skills to create:
    - wl-<skill> — <why it clears the value threshold>
```

**HARD STOP:** after the proposal, end your turn. Do not write `workspace.json`, `AGENTS.md`, or skill files until the user approves in a new turn.

After approval, create `workspace.json`, `AGENTS.md`, and only the approved skills. Keep `AGENTS.md` specific and concise (usually 10-30 lines). Verify with `wl which <slug>`.

## Update flow

1. Identify the workspace from attached directories or the workspace root folder.
2. Read `workspace.json`, `AGENTS.md`, all `.copilot/skills/*/SKILL.md`, repo instructions, and recent repo changes.
3. Detect drift:
   - missing or unused additional dirs
   - outdated AGENTS.md sections
   - repeated workflows that deserve a skill
   - stale skill commands or paths
   - non-portable paths that should become `~/` or `$VAR`
   - obsolete `copilotArgs`
4. Propose exact changes, including before/after text for AGENTS.md edits. Wait for approval.
5. Apply only approved changes and verify with `wl which <name>`.

## Skill threshold

Create a workspace skill only when it has 3+ steps, captures non-obvious project knowledge, or implements a real multi-command workflow. Do not create one-line wrappers.
