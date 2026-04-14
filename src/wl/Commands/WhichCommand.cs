using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class WhichCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher)
{
    public void Execute(string name)
    {
        var ws = workspaces.LoadWorkspace(name);
        if (ws is null)
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"  Workspace: {ws.Name}");

        var (repoOk, _) = PathHelper.ValidatePath(ws.PrimaryRepo);
        Console.WriteLine($"  Repo:      {ws.PrimaryRepo} ({(repoOk ? "ok" : "NOT FOUND")})");

        foreach (var dir in ws.AdditionalDirs)
        {
            var (ok, _) = PathHelper.ValidatePath(dir);
            Console.WriteLine($"  Dir:       {dir} ({(ok ? "ok" : "NOT FOUND")})");
        }

        if (File.Exists(ws.InstructionsPath))
        {
            var lines = File.ReadLines(ws.InstructionsPath).Count();
            Console.WriteLine($"  Instructions: instructions.md ({lines} lines)");
        }
        else
        {
            Console.WriteLine("  Instructions: (none)");
        }

        if (Directory.Exists(ws.SkillsPath))
        {
            var skills = Directory.GetDirectories(ws.SkillsPath)
                .Select(d => "/" + Path.GetFileName(d))
                .ToList();
            if (skills.Count > 0)
            {
                Console.WriteLine($"  Skills:    {string.Join(", ", skills)}");
            }
        }

        var savedPrompts = prompts.ListPrompts(ws);
        if (savedPrompts.Count > 0)
        {
            Console.WriteLine($"  Prompts:   {string.Join(", ", savedPrompts.Select(p => p.Slug))}");
        }

        if (ws.Yolo)
        {
            Console.WriteLine($"  Permissions: yolo");
        }

        Console.WriteLine();
        Console.WriteLine("  Command:");
        Console.WriteLine($"    {launcher.BuildCommandString(ws, yolo: ws.Yolo)}");
        Console.WriteLine();
    }
}