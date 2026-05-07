using wl.Models;

namespace wl.Services;

public interface IToolAdapter
{
    string ExecutableName { get; }
    string DisplayName { get; }

    /// <summary>
    /// Whether this adapter's tool routes built-in slash commands the way
    /// Claude does. Used by user-facing displays that show skill names
    /// (Claude lists skills as `/wl-create-workspace`; in Copilot, slash
    /// is reserved for built-ins like `/init`, `/skills`, so skills are
    /// shown bare and triggered by description match instead).
    /// </summary>
    bool SkillsAreSlashInvokable { get; }

    AdapterArgs BuildArgs(AdapterLaunchSpec spec);

    void PrepareLaunch(Workspace ws);

    IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws);

    /// <summary>
    /// Spawn the tool with a one-shot prompt that triggers the named
    /// shared skill. Each adapter formats the invocation for its tool —
    /// Claude takes `/skill-name [arg]`; Copilot needs natural-language
    /// description-match phrasing.
    /// </summary>
    void InvokeCreateSkill(string skillName, string? workspaceName, string cwd, string sharedDir, ClaudeRunner runner);
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
