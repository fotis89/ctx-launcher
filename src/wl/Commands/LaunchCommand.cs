using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class LaunchCommand(WorkspaceService workspaces, PromptService prompts, LaunchService launcher, VersionService versionService)
{
    public void Execute(string? name, string? promptArg, bool yolo = false, bool resume = false, bool forceNew = false)
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

        var sharedDirResolved = workspaces.GetSharedDirIfExists();

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
                    return;
                }

                shouldResume = false;
            }
        }

        var (args, skippedDirs, newSessionId) = launcher.BuildClaudeArgs(ws, resolvedPrompt, skipPermissions, resumeSessionId, sharedDirResolved);

        foreach (var dir in skippedDirs)
        {
            Console.Error.WriteLine($"  Warning: directory not found: {dir} (skipping)");
        }

        var instructionLines = File.Exists(ws.InstructionsPath)
            ? File.ReadLines(ws.InstructionsPath).Count() : 0;

        var skillNames = WorkspaceService.ListSkillNames(ws.SkillsPath);
        if (sharedDirResolved is not null)
        {
            skillNames.AddRange(WorkspaceService.ListSkillNames(workspaces.GetSharedSkillsPath()));
        }

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

        if (shouldResume || skipPermissions || (ws.Resume && resumeSessionId is null))
        {
            Console.WriteLine();
            if (skipPermissions)
            {
                Console.WriteLine("  Bypassing permissions");
            }

            if (shouldResume)
            {
                Console.WriteLine(ws.Resume ? "  Resuming session (auto)" : "  Resuming session");
                Console.WriteLine("  If session not found, run: wl launch --new");
            }
            else if (ws.Resume)
            {
                Console.WriteLine("  New session (no previous session to resume)");
            }
        }

        var installedVersion = versionService.GetInstalledVersion();
        var currentVersion = versionService.GetCurrentVersion();
        if (installedVersion is null)
        {
            Console.Error.WriteLine("  Hint: run 'wl setup' to install wl skills.");
        }
        else if (installedVersion != currentVersion)
        {
            Console.Error.WriteLine($"  Hint: run 'wl setup' to update skills ({installedVersion} → {currentVersion}).");
        }

        Console.WriteLine();
        workspaces.SetLastUsed(name);

        if (newSessionId is not null)
        {
            LaunchService.SaveLastSession(ws, newSessionId);
        }

        launcher.Launch(ws, args);
    }
}
