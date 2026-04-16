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
        Console.WriteLine();

        InstallClaudeSkill();
        InstallSharedSkills();
        versionService.StampVersion();

        Console.WriteLine();
        Console.WriteLine("  Tab completion (optional):");
        if (Environment.GetEnvironmentVariable("PSModulePath") is not null)
        {
            Console.WriteLine("  Add to your PowerShell profile (run `notepad $PROFILE` to open or create):");
            Console.WriteLine();
            Console.WriteLine("    Register-ArgumentCompleter -CommandName wl -Native -ScriptBlock {");
            Console.WriteLine("        param($w, $ast, $pos)");
            Console.WriteLine("        wl \"[suggest:$pos]\" \"$($ast.ToString())\" | ForEach-Object {");
            Console.WriteLine("            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)");
            Console.WriteLine("        }");
            Console.WriteLine("    }");
        }
        else
        {
            Console.WriteLine("  Add to your ~/.bashrc:");
            Console.WriteLine();
            Console.WriteLine("    _wl() {");
            Console.WriteLine("        local c=$(wl \"[suggest:${COMP_POINT}]\" \"${COMP_LINE}\" 2>/dev/null)");
            Console.WriteLine("        COMPREPLY=($(compgen -W \"$c\" -- \"${COMP_WORDS[$COMP_CWORD]}\"))");
            Console.WriteLine("    }");
            Console.WriteLine("    complete -F _wl wl");
        }
        Console.WriteLine();
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
            ? "  Skill /wl-create-workspace updated"
            : "  Skill /wl-create-workspace installed");
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
            ? "  Skill /wl-update-workspace updated"
            : "  Skill /wl-update-workspace installed");

        // Clean up old location
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var oldSkillDir = Path.Combine(home, ".claude", "skills", "wl-update-workspace");
        if (Directory.Exists(oldSkillDir))
        {
            Directory.Delete(oldSkillDir, true);
        }
    }

}
