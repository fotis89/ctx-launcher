using System.Text;

using wl.Models;

namespace wl.Services;

public class CopilotAdapter : IToolAdapter
{
    public string ExecutableName => "copilot";
    public string DisplayName => "copilot";

    public void PrepareLaunch(Workspace ws)
    {
        // Copilot auto-discovers AGENTS.md (and related files) per its
        // --no-custom-instructions help text. The workspace folder isn't
        // cwd or git root, so set COPILOT_CUSTOM_INSTRUCTIONS_DIRS in
        // GetEnvironment() to make sure it's searched.
        //
        // Build AGENTS.md from instructions.md + .claude/skills/* +
        // shared .claude/skills/*. Copilot doesn't have a slash-command
        // skills bridge, but it reads AGENTS.md as system context — so
        // skill content becomes "available workflows" Copilot can pattern-
        // match against natural-language user requests.
        var sb = new StringBuilder();

        if (File.Exists(ws.InstructionsPath))
        {
            sb.AppendLine(File.ReadAllText(ws.InstructionsPath).Trim());
            sb.AppendLine();
        }

        AppendSkills(sb, ws.SkillsPath);

        var workspacesRoot = Path.GetDirectoryName(ws.FolderPath);
        if (workspacesRoot is not null)
        {
            var sharedSkillsPath = Path.Combine(workspacesRoot, WorkspaceService.SharedDirName, ".claude", "skills");
            AppendSkills(sb, sharedSkillsPath);
        }

        var agentsPath = Path.Combine(ws.FolderPath, "AGENTS.md");
        if (sb.Length == 0)
        {
            // Nothing to write — clean up any stale AGENTS.md so a workspace
            // that loses all its content doesn't keep an orphan.
            if (File.Exists(agentsPath))
            {
                File.Delete(agentsPath);
            }
            return;
        }

        File.WriteAllText(agentsPath, sb.ToString().TrimEnd() + "\n");
    }

    private static void AppendSkills(StringBuilder sb, string skillsDir)
    {
        if (!Directory.Exists(skillsDir))
        {
            return;
        }

        foreach (var skillDir in Directory.GetDirectories(skillsDir))
        {
            var skillFile = Path.Combine(skillDir, "SKILL.md");
            if (!File.Exists(skillFile))
            {
                continue;
            }

            var name = Path.GetFileName(skillDir);
            var (description, body) = ParseSkill(File.ReadAllText(skillFile));

            sb.AppendLine($"## /{name}");
            if (!string.IsNullOrEmpty(description))
            {
                sb.AppendLine();
                sb.AppendLine($"_{description}_");
            }
            sb.AppendLine();
            sb.AppendLine(body.Trim());
            sb.AppendLine();
        }
    }

    private static (string? Description, string Body) ParseSkill(string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---", StringComparison.Ordinal))
        {
            return (null, normalized);
        }

        var lines = normalized.Split('\n');
        var closingIdx = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == "---")
            {
                closingIdx = i;
                break;
            }
        }

        if (closingIdx == -1)
        {
            return (null, normalized);
        }

        string? description = null;
        for (var i = 1; i < closingIdx; i++)
        {
            var line = lines[i];
            if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                description = line["description:".Length..].Trim().Trim('"').Trim('\'');
                break;
            }
        }

        var body = string.Join('\n', lines.Skip(closingIdx + 1)).TrimStart();
        return (description, body);
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
