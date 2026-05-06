using wl.Models;

namespace wl.Services;

public interface IToolAdapter
{
    string ExecutableName { get; }
    string DisplayName { get; }

    AdapterArgs BuildArgs(AdapterLaunchSpec spec);
}

public record AdapterLaunchSpec(
    Workspace Workspace,
    List<string> ResolvedAdditionalDirs,
    string? ResolvedSharedDir,
    string? Prompt,
    bool Yolo,
    string? ResumeSessionId);

public record AdapterArgs(
    List<string> Args,
    string? NewSessionId);
