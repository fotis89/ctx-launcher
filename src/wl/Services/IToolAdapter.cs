using wl.Models;

namespace wl.Services;

public interface IToolAdapter
{
    string ExecutableName { get; }
    string DisplayName { get; }

    /// <summary>
    /// Whether this tool routes built-in slash commands like Claude.
    /// `wl which` uses this to render skill names as `/skill` (Claude)
    /// vs. bare `skill` (Copilot, which reserves `/` for built-ins).
    /// </summary>
    bool SkillsAreSlashInvokable { get; }

    AdapterArgs BuildArgs(AdapterLaunchSpec spec);

    void PrepareLaunch(Workspace ws);

    IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws);

    /// <summary>
    /// Human-readable description of files this adapter will write or
    /// modify during PrepareLaunch (AGENTS.md mirror, plugin.json
    /// manifests). Used by `wl which`. Pure — no IO mutation. Empty
    /// for adapters that don't prep anything.
    /// </summary>
    IEnumerable<string> DescribeLaunchPrep(Workspace ws);

    /// <summary>
    /// Spawn the tool with a one-shot prompt that triggers the named
    /// shared skill. Adapters format the invocation per tool — Claude
    /// uses `/skill-name`; Copilot needs description-match phrasing.
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
    // Centralized so adapters don't drift when the set or order of
    // attached paths changes. PrimaryRepo is the cwd, not an --add-dir.
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
