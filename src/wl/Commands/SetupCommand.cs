using wl.Services;

namespace wl.Commands;

public class SetupCommand(SetupService setup, ClaudeRunner claudeRunner)
{
    public void Execute()
    {
        Console.WriteLine();

        var result = setup.RunSetup();
        Console.WriteLine(result.CreateWorkspaceFresh ? "  Skill /wl-create-workspace installed" : "  Skill /wl-create-workspace updated");
        Console.WriteLine(result.UpdateWorkspaceFresh ? "  Skill /wl-update-workspace installed" : "  Skill /wl-update-workspace updated");

        Console.WriteLine();
        ReportTool("Claude Code", "claude", "https://code.claude.com");
        ReportTool("GitHub Copilot CLI", "copilot", "https://docs.github.com/copilot/how-tos/copilot-cli");

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

    private void ReportTool(string label, string command, string installUrl)
    {
        if (claudeRunner.TryGetVersion(command, out var version))
        {
            Console.WriteLine($"  {label}: {version}");
        }
        else
        {
            Console.WriteLine($"  {label}: NOT FOUND — install from {installUrl} and ensure `{command}` is on your PATH (optional if you use the other tool)");
        }
    }
}
