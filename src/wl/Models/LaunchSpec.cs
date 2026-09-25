namespace wl.Models;

public record LaunchSpec(
    Workspace Workspace,
    List<string> ResolvedAdditionalDirs,
    string? ResolvedSharedDir,
    string? Prompt,
    bool Yolo,
    string? ResumeSessionId,
    bool TemporarySession = false)
{
    // PrimaryRepo is the cwd, not an --add-dir.
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

public record LaunchArgs(
    List<string> Args,
    string? NewSessionId);