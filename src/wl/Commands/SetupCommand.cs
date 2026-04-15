namespace wl.Commands;

public class SetupCommand
{
    public void Execute()
    {
        var isPowerShell = Environment.GetEnvironmentVariable("PSModulePath") is not null;

        Console.WriteLine();
        if (isPowerShell)
        {
            Console.WriteLine("  Detected: PowerShell");
            Console.WriteLine();
            Console.WriteLine("  To enable tab completion, add this to your $PROFILE:");
            Console.WriteLine();
            Console.WriteLine("    Register-ArgumentCompleter -CommandName wl -Native -ScriptBlock {");
            Console.WriteLine("        param($wordToComplete, $commandAst, $cursorPosition)");
            Console.WriteLine("        $ast = $commandAst.ToString()");
            Console.WriteLine("        wl \"[suggest:$cursorPosition]\" \"$ast\" | ForEach-Object {");
            Console.WriteLine("            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)");
            Console.WriteLine("        }");
            Console.WriteLine("    }");
        }
        else
        {
            Console.WriteLine("  Detected: Bash/Zsh");
            Console.WriteLine();
            Console.WriteLine("  To enable tab completion, add this to your shell profile:");
            Console.WriteLine();
            Console.WriteLine("    _wl_completions() {");
            Console.WriteLine("        local completions=$(wl \"[suggest:${COMP_POINT}]\" \"${COMP_LINE}\" 2>/dev/null)");
            Console.WriteLine("        COMPREPLY=($(compgen -W \"$completions\" -- \"${COMP_WORDS[$COMP_CWORD]}\"))");
            Console.WriteLine("    }");
            Console.WriteLine("    complete -F _wl_completions wl");
        }

        Console.WriteLine();
        Console.WriteLine("  Note: cmd.exe does not support tab completion.");
        Console.WriteLine("  Use PowerShell or Bash for the best experience.");
        Console.WriteLine();
        Console.Write("  Install tab completion automatically? (y/n): ");
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (answer == "y")
        {
            if (isPowerShell)
            {
                InstallPowerShell();
            }
            else
            {
                InstallBash();
            }
        }

        InstallClaudeSkill();
    }

    private static void InstallPowerShell()
    {
        // Use the system's Documents folder (respects OneDrive redirection)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documentsPath))
        {
            Console.Error.WriteLine("  Could not determine Documents folder path.");
            return;
        }

        // Install to both PowerShell 7+ and Windows PowerShell 5.1
        var profiles = new[]
        {
            Path.Combine(documentsPath, "PowerShell"),
            Path.Combine(documentsPath, "WindowsPowerShell"),
        };

        var snippet =
"""

# wl tab completion
Register-ArgumentCompleter -CommandName wl -Native -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $ast = $commandAst.ToString()
    wl "[suggest:$cursorPosition]" "$ast" | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
    }
}
""";

        foreach (var psProfileDir in profiles)
        {
            Directory.CreateDirectory(psProfileDir);
            var psProfile = Path.Combine(psProfileDir, "Microsoft.PowerShell_profile.ps1");

            if (File.Exists(psProfile) && File.ReadAllText(psProfile).Contains("# wl tab completion"))
            {
                Console.WriteLine($"  Already installed: {psProfile}");
                continue;
            }

            File.AppendAllText(psProfile, snippet);
            Console.WriteLine($"  Installed to: {psProfile}");
        }
        Console.WriteLine("  Restart PowerShell to activate.");
    }

    private static void InstallBash()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var bashrc = Path.Combine(home, ".bashrc");

        var snippet =
"""

# wl tab completion
_wl_completions() {
    local completions=$(wl "[suggest:${COMP_POINT}]" "${COMP_LINE}" 2>/dev/null)
    COMPREPLY=($(compgen -W "$completions" -- "${COMP_WORDS[$COMP_CWORD]}"))
}
complete -F _wl_completions wl
""";

        if (File.Exists(bashrc) && File.ReadAllText(bashrc).Contains("_wl_completions"))
        {
            Console.WriteLine("  Already installed in .bashrc.");
            return;
        }

        File.AppendAllText(bashrc, snippet);
        Console.WriteLine($"  Installed to: {bashrc}");
        Console.WriteLine("  Restart your shell to activate.");
    }

    private static void InstallClaudeSkill()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var skillDir = Path.Combine(home, ".claude", "skills", "wl-create-workspace");
        var skillFile = Path.Combine(skillDir, "SKILL.md");

        var exists = File.Exists(skillFile);
        Directory.CreateDirectory(skillDir);

        var content =
