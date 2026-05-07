using wl.Models;

namespace wl.Services;

public interface IToolAdapter
{
    string ExecutableName { get; }
    string DisplayName { get; }

    AdapterArgs BuildArgs(AdapterLaunchSpec spec);

    void PrepareLaunch(Workspace ws);

    IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws);

    void InvokeCreateSkill(string prompt, string cwd, string sharedDir, ClaudeRunner runner);
}

public record AdapterLaunchSpec(
    Workspace Workspace,
    List<string> ResolvedAdditionalDirs,
    string? ResolvedSharedDir,
    string? Prompt,
    bool Yolo,
    string? ResumeSessionId)
{
    // Both Claude and Copilot use --add-dir for additional dirs, shared dir,
    // and the workspace folder. The primary repo is not added here via
    // --add-dir; it is provided as the process working directory. Centralized
    // here so adapters don't drift when the order or set of attached paths changes.
    public void AppendAddDirArgs(List<string> args)
    {
        foreach (var dir in ResolvedAdditionalDirs)
        {
            args.Add("--add-dir");
            args.Add(dir);
        }
        if (ResolvedSharedDir is not null)
        {
            args.Add("--add-dir");
            args.Add(ResolvedSharedDir);
        }
        args.Add("--add-dir");
        args.Add(Workspace.FolderPath);
    }
}

public record AdapterArgs(
    List<string> Args,
    string? NewSessionId);
