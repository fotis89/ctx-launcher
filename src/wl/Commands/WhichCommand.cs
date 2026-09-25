using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class WhichCommand(WorkspaceService workspaces, LaunchService launcher, PathsService paths, CopilotService copilot)
{
    public int Execute(string? name, bool forceNew = false, bool temporary = false, IReadOnlyList<string>? passThroughArgs = null)
    {
        if (forceNew && temporary)
        {
            Console.Error.WriteLine("Cannot use --new and --temp together.");
            return 1;
        }

        if (name is null)
        {
            return ExecuteFolderMode(forceNew, temporary, passThroughArgs);
        }

        var ws = workspaces.LoadWorkspace(name);
        if (ws is null)
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            PrintAvailableWorkspaces();
            return 1;
        }

        Console.WriteLine();
        ConsoleLabel.WriteLine("Workspace:", ws.Name);

        var repoOk = Directory.Exists(launcher.ResolveWorkspacePath(ws, ws.PrimaryRepo));
        ConsoleLabel.WriteLine("Repo:", $"{ws.PrimaryRepo} ({PathStatus(ws.PrimaryRepo, repoOk)})");

        foreach (var dir in ws.AdditionalDirs)
        {
            var ok = Directory.Exists(launcher.ResolveWorkspacePath(ws, dir));
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

        var hasInstructions = File.Exists(ws.AgentsPath);
        if (hasInstructions)
        {
            Console.WriteLine();
            var lines = File.ReadLines(ws.AgentsPath).Count();
            ConsoleLabel.WriteLine("Instructions:", $"AGENTS.md ({lines} lines)");
        }

        var lastSession = forceNew || temporary ? null : LaunchService.LoadLastSession(ws);

        if (lastSession is not null)
        {
            Console.WriteLine();
            if (lastSession is not null)
            {
                ConsoleLabel.WriteLine("Session:", "resuming previous");
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
        Console.WriteLine($"    {launcher.BuildCommandString(ws, resumeSessionId: lastSession, sharedDirPath: sharedDir, passThroughArgs: passThroughArgs)}");
        Console.WriteLine();
        return repoOk ? 0 : 1;
    }

    private int ExecuteFolderMode(bool forceNew, bool temporary, IReadOnlyList<string>? passThroughArgs)
    {
        var folderPath = Directory.GetCurrentDirectory();
        var lastSession = forceNew || temporary ? null : launcher.LoadFolderSession(folderPath);
        var sharedDir = workspaces.GetSharedDirIfExists();

        Console.WriteLine();
        ConsoleLabel.WriteLine("Folder:", folderPath);
        ConsoleLabel.WriteLine("Shared:", $"{workspaces.GetSharedDirPath()} ({(sharedDir is not null ? "ok" : "NOT FOUND — run wl setup")})");

        var sharedSkills = sharedDir is not null
            ? WorkspaceService.ListSkillNames(workspaces.GetSharedSkillsPath())
            : [];
        if (sharedSkills.Count > 0)
        {
            Console.WriteLine();
            ConsoleLabel.WriteLine("wl skills:", string.Join(", ", sharedSkills));
        }

        if (lastSession is not null)
        {
            Console.WriteLine();
            ConsoleLabel.WriteLine("Session:", "resuming previous");
        }

        var env = copilot.GetEnvironment(folderPath);
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
        Console.WriteLine($"    {launcher.BuildFolderCommandString(folderPath, lastSession, sharedDir, passThroughArgs)}");
        Console.WriteLine();
        return 0;
    }

    private void PrintAvailableWorkspaces()
    {
        Console.Error.WriteLine($"Workspaces root: {workspaces.GetWorkspacesRoot()}");
        var entries = workspaces.ListEntries();
        if (entries.Count == 0)
        {
            Console.Error.WriteLine("Available workspaces: (none)");
            return;
        }

        Console.Error.WriteLine("Available workspaces:");
        foreach (var entry in entries)
        {
            Console.Error.WriteLine($"  {entry.FolderName}");
        }
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