"""
---
name: wl-create-workspace
description: Create an AI workspace from the current session context for the wl launcher tool. Use this when the user wants to save their current project setup, reuse this context later, create a workspace, capture this session, or says anything about wl/workspace/launch configuration. Also trigger when the user has been working in a multi-repo or multi-folder setup and wants to persist it.
allowed-tools: Bash(wl *) Write Read Glob Grep
---

Analyze the current session and propose a workspace for the `wl` AI context launcher. Be opinionated — propose your best guess, then let the user confirm or adjust. Do not ask open-ended questions.

## Step 1: Gather context

Before proposing, silently gather:

- **Primary repo**: the current working directory (check for `.git`)
- **Additional dirs**: look for clues in the conversation — referenced paths, imports from other repos, external docs/specs mentioned, `--add-dir` flags used, OneDrive/shared folders discussed
- **Project type**: language, framework, build system (check for `package.json`, `*.csproj`, `Cargo.toml`, `go.mod`, etc.)
- **Conventions**: coding style, architecture patterns, testing approach observed in the session
- **Workflows**: what the user has been doing — debugging, reviewing, testing, deploying. These become skills.
- **Existing docs**: check for `CLAUDE.md` files in the primary repo and additional dirs. These are already loaded by Claude Code automatically — workspace instructions must not duplicate them.

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
    - /<skill-1> — <what it does>
    - /<skill-2> — <what it does>

Does this look right? Any changes before I create it?
```

### Slug naming

Pick a slug that identifies the project, not the task. Use lowercase with hyphens. Prefer short, recognizable names: `backend-api`, `fullstack-platform`, `data-pipeline`. If the user has been working across multiple repos, name it after the overall system, not one repo.

## Step 3: Create the workspace

After confirmation:

1. Create `~/.wl-workspaces/<slug>/workspace.json`:
   ```json
   {
     "name": "<display name>",
     "primaryRepo": "<repo path>",
     "additionalDirs": ["<dir1>", "<dir2>"],
     "yolo": false,
     "resume": false
   }
   ```

2. Write `instructions.md` — this is the most important file. It should contain:
   - **System overview**: what the project is, what each repo/folder contains, how they relate
   - **Architecture**: key patterns, folder structure, dependency direction
   - **Conventions**: naming, formatting, testing expectations, commit style
   - **Debugging**: where logs are, how to trace errors, common failure modes
   - **Workflow**: how to build, test, deploy — the commands and the order

   **Do not duplicate content from repo-level `CLAUDE.md` files.** Claude Code loads those automatically when working in a repo. Workspace instructions should only contain what `CLAUDE.md` doesn't cover: cross-repo context (how repos relate, shared workflows), workspace-specific setup (additional dirs, environment notes), and decisions or conventions that span multiple repos. If a repo's `CLAUDE.md` already documents architecture, build commands, and conventions, don't repeat them — reference the repo by name and focus on the bigger picture.

   Write from what you observed in this session. Be specific — mention actual file paths, actual commands, actual patterns. 10-30 lines is the sweet spot. Never write placeholder text like "(describe your project)".

3. Create skills in `.claude/skills/<name>/SKILL.md` based on workflows you observed:
   - Look for: test commands run, build steps, deployment, code review patterns, log analysis
   - Each skill should be a concrete action, not a description. Include the actual commands, paths, and steps.
   - Example triggers: `run-tests` (how to test this project), `deploy` (deployment steps), `review` (what to check in code review)
   - Every skill needs `name`, `description`, and `allowed-tools` in frontmatter

4. Create empty `prompts/` directory.

5. Verify with `wl which <slug>`.

## Skill format

```markdown
---
name: <skill-name>
description: <one line — what this skill does and when to use it>
allowed-tools: <tools this skill needs>
---

<concrete instructions for Claude when this skill is invoked>
```

## Output

Show `wl which <slug>` output and tell the user to run `wl launch <slug>`.
""";

        File.WriteAllText(skillFile, content);
        Console.WriteLine(exists
            ? "  Claude skill /wl-create-workspace updated to latest version."
            : "  Claude skill /wl-create-workspace installed.");
        Console.WriteLine("  Use it in any Claude Code session to create workspaces.");
    }
}