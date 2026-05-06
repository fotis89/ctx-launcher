using wl.Models;

namespace wl.Services;

public class CopilotAdapter : IToolAdapter
{
    public string ExecutableName => "copilot";
    public string DisplayName => "copilot";

    public void PrepareLaunch(Workspace ws)
    {
        // Copilot auto-discovers AGENTS.md (and related files like
        // .github/copilot-instructions.md) per its --no-custom-instructions
        // help text. The workspace folder isn't cwd or git root, so set
        // COPILOT_CUSTOM_INSTRUCTIONS_DIRS in GetEnvironment() to make sure
        // it's searched. Mirror instructions.md → AGENTS.md here.
        if (!File.Exists(ws.InstructionsPath))
        {
            return;
        }

        var agentsPath = Path.Combine(ws.FolderPath, "AGENTS.md");
        File.Copy(ws.InstructionsPath, agentsPath, overwrite: true);
    }

    public IReadOnlyDictionary<string, string> GetEnvironment(Workspace ws)
    {
        // Tell Copilot to also search the workspace folder for AGENTS.md
        // (default search is cwd + git root only).
        return new Dictionary<string, string>
        {
            ["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"] = ws.FolderPath,
        };
    }

    public AdapterArgs BuildArgs(AdapterLaunchSpec spec)
    {
        var args = new List<string>();
        string? newSessionId = null;
        var ws = spec.Workspace;

        // Copilot's --resume=<uuid> is idempotent: starts a new session if
        // the UUID doesn't exist, resumes if it does. Always emit it so wl
        // can track session IDs the same way it does for Claude.
        if (spec.ResumeSessionId is not null)
        {
            args.Add($"--resume={spec.ResumeSessionId}");
        }
        else
        {
            newSessionId = Guid.NewGuid().ToString();
            args.Add($"--resume={newSessionId}");
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

        // instructions.md is mirrored to AGENTS.md in PrepareLaunch and
        // discovered by Copilot via the workspace folder's --add-dir entry.

        if (spec.Yolo)
        {
            args.Add("--yolo");
        }

        if (!string.IsNullOrEmpty(spec.Prompt))
        {
            args.Add("-i");
            args.Add(spec.Prompt);
        }

        return new AdapterArgs(args, newSessionId);
    }
}
