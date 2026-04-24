---
name: wl-create-workspace
description: Create an AI workspace from the current session context for the wl launcher tool. Use this when the user wants to save their current project setup, reuse this context later, create a workspace, capture this session, or says anything about wl/workspace/launch configuration. Also trigger when the user has been working in a multi-repo or multi-folder setup and wants to persist it.
allowed-tools: Bash Write Read Glob Grep
---

Analyze the current session and propose a workspace for the `wl` AI context launcher. Be opinionated — propose your best guess, then let the user confirm or adjust. Do not ask open-ended questions.

## Step 0: Pre-check

Run `wl which <slug>` first. If the workspace already exists, tell the user and suggest `/wl-update-workspace` instead. Only proceed with creation if no workspace exists for this project.

## Step 1: Gather context

First, assess session warmth. If the skill was invoked as the first/early turn of a fresh CLI session with no real work done yet (no files explored, no commands run, no prior substantive turns), treat this as a **cold session** — you have almost nothing to analyze beyond the cwd. For **warm sessions** (non-trivial prior activity), proceed with silent analysis as normal.

### Cold session: ask before proposing

In the cold case, silent analysis will produce a shallow proposal. Use `AskUserQuestion` to gather the minimum needed to propose well. Ask 3–5 questions, covering:

- Single repo or multi-repo — and if multi, the other paths
- Typical commands the user runs here (build, test, lint, deploy)
- External docs, specs, or OneDrive/shared folders worth pinning
- Workspace-level quirks the user wants encoded (auth setup, env vars, team conventions)
- Workflows worth capturing as skills — or "none" (a valid answer; see the value threshold in Step 2)

Keep questions concrete and offer sensible defaults. Do not ask open-ended "tell me about your project" questions.

### Warm session: silently gather

- **Primary repo**: the current working directory (check for `.git`)
- **Additional dirs**: look for clues in the conversation — referenced paths, imports from other repos, external docs/specs mentioned, `--add-dir` flags used, OneDrive/shared folders discussed
- **Project type**: language, framework, build system (check for `package.json`, `*.csproj`, `Cargo.toml`, `go.mod`, etc.)
- **Conventions**: coding style, architecture patterns, testing approach observed in the session
- **Workflows**: what the user has been doing — debugging, reviewing, testing, deploying. Candidates for skills.
- **Existing docs**: check for `CLAUDE.md` files in the primary repo and additional dirs. Read them — you need to know what they cover so you don't repeat it in instructions.md.

## Step 2: Propose

### Pre-proposal checklist

Before writing the proposal, walk through these filters. They prevent the two most common failure modes of this skill: duplicating CLAUDE.md and inventing thin wrapper skills.

**1. Duplication-diff for instructions.md.** For each bullet you plan to include, name the specific file or section that does *not* already cover it (CLAUDE.md, `.claude/rules/*`, repo-level skills). If you can't name one, drop the bullet. Workspace instructions exist to capture what CLAUDE.md *doesn't* — cross-repo relationships, additional-dir setup, environment quirks — not to restate it.

**2. Make paths portable.** Walk every path you plan to put in `workspace.json` (both `primaryRepo` and each entry in `additionalDirs`) and apply in order:

  - **Use `~/` when the path is already under the user's home.** `/Users/fokaragi/docs/x` or `C:\Users\fokaragi\docs\x` becomes `~/docs/x`. Home-rooted paths port cleanly across PCs and OSes.
  - **Envvar-ize absolute paths outside `~/`.** Drive-absolute Windows paths (`D:\repos\...`), or Unix paths like `/opt/...` or `/mnt/...`, should become a `$VAR` reference. Example: `D:\repos\ctx-launcher` → `$REPOS_ROOT/ctx-launcher` plus a `REPOS_ROOT=D:/repos` entry in `~/.wl-workspaces/.paths.json`.
  - **Skip subdirectories of `primaryRepo`.** Everything under the primary repo is already attached via `primaryRepo`. Adding `<primaryRepo>/docs` or `<primaryRepo>/src` as an additional dir is redundant — drop it.
  - **Same rules for `instructions.md`.** When you reference paths in prose (build outputs, log locations, config files), use `~/` or `$VAR` — never hardcode drive-absolute or root-absolute paths. Paths inside the primary repo should be relative to the repo root.

**3. Skill value threshold.** Only propose a skill if at least one of these holds:
  - It takes **3+ steps** to execute
  - It encodes **non-obvious knowledge** not captured in CLAUDE.md or existing repo-level skills
  - It's a **multi-command workflow** (not a single command with flags)

One-line command wrappers do not meet this bar. Writing `rush update` or `az repos pr create …` as a skill adds noise without value. When nothing clears the bar, omit the "Skills to create" section entirely — "none" is the right answer and should be shown as such.

**4. Decide the shape of the workspace.** Based on what's left after the three filters above:

- **Minimal workspace** — the repo has a thorough CLAUDE.md, no additional dirs, no cross-repo concerns, and nothing passes the skill threshold. Propose a minimal workspace in one shot: launcher config only, a near-empty `instructions.md` that points to CLAUDE.md, no skills. Don't scaffold full content and then whittle it down across multiple rounds.
- **Full workspace** — additional dirs, cross-repo concerns, or genuine workspace-level context to capture. Use the full proposal template below.

### Proposal templates

Present a proposal with enough detail for the user to judge. Pick the template that matches the shape you decided on. The inline hints next to `Yolo` and `Resume` are there on purpose — first-time users need them to judge the defaults.

**Minimal template** (the common case when CLAUDE.md is comprehensive and there are no additional dirs):

