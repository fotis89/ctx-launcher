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
        if (claudeRunner.TryGetVersion(out var version))
        {
            Console.WriteLine($"  Claude Code: {version}");
        }
        else
        {
            Console.WriteLine("  Claude Code: NOT FOUND — install from https://code.claude.com and ensure `claude` is on your PATH");
        }

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
}
