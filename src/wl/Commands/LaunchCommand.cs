using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class LaunchCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher, SetupService setup, PathsService paths)
{
    public int Execute(string? name, string? promptArg, bool yolo = false, bool resume = false, bool forceNew = false)
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

        var repoExists = Directory.Exists(PathHelper.ResolvePath(ws.PrimaryRepo, paths.Get));
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

        if (forceNew && resume)
        {
            Console.Error.WriteLine("Cannot use --new and --resume together.");
            return 1;
        }

        var skipPermissions = yolo || ws.Yolo;
        var shouldResume = !forceNew && (resume || ws.Resume);

        string? resumeSessionId = null;
        if (shouldResume)
        {
            resumeSessionId = LaunchService.LoadLastSession(ws);
            if (resumeSessionId is null)
            {
                if (resume)
                {
                    Console.Error.WriteLine("No previous session found for this workspace.");
                    Console.Error.WriteLine("Run without --resume to start a new session.");
                    return 1;
                }

                shouldResume = false;
            }
        }

        setup.EnsureInstalled();
        var sharedDirResolved = workspaces.GetSharedDirIfExists();
        var (args, skippedDirs, newSessionId) = launcher.BuildLaunchArgs(ws, resolvedPrompt, skipPermissions, resumeSessionId, sharedDirResolved);

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

        if (skipPermissions || shouldResume || (ws.Resume && resumeSessionId is null))
        {
            Console.WriteLine();
            if (skipPermissions)
            {
                ConsoleLabel.WriteLine("Permissions:", "yolo");
            }
            if (shouldResume)
            {
                var note = ws.Resume ? "resuming previous (auto)" : "resuming previous";
                ConsoleLabel.WriteLine("Session:", note);
                ConsoleLabel.WriteContinuation("If not found, run: wl launch --new");
            }
            else if (ws.Resume)
            {
                ConsoleLabel.WriteLine("Session:", "new (no previous to resume)");
            }
        }

        Console.WriteLine();

        // Only replace pointers after Copilot exits successfully.
        var exitCode = launcher.Launch(ws, args);
        if (exitCode == 0)
        {
            workspaces.SetLastUsed(name);
            if (newSessionId is not null)
            {
                LaunchService.SaveLastSession(ws, newSessionId);
            }
        }
        return exitCode;
    }
}