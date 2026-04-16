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

        var sharedDir = workspaces.GetSharedDirIfExists();
        Console.WriteLine($"  Shared:    {workspaces.GetSharedDirPath()} ({(sharedDir is not null ? "ok" : "NOT FOUND — run wl setup")})");

        var sharedSkills = sharedDir is not null
            ? WorkspaceService.ListSkillNames(workspaces.GetSharedSkillsPath())
            : [];
        if (sharedSkills.Count > 0)
        {
            Console.WriteLine($"  wl skills: {string.Join(", ", sharedSkills.Select(s => "/" + s))}");
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

        var skills = WorkspaceService.ListSkillNames(ws.SkillsPath);
        if (skills.Count > 0)
        {
            Console.WriteLine($"  Skills:    {string.Join(", ", skills.Select(s => "/" + s))}");
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

        if (ws.Resume)
        {
            Console.WriteLine($"  Resume: auto");
        }

        Console.WriteLine();
        Console.WriteLine("  Command:");
        var lastSession = ws.Resume ? LaunchService.LoadLastSession(ws) : null;
        Console.WriteLine($"    {launcher.BuildCommandString(ws, yolo: ws.Yolo, resumeSessionId: lastSession, sharedDirPath: sharedDir)}");
        Console.WriteLine();
    }
}