```
Proposed workspace: <slug>  (minimal — launcher config only)

  Name:         <display name>
  Primary repo: <path>
  Additional:   none
  Yolo:         yes/no    (skip permission prompts — Claude runs tools without asking before each action)
  Resume:       yes/no    (restore your prior conversation on each launch, so you pick up where you left off)

  instructions.md: one-liner pointing to CLAUDE.md and .claude/rules/*
  Skills to create: none

Reasoning: <one sentence on why nothing else is warranted — e.g.,
"single repo with a thorough CLAUDE.md; repo-level skills already
cover workflows">

Good to create it?
(Flags explained above. Change either by telling me "yolo off" or "fresh conversation each launch".)
```

**Full template** (when there are additional dirs, cross-repo context, or genuine workspace-level knowledge to capture):

```
Proposed workspace: <slug>

  Name:         <display name>
  Primary repo: <path>
  Additional:   <path1>, <path2>
  Yolo:         yes/no    (skip permission prompts — Claude runs tools without asking before each action)
  Resume:       yes/no    (restore your prior conversation on each launch, so you pick up where you left off)

  Instructions will cover:
    - <bullet — and the section of CLAUDE.md/.claude/rules that does NOT cover it>
    - <bullet — likewise>

  Skills to create:
    - /wl-<skill> — <what it does, and why it clears the value threshold>
    (or omit this section entirely if nothing cleared the threshold)

Does this look right? Any changes before I create it?
(Flags explained above. Change either by telling me "yolo off" or "fresh conversation each launch".)
```

**HARD STOP — end your turn here.** Output the proposal as your final message and do not call any tools in the same turn. Do not write `workspace.json`, `instructions.md`, or any skill files until the user replies in a new turn approving the proposal (or with edits). This applies even in auto mode — auto mode minimizes interruptions for *routine* decisions, but workspace contents are durable user-facing config and explicit approval is required. A simple "yes" / "looks good" / "go ahead" in the next turn is the green light; anything else is feedback to incorporate before re-proposing.

### Slug naming

Pick a slug that identifies the project, not the task. Use lowercase with hyphens. Prefer short, recognizable names: `backend-api`, `fullstack-platform`, `data-pipeline`. If the user has been working across multiple repos, name it after the overall system, not one repo.

### Yolo and Resume defaults

- **Yolo**: set to `true` if the user's current session already has permissions bypass enabled (dangerously-skip-permissions). Otherwise `false`.
- **Resume**: set to `true` if the project involves ongoing work where picking up where you left off is valuable (most projects). Set to `false` for one-off or ephemeral workspaces.

## Step 3: Create the workspace

Only run this step **in the turn after** the user has approved the proposal from Step 2. If you are still in the same turn as the proposal, stop — you have skipped the hand-off. If the user's reply contained edits, re-propose (back to Step 2) instead of creating; do not silently apply the edits and create in one shot.

After confirmation:

1. Create `~/.wl-workspaces/<slug>/workspace.json`:
   ```json
   {
     "name": "<display name>",
     "primaryRepo": "<repo path>",
     "additionalDirs": ["<dir1>", "<dir2>"],
     "yolo": true,
     "resume": true
   }
   ```

   If the proposal envvar-ized any paths (filter 2 in the pre-proposal checklist), also run `wl paths set <NAME> <value>` for each new variable so `~/.wl-workspaces/.paths.json` is populated on this PC. Write `$NAME/...` into the JSON fields.

2. Write `instructions.md` — this is the most important file. It should contain:
   - **System overview**: what the project is, what each repo/folder contains, how they relate
   - **Architecture**: key patterns, folder structure, dependency direction
   - **Conventions**: naming, formatting, testing expectations, commit style
   - **Debugging**: where logs are, how to trace errors, common failure modes
   - **Workflow**: how to build, test, deploy — the commands and the order

   **Do not duplicate content from repo-level `CLAUDE.md` files.** Claude Code loads those automatically when working in a repo. Before writing instructions.md, read each repo's CLAUDE.md and mentally diff your draft against it. If a fact is already in CLAUDE.md, leave it out. Workspace instructions should only contain what CLAUDE.md doesn't cover: cross-repo context (how repos relate, shared workflows), workspace-specific setup (additional dirs, environment notes), and decisions or conventions that span multiple repos.

   Write from what you observed in this session. Be specific — mention actual file paths, actual commands, actual patterns. 10-30 lines is the sweet spot. Never write placeholder text like "(describe your project)".

3. Create skills in `~/.wl-workspaces/<slug>/.claude/skills/<name>/SKILL.md` — but **only for skills that passed the value threshold in Step 2** (3+ steps, non-obvious knowledge, or multi-command workflow). If the approved proposal said "Skills to create: none" (or omitted the section), create no skills — this is an expected and common outcome.
   - Look for: test commands run, build steps, deployment, code review patterns, log analysis
   - Each skill should be a concrete action, not a description. Include the actual commands, paths, and steps.
   - Example triggers (only if they clear the threshold): `wl-run-tests` (how to test this project), `wl-deploy` (deployment steps), `wl-review` (what to check in code review)
   - Every skill needs `name`, `description`, and `allowed-tools` in frontmatter
   - **Always use the `wl-` prefix** for workspace skill names to distinguish them from repo-level skills

4. Verify with `wl which <slug>`.

## Skill format

```markdown
---
name: wl-<skill-name>
description: <one line — what this skill does and when to use it>
allowed-tools: <tools this skill needs>
---

<concrete instructions for Claude when this skill is invoked>
```

## Output

Show `wl which <slug>` output and tell the user to run `wl launch <slug>`.
