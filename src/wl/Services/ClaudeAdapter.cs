using wl.Models;

namespace wl.Services;

public class ClaudeAdapter : IToolAdapter
{
    public string ExecutableName => "claude";
    public string DisplayName => "claude";

    public void PrepareLaunch(Workspace ws)
    {
        // Claude reads instructions.md directly via --append-system-prompt-file; no prep needed.
    }

    public IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws)
        => new Dictionary<string, string>();

    public AdapterArgs BuildArgs(AdapterLaunchSpec spec)
    {
        var args = new List<string>();
        string? newSessionId = null;
        var ws = spec.Workspace;

        if (spec.ResumeSessionId is not null)
        {
            args.Add("--resume");
            args.Add(spec.ResumeSessionId);
        }
        else
        {
            newSessionId = Guid.NewGuid().ToString();
            args.Add("--session-id");
            args.Add(newSessionId);
            args.Add("--name");
            args.Add(ws.Name);
        }

        foreach (var dir in spec.ResolvedAdditionalDirs)
        {
            args.Add("--add-dir");
            args.Add(dir);
        }

        if (spec.ResolvedSharedDir is not null)
        {
            args.Add("--add-dir");
            args.Add(spec.ResolvedSharedDir);
        }

        args.Add("--add-dir");
        args.Add(ws.FolderPath);

        if (File.Exists(ws.InstructionsPath))
        {
            args.Add("--append-system-prompt-file");
            args.Add(ws.InstructionsPath);
        }

        if (spec.Yolo)
        {
            args.Add("--dangerously-skip-permissions");
        }

        if (!string.IsNullOrEmpty(spec.Prompt))
        {
            args.Add(spec.Prompt);
        }

        return new AdapterArgs(args, newSessionId);
    }
}
