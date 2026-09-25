using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class LaunchCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher, SetupService setup)
{
    public int Execute(string? name, string? promptArg, bool yolo = false, bool forceNew = false, bool temporary = false)
    {
        if (name is null)
        {
            name = workspaces.GetLastUsed();
            if (name is null)
            {
                Console.Error.WriteLine("No workspace specified and no last-used workspace found.");
                Console.Error.WriteLine("Run: wl launch <name>");
                return 1;
            }
        }

        var ws = workspaces.LoadWorkspace(name);
        if (ws is null)
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            Console.Error.WriteLine("Run 'wl list' to see available workspaces.");
            return 1;
        }

        var repoExists = Directory.Exists(launcher.ResolveWorkspacePath(ws, ws.PrimaryRepo));
        if (!repoExists)
        {
            Console.Error.WriteLine($"Error: primary repo not found: {ws.PrimaryRepo}");
            return 1;
        }

        string? resolvedPrompt = null;
        if (promptArg is not null)
        {
            resolvedPrompt = prompts.ResolvePrompt(ws, promptArg);
        }

        if (forceNew && temporary)
        {
            Console.Error.WriteLine("Cannot use --new and --temp together.");
            return 1;
        }

        var skipPermissions = yolo || ws.Yolo;
        var resumeSessionId = forceNew || temporary ? null : LaunchService.LoadLastSession(ws);
        var shouldResume = resumeSessionId is not null;

        setup.EnsureInstalled();
        var sharedDirResolved = workspaces.GetSharedDirIfExists();
        var (args, skippedDirs, newSessionId) = launcher.BuildLaunchArgs(ws, resolvedPrompt, skipPermissions, resumeSessionId, sharedDirResolved, temporary);

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

        if (resolvedPrompt is not null)
        {
            var truncated = resolvedPrompt.Length > 60 ? resolvedPrompt[..57] + "..." : resolvedPrompt;
            ConsoleLabel.WriteLine("Prompt:", truncated);
        }

        if (skipPermissions || shouldResume)
        {
            Console.WriteLine();
            if (skipPermissions)
            {
                ConsoleLabel.WriteLine("Permissions:", "yolo");
            }
            if (shouldResume)
            {
                ConsoleLabel.WriteLine("Session:", "resuming previous");
                ConsoleLabel.WriteContinuation("If not found, run: wl launch --new");
            }
        }

        Console.WriteLine();

        // Only replace pointers after Copilot exits successfully.
        var exitCode = launcher.Launch(ws, args);
        if (exitCode == 0)
        {
            workspaces.SetLastUsed(name);
            if (newSessionId is not null && !temporary)
            {
                LaunchService.SaveLastSession(ws, newSessionId);
            }
        }
        return exitCode;
    }
}