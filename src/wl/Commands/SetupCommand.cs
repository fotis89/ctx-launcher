using System.Reflection;

using wl.Services;

namespace wl.Commands;

public class SetupCommand(WorkspaceService workspaces, VersionService versionService)
{
    private static string LoadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"wl.Resources.{name}")
            ?? throw new InvalidOperationException($"Missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

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
        InstallSharedSkills();
        versionService.StampVersion();
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

        File.AppendAllText(bashrc, snippet.ReplaceLineEndings("\n"));
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

        File.WriteAllText(skillFile, LoadResource("wl-create-workspace.md"));
        Console.WriteLine(exists
            ? "  Claude skill /wl-create-workspace updated to latest version."
            : "  Claude skill /wl-create-workspace installed.");
        Console.WriteLine("  Use it in any Claude Code session to create workspaces.");
    }

    private void InstallSharedSkills()
    {
        workspaces.EnsureSharedDir();
        var skillDir = Path.Combine(workspaces.GetSharedSkillsPath(), "wl-update-workspace");
        var skillFile = Path.Combine(skillDir, "SKILL.md");

        var exists = File.Exists(skillFile);
        Directory.CreateDirectory(skillDir);

        File.WriteAllText(skillFile, LoadResource("wl-update-workspace.md"));
        Console.WriteLine(exists
            ? "  Skill /wl-update-workspace updated to latest version."
            : "  Skill /wl-update-workspace installed.");

        // Clean up old location
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var oldSkillDir = Path.Combine(home, ".claude", "skills", "wl-update-workspace");
        if (Directory.Exists(oldSkillDir))
        {
            Directory.Delete(oldSkillDir, true);
            Console.WriteLine("  Migrated /wl-update-workspace from ~/.claude/skills/ to .shared.");
        }
    }

}
