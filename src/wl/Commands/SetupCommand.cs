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
        Console.Write("  Install automatically? (y/n): ");
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

        var snippet = """

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

        var snippet = """

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
        var skillDir = Path.Combine(home, ".claude", "skills", "create-workspace");
        var skillFile = Path.Combine(skillDir, "SKILL.md");

        if (File.Exists(skillFile))
        {
            Console.WriteLine("  Claude skill /create-workspace already installed.");
            return;
        }

        Directory.CreateDirectory(skillDir);

        var content = """
            ---
            name: create-workspace
            description: Create an AI workspace for the wl launcher tool
            allowed-tools: Bash(wl *) Write Read
            ---

            Create a new workspace for the `wl` AI context launcher. The user will tell you the workspace name, which repo it's for, and what related folders to include.

            ## Steps

            1. Create the workspace folder at `~/.ai-workspaces/<name>/`
            2. Write `workspace.json` with:
               ```json
               {
                 "name": "<display name>",
                 "primaryRepo": "<repo path>",
                 "additionalDirs": ["<dir1>", "<dir2>"],
                 "yolo": false
               }
               ```
               - `yolo`: When `true`, launches Claude with `--dangerously-skip-permissions` (no tool approval prompts). Ask the user if they want YOLO mode enabled.
            3. Write `instructions.md` with context about the project — what it does, what the related dirs contain, and guidelines for working with it. Ask the user what to include.
            4. Create empty `prompts/` and `.claude/skills/` directories
            5. Optionally create saved prompts in `prompts/<slug>.md` with frontmatter:
               ```markdown
               ---
               label: <display label>
               ---
               <prompt text>
               ```
            6. Optionally create skills in `.claude/skills/<name>/SKILL.md`
            7. Verify with `wl which <name>` to confirm everything is valid

            ## Output
            After creating, show the user:
            - The `wl which <name>` output
            - Remind them to edit `instructions.md` if they want to add more context
            - Tell them to run `wl launch <name>` to use it
            """;

        File.WriteAllText(skillFile, content);
        Console.WriteLine($"  Claude skill installed: /create-workspace");
        Console.WriteLine("  Use it in any Claude Code session to create workspaces.");
    }
}