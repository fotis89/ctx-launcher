using wl.Services;

namespace wl.Commands;

public class SetupCommand(SetupService setup, CopilotRunner runner)
{
    public int Execute()
    {
        Console.WriteLine();

        var result = setup.RunSetup();
        Console.WriteLine(result.CreateWorkspaceFresh ? "  Skill wl-create-workspace installed" : "  Skill wl-create-workspace updated");
        Console.WriteLine(result.UpdateWorkspaceFresh ? "  Skill wl-update-workspace installed" : "  Skill wl-update-workspace updated");

        Console.WriteLine();
        var available = runner.TryGetVersion(out var version);
        if (available)
            Console.WriteLine($"  GitHub Copilot CLI: {version}");
        else
            Console.Error.WriteLine("Error: Copilot is not available. Install GitHub Copilot CLI and ensure `copilot` is on your PATH.");

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
        return available ? 0 : 1;
    }
}