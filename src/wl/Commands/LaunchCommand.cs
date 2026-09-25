using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class LaunchCommand(WorkspaceService workspaces, LaunchService launcher, SetupService setup)
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

        launcher.EnsureWorkspaceVariables(ws);
        var repoExists = Directory.Exists(launcher.ResolveWorkspacePath(ws, ws.PrimaryRepo));
        if (!repoExists)
        {
            Console.Error.WriteLine($"Error: primary repo not found: {ws.PrimaryRepo}");
            return 1;
        }

        var resumeSessionId = forceNew || temporary ? null : LaunchService.LoadLastSession(ws);
        var shouldResume = resumeSessionId is not null;

        setup.EnsureInstalled();
        var sharedDirResolved = workspaces.GetSharedDirIfExists();
        var (args, skippedDirs, newSessionId) = launcher.BuildLaunchArgs(ws, resumeSessionId, sharedDirResolved, temporary, passThroughArgs);

        foreach (var dir in skippedDirs)
        {
            Console.Error.WriteLine($"  Warning: directory not found: {dir} (skipping)");
        }

        var instructionLines = File.Exists(ws.AgentsPath)
            ? File.ReadLines(ws.AgentsPath).Count() : 0;

        var skillNames = WorkspaceService.ListSkillNames(ws.SkillsPath);
        if (sharedDirResolved is not null)
        {
            skillNames.AddRange(WorkspaceService.ListSkillNames(workspaces.GetSharedSkillsPath()));
        }

        Console.WriteLine();
        ConsoleLabel.WriteLine("Launching:", ws.Name);
        ConsoleLabel.WriteLine("Repo:", ws.PrimaryRepo);
        if (instructionLines > 0)
        {
            ConsoleLabel.WriteLine("Instructions:", $"{instructionLines} lines");
        }

        if (skillNames.Count > 0)
        {
            ConsoleLabel.WriteLine("Skills:", string.Join(", ", skillNames));
        }

        if (ws.AdditionalDirs.Count > 0)
        {
            ConsoleLabel.WriteLine("Dirs:", $"{ws.AdditionalDirs.Count} additional");
        }

        if (shouldResume)
        {
            Console.WriteLine();
            ConsoleLabel.WriteLine("Session:", "resuming previous");
            ConsoleLabel.WriteContinuation("If not found, run: wl launch --new");
        }

        Console.WriteLine();

        // Only replace pointers after Copilot exits successfully.
        var exitCode = launcher.Launch(ws, args);
        if (exitCode == 0)
        {
            if (newSessionId is not null && !temporary)
            {
                LaunchService.SaveLastSession(ws, newSessionId);
            }
        }
        return exitCode;
    }

    private int ExecuteFolderMode(bool forceNew, bool temporary, IReadOnlyList<string>? passThroughArgs)
    {
        var folderPath = Directory.GetCurrentDirectory();
        var resumeSessionId = forceNew || temporary ? null : launcher.LoadFolderSession(folderPath);
        var shouldResume = resumeSessionId is not null;

        setup.EnsureInstalled();
        var sharedDirResolved = workspaces.GetSharedDirIfExists();
        var (args, newSessionId) = launcher.BuildFolderLaunchArgs(folderPath, resumeSessionId, sharedDirResolved, temporary, passThroughArgs);

        Console.WriteLine();
        ConsoleLabel.WriteLine("Launching:", Path.GetFileName(folderPath));
        ConsoleLabel.WriteLine("Repo:", folderPath);
        if (shouldResume)
        {
            Console.WriteLine();
            ConsoleLabel.WriteLine("Session:", "resuming previous");
            ConsoleLabel.WriteContinuation("If not found, run: wl launch --new");
        }

        Console.WriteLine();

        var exitCode = launcher.LaunchFolder(folderPath, args);
        if (exitCode == 0 && newSessionId is not null && !temporary)
        {
            launcher.SaveFolderSession(folderPath, newSessionId);
        }
        return exitCode;
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
}