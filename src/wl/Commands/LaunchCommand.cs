using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class LaunchCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher)
{
    public void Execute(string? name, string? promptArg, bool yolo = false)
    {
        if (name is null)
        {
            name = workspaces.GetLastUsed();
            if (name is null)
            {
                Console.Error.WriteLine("No workspace specified and no last-used workspace found.");
                Console.Error.WriteLine("Run: wl launch <name>");
                return;
            }
        }

        var ws = workspaces.LoadWorkspace(name);
        if (ws is null)
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            Console.Error.WriteLine("Run 'wl list' to see available workspaces.");
            return;
        }

        var (repoExists, _) = PathHelper.ValidatePath(ws.PrimaryRepo);
        if (!repoExists)
        {
            Console.Error.WriteLine($"Error: primary repo not found: {ws.PrimaryRepo}");
            return;
        }

        string? resolvedPrompt = null;
        if (promptArg is not null)
        {
            resolvedPrompt = prompts.ResolvePrompt(ws, promptArg);
        }

        var skipPermissions = yolo || ws.Yolo;
        var (_, skippedDirs) = launcher.BuildClaudeArgs(ws, resolvedPrompt, skipPermissions);

        foreach (var dir in skippedDirs)
        {
            Console.Error.WriteLine($"  Warning: directory not found: {dir} (skipping)");
        }

        var instructionLines = File.Exists(ws.InstructionsPath)
            ? File.ReadLines(ws.InstructionsPath).Count() : 0;

        var skillNames = Directory.Exists(ws.SkillsPath)
            ? Directory.GetDirectories(ws.SkillsPath).Select(Path.GetFileName).ToList()
            : [];

        Console.WriteLine();
        Console.WriteLine($"  Launching: {ws.Name}");
        Console.WriteLine($"  Repo: {ws.PrimaryRepo}");
        if (instructionLines > 0)
        {
            Console.WriteLine($"  Instructions: {instructionLines} lines");
        }

        if (skillNames.Count > 0)
        {
            Console.WriteLine($"  Skills: {string.Join(", ", skillNames.Select(s => "/" + s))}");
        }

        if (ws.AdditionalDirs.Count > 0)
        {
            Console.WriteLine($"  Dirs: +{ws.AdditionalDirs.Count} additional");
        }

        if (resolvedPrompt is not null)
        {
            Console.WriteLine($"  Prompt: {(resolvedPrompt.Length > 60 ? resolvedPrompt[..57] + "..." : resolvedPrompt)}");
        }

        Console.WriteLine();

        if (skipPermissions)
        {
            Console.WriteLine("  Permissions: skipped (yolo)");
        }

        workspaces.SetLastUsed(name);
        launcher.Launch(ws, resolvedPrompt, skipPermissions);
    }
}
