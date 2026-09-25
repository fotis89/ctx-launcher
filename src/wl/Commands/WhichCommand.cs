using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class WhichCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher, PathsService paths, CopilotService copilot)
{
    public int Execute(string name)
    {
        var ws = workspaces.LoadWorkspace(name);
        if (ws is null)
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            return 1;
        }

        Console.WriteLine();
        ConsoleLabel.WriteLine("Workspace:", ws.Name);

        var repoOk = Directory.Exists(PathHelper.ResolvePath(ws.PrimaryRepo, paths.Get));
        ConsoleLabel.WriteLine("Repo:", $"{ws.PrimaryRepo} ({PathStatus(ws.PrimaryRepo, repoOk)})");

        foreach (var dir in ws.AdditionalDirs)
        {
            var ok = Directory.Exists(PathHelper.ResolvePath(dir, paths.Get));
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
                ConsoleLabel.WriteLine("wl skills:", string.Join(", ", sharedSkills));
            }
            if (skills.Count > 0)
            {
                ConsoleLabel.WriteLine("Skills:", string.Join(", ", skills));
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

        var prep = copilot.DescribeLaunchPrep(ws).ToList();
        var env = copilot.GetEnvironment(ws);
        if (prep.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Launch prep:");
            foreach (var line in prep)
            {
                Console.WriteLine($"    - {line}");
            }
        }
        if (env.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Environment:");
            foreach (var (k, v) in env)
            {
                Console.WriteLine($"    {k}={v}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Command:");
        Console.WriteLine($"    {launcher.BuildCommandString(ws, yolo: ws.Yolo, resumeSessionId: lastSession, sharedDirPath: sharedDir)}");
        Console.WriteLine();
        return repoOk ? 0 : 1;
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