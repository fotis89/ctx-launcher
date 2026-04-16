---
name: wl-create-workspace
description: Create an AI workspace from the current session context for the wl launcher tool. Use this when the user wants to save their current project setup, reuse this context later, create a workspace, capture this session, or says anything about wl/workspace/launch configuration. Also trigger when the user has been working in a multi-repo or multi-folder setup and wants to persist it.
allowed-tools: Bash Write Read Glob Grep
---

Analyze the current session and propose a workspace for the `wl` AI context launcher. Be opinionated — propose your best guess, then let the user confirm or adjust. Do not ask open-ended questions.

## Step 0: Pre-check

Run `wl which <slug>` first. If the workspace already exists, tell the user and suggest `/wl-update-workspace` instead. Only proceed with creation if no workspace exists for this project.

## Step 1: Gather context

Before proposing, silently gather:

- **Primary repo**: the current working directory (check for `.git`)
- **Additional dirs**: look for clues in the conversation — referenced paths, imports from other repos, external docs/specs mentioned, `--add-dir` flags used, OneDrive/shared folders discussed
- **Project type**: language, framework, build system (check for `package.json`, `*.csproj`, `Cargo.toml`, `go.mod`, etc.)
- **Conventions**: coding style, architecture patterns, testing approach observed in the session
- **Workflows**: what the user has been doing — debugging, reviewing, testing, deploying. These become skills.
- **Existing docs**: check for `CLAUDE.md` files in the primary repo and additional dirs. Read them — you need to know what they cover so you don't repeat it in instructions.md.

## Step 2: Propose

Present a proposal with enough detail for the user to judge:

```
Proposed workspace: <slug>

  Name:         <display name>
  Primary repo: <path>
  Additional:   <path1>, <path2> (or "none")
  Yolo:         yes/no
  Resume:       yes/no

  Instructions will cover:
    - <what the project is and how it's structured>
    - <key conventions and architecture decisions>
    - <debugging and workflow notes>

  Skills to create:
    - /wl-<skill-1> — <what it does>
    - /wl-<skill-2> — <what it does>

Does this look right? Any changes before I create it?
```

### Slug naming

Pick a slug that identifies the project, not the task. Use lowercase with hyphens. Prefer short, recognizable names: `backend-api`, `fullstack-platform`, `data-pipeline`. If the user has been working across multiple repos, name it after the overall system, not one repo.

### Yolo and Resume defaults

- **Yolo**: set to `true` if the user's current session already has permissions bypass enabled (dangerously-skip-permissions). Otherwise `false`.
- **Resume**: set to `true` if the project involves ongoing work where picking up where you left off is valuable (most projects). Set to `false` for one-off or ephemeral workspaces.

## Step 3: Create the workspace

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

2. Write `instructions.md` — this is the most important file. It should contain:
   - **System overview**: what the project is, what each repo/folder contains, how they relate
   - **Architecture**: key patterns, folder structure, dependency direction
   - **Conventions**: naming, formatting, testing expectations, commit style
   - **Debugging**: where logs are, how to trace errors, common failure modes
   - **Workflow**: how to build, test, deploy — the commands and the order

   **Do not duplicate content from repo-level `CLAUDE.md` files.** Claude Code loads those automatically when working in a repo. Before writing instructions.md, read each repo's CLAUDE.md and mentally diff your draft against it. If a fact is already in CLAUDE.md, leave it out. Workspace instructions should only contain what CLAUDE.md doesn't cover: cross-repo context (how repos relate, shared workflows), workspace-specific setup (additional dirs, environment notes), and decisions or conventions that span multiple repos.

   Write from what you observed in this session. Be specific — mention actual file paths, actual commands, actual patterns. 10-30 lines is the sweet spot. Never write placeholder text like "(describe your project)".

3. Create skills in `~/.wl-workspaces/<slug>/.claude/skills/<name>/SKILL.md` based on workflows you observed:
   - Look for: test commands run, build steps, deployment, code review patterns, log analysis
   - Each skill should be a concrete action, not a description. Include the actual commands, paths, and steps.
   - Example triggers: `wl-run-tests` (how to test this project), `wl-deploy` (deployment steps), `wl-review` (what to check in code review)
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
