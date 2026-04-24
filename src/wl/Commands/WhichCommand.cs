using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class WhichCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher, PathsService paths)
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
        ConsoleLabel.WriteLine("Workspace:", ws.Name);

        var (repoOk, _) = PathHelper.ValidatePath(ws.PrimaryRepo, paths.Get);
        ConsoleLabel.WriteLine("Repo:", $"{ws.PrimaryRepo} ({PathStatus(ws.PrimaryRepo, repoOk)})");

        foreach (var dir in ws.AdditionalDirs)
        {
            var (ok, _) = PathHelper.ValidatePath(dir, paths.Get);
            ConsoleLabel.WriteLine("Dir:", $"{dir} ({PathStatus(dir, ok)})");
        }

        var sharedDir = workspaces.GetSharedDirIfExists();
        ConsoleLabel.WriteLine("Shared:", $"{workspaces.GetSharedDirPath()} ({(sharedDir is not null ? "ok" : "NOT FOUND — run wl setup")})");

        var sharedSkills = sharedDir is not null
            ? WorkspaceService.ListSkillNames(workspaces.GetSharedSkillsPath())
            : [];
        var skills = WorkspaceService.ListSkillNames(ws.SkillsPath);
        if (sharedSkills.Count > 0 || skills.Count > 0)
        {
            Console.WriteLine();
            if (sharedSkills.Count > 0)
            {
                ConsoleLabel.WriteLine("wl skills:", string.Join(", ", sharedSkills.Select(s => "/" + s)));
            }
            if (skills.Count > 0)
            {
                ConsoleLabel.WriteLine("Skills:", string.Join(", ", skills.Select(s => "/" + s)));
            }
        }

        var savedPrompts = prompts.ListPrompts(ws);
        var hasInstructions = File.Exists(ws.InstructionsPath);
        if (hasInstructions || savedPrompts.Count > 0)
        {
            Console.WriteLine();
            if (hasInstructions)
            {
                var lines = File.ReadLines(ws.InstructionsPath).Count();
                ConsoleLabel.WriteLine("Instructions:", $"instructions.md ({lines} lines)");
            }
            else
            {
                ConsoleLabel.WriteLine("Instructions:", "(none)");
            }
            if (savedPrompts.Count > 0)
            {
                ConsoleLabel.WriteLine("Prompts:", string.Join(", ", savedPrompts.Select(p => p.Slug)));
            }
        }

        var lastSession = ws.Resume ? LaunchService.LoadLastSession(ws) : null;

        if (ws.Yolo || ws.Resume)
        {
            Console.WriteLine();
            if (ws.Yolo)
            {
                ConsoleLabel.WriteLine("Permissions:", "yolo");
            }
            if (ws.Resume)
            {
                var suffix = lastSession is null ? " (no saved session — will start fresh)" : "";
                ConsoleLabel.WriteLine("Resume:", $"auto{suffix}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Command:");
        Console.WriteLine($"    {launcher.BuildCommandString(ws, yolo: ws.Yolo, resumeSessionId: lastSession, sharedDirPath: sharedDir)}");
        Console.WriteLine();
    }

    private string PathStatus(string rawPath, bool exists)
    {
        if (exists) return "ok";

        var unsetVars = PathHelper.ExtractVariables(rawPath)
            .Where(v => paths.Get(v) is null)
            .Distinct()
            .ToList();

        if (unsetVars.Count > 0)
        {
            return $"unset: {string.Join(", ", unsetVars.Select(v => "$" + v))} — run 'wl paths init'";
        }

        return "NOT FOUND";
    }
}
