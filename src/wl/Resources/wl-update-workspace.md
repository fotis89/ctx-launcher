---
name: wl-update-workspace
description: Detect drift between workspace config and actual session usage, then propose reviewable updates. Use when the user wants to refresh instructions, add new skills, fix outdated paths, or sync the workspace with how the project has evolved. Also trigger on "update workspace", "refresh workspace", or "sync workspace".
allowed-tools: Bash Write Read Glob Grep
---

Detect when the active workspace no longer matches real usage and propose safe, explicit updates. This is drift detection — observe, compare, propose, confirm, apply. Never silently mutate workspace state.

## Step 1: Identify the workspace

Figure out which workspace to update without asking the user:

1. Check attached directories — look for a `~/.wl-workspaces/<name>/` path in the session's additional dirs.
2. If unclear, run `wl list` and match against the current working directory's repo.
3. Only ask the user if neither approach resolves to a single workspace.

Read all workspace files:
- `workspace.json` — repos, dirs, settings
- `instructions.md` — current instructions
- `.claude/skills/` — all existing skills
- Any other workspace files

Run `wl which <name>` to see resolved config and path warnings.

## Step 2: Detect drift

Compare workspace config against the repo's current state. Don't rely only on conversation history — a fresh session may have none. Actively investigate:

### How to investigate

- **Read the repo's `CLAUDE.md`** and diff it mentally against `instructions.md`. Flag any content in instructions.md that duplicates what CLAUDE.md already covers — Claude Code loads CLAUDE.md automatically, so workspace instructions should only cover cross-repo context, workspace-specific setup, and multi-repo decisions.
- **Check `git log --oneline -20`** in the primary repo for recent changes that might invalidate instructions (renamed files, new build steps, moved directories).
- **Scan the file tree** (`ls` key directories) for new folders, removed files, or structural changes that instructions.md doesn't reflect.
- **Read each skill's SKILL.md** and verify the commands, paths, and steps it references still exist.

### Categories of drift

**Additions** (missing from workspace):
- Repo or directory used in session but not in workspace.json
- Recurring instruction pattern not documented
- Repeated workflow that should be a skill
- External folder referenced but not attached

**Removals** (unused config):
- Repo or directory in workspace.json but never touched
- Skill that doesn't match current workflows
- Skill that doesn't clear the value threshold (3+ steps, non-obvious knowledge, or multi-command workflow) — single-flag wrappers don't warrant being skills
- Workspace file no longer relevant

**Modifications** (outdated config):
- Instructions reference renamed files, removed patterns, or old decisions
- Paths changed (build output, binary locations)
- Workflow steps changed (new commands, different order)
- Skills with outdated commands or paths
- Settings (`yolo`, `resume`) that no longer match how the workspace is used
- Skills not using the `wl-` naming prefix (workspace skills should always be prefixed `wl-` to distinguish them from repo-level skills)
- Skills missing required frontmatter fields (`name`, `description`, `allowed-tools`) — propose adding the missing fields
- Non-portable paths in `primaryRepo` or `additionalDirs`, in priority order:
  - Paths under the user's home that aren't `~/`-rooted (`/Users/foo/x`, `C:\Users\foo\x`) — propose rewriting as `~/x`.
  - Absolute paths outside `~/` (drive letters, `/opt`, `/mnt`) — propose rewriting as `$VAR` references and running `wl paths set <NAME> <value>` to populate `~/.wl-workspaces/.paths.json`.
  - Subdirectories of `primaryRepo` listed as additional dirs — propose removing; they're already attached via `primaryRepo`.
- Non-portable paths in `instructions.md` prose — drive-absolute or root-absolute paths that should be `~/`, `$VAR`, or relative-to-repo. Propose rewriting in place.

## Step 3: Propose

Present drift as a structured proposal. Show actual content for instructions.md changes so the user can judge — "updated section X" isn't enough to approve.

```
## Workspace update proposal: <name>

### Detected drift

#### Added
- <path or item> — <why: observed in N contexts / used repeatedly>

#### Removed
- <path or item> — <why: unused / obsolete>

#### Updated
- instructions.md: <section name>
  - Before: "<quoted old text>"
  - After: "<quoted new text>"
- skills/wl-<name>: <what changed and why>

### No changes needed
- <list unchanged files so the user knows you checked>

Apply these changes? [y/n]
```

If nothing needs updating, say so and stop.

## Step 4: Apply (only after confirmation)

After the user confirms:

1. Apply only the approved changes.
2. When updating `instructions.md` — edit specific sections, don't rewrite from scratch. Preserve the user's structure and voice.
3. When updating skills — preserve existing structure and allowed-tools. Update only the commands, paths, and steps that changed.
4. Run `wl which <name>` to verify paths resolve and config is valid.
5. Show what was changed:
   ```
   Workspace updated:
   - N additions
   - N removals
   - N updates
   ```

## Safety

This skill proposes changes for the user to review — it never silently mutates workspace state. Show what will change before changing it, require explicit confirmation, and keep changes reversible.
