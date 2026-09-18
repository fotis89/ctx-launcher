using System.Reflection;

using wl.Helpers;

namespace wl.Services;

public record SetupResult(bool CreateWorkspaceFresh, bool UpdateWorkspaceFresh, string? PreviousVersion, string CurrentVersion);

public class SetupService(VersionService versionService, WlPaths paths)
{
    private const string CreateSkillName = "wl-create-workspace";
    private const string UpdateSkillName = "wl-update-workspace";

    private static string LoadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"wl.Resources.{name}")
            ?? throw new InvalidOperationException($"Missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool WriteSkill(string skillDir, string resourceName)
    {
        var skillFile = Path.Combine(skillDir, WlPaths.SkillFileName);
        var existed = File.Exists(skillFile);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(skillFile, LoadResource(resourceName));
        return !existed;
    }

    private const string DefaultGitignore =
        """
        # Machine-local state
        .last-session
        .last
        .version
        .paths.json
        .config.json

        # wl-managed skills
        .shared/.copilot/skills/wl-create-workspace/
        .shared/.copilot/skills/wl-update-workspace/

        # Generated instructions and plugin manifests
        */AGENTS.md
        */.copilot/plugin.json
        .shared/.copilot/plugin.json

        # Added by `wl setup`
        """;

    public SetupResult RunSetup()
    {
        new WorkspaceService(paths).ValidateEnvironment();
        var previous = versionService.GetInstalledVersion();
        var current = versionService.GetCurrentVersion();
        var createFresh = WriteSkill(WlPaths.Skill(paths.SharedSkillsDir, CreateSkillName), $"{CreateSkillName}.md");
        var updateFresh = WriteSkill(WlPaths.Skill(paths.SharedSkillsDir, UpdateSkillName), $"{UpdateSkillName}.md");
        EnsureGitignore();
        versionService.StampVersion();
        return new SetupResult(createFresh, updateFresh, previous, current);
    }

    private void EnsureGitignore()
    {
        if (!File.Exists(paths.GitignoreFile))
        {
            File.WriteAllText(paths.GitignoreFile, DefaultGitignore);
            return;
        }

        var existing = File.ReadAllText(paths.GitignoreFile);
        var existingPatterns = existing.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
        var missing = DefaultGitignore.Split('\n').Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !existingPatterns.Contains(l)).ToList();
        if (missing.Count > 0)
            File.WriteAllText(paths.GitignoreFile, MergeGitignore(existing, missing));
    }

    private const string AddedByHeader = "# Added by `wl setup`";

    public static string MergeGitignore(string existing, IEnumerable<string> missing)
    {
        var newline = DetectNewline(existing);
        var trimmed = string.IsNullOrWhiteSpace(existing) ? "" : existing.TrimEnd();
        var lines = trimmed.Length == 0
            ? new List<string>()
            : trimmed.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var headerIndex = lines.FindIndex(l => l.Trim() == AddedByHeader);
        if (headerIndex < 0)
        {
            if (lines.Count > 0) lines.Add("");
            lines.Add(AddedByHeader);
            lines.AddRange(missing);
        }
        else
        {
            var insertAt = headerIndex + 1;
            while (insertAt < lines.Count && !string.IsNullOrWhiteSpace(lines[insertAt])) insertAt++;
            lines.InsertRange(insertAt, missing);
        }
        return string.Join(newline, lines) + newline;
    }

    public static string DetectNewline(string content)
    {
        if (content.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (content.Contains('\n')) return "\n";
        return Environment.NewLine;
    }

    public bool EnsureInstalled()
    {
        new WorkspaceService(paths).ValidateEnvironment();
        var skillsPresent =
            File.Exists(WlPaths.SkillFile(paths.SharedSkillsDir, CreateSkillName)) &&
            File.Exists(WlPaths.SkillFile(paths.SharedSkillsDir, UpdateSkillName));
        if (skillsPresent && versionService.GetInstalledVersion() == versionService.GetCurrentVersion())
            return false;

        var result = RunSetup();
        Console.WriteLine();
        Console.WriteLine(result.PreviousVersion is null
            ? "  First run: installed wl skills."
            : $"  Upgraded {result.PreviousVersion} -> {result.CurrentVersion}: refreshed wl skills.");
        return true;
    }
}