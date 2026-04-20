using System.Reflection;

namespace wl.Services;

public record SetupResult(bool CreateWorkspaceFresh, bool UpdateWorkspaceFresh, string? PreviousVersion, string CurrentVersion);

public class SetupService(WorkspaceService workspaces, VersionService versionService)
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
        var skillFile = Path.Combine(skillDir, "SKILL.md");
        var existed = File.Exists(skillFile);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(skillFile, LoadResource(resourceName));
        return !existed;
    }

    public SetupResult RunSetup()
    {
        var previous = versionService.GetInstalledVersion();
        var current = versionService.GetCurrentVersion();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var createDir = Path.Combine(home, ".claude", "skills", CreateSkillName);
        var updateDir = Path.Combine(workspaces.GetSharedSkillsPath(), UpdateSkillName);

        var createFresh = WriteSkill(createDir, $"{CreateSkillName}.md");
        var updateFresh = WriteSkill(updateDir, $"{UpdateSkillName}.md");

        versionService.StampVersion();
        return new SetupResult(createFresh, updateFresh, previous, current);
    }

    public bool EnsureInstalled()
    {
        if (versionService.GetInstalledVersion() == versionService.GetCurrentVersion())
        {
            return false;
        }

        var result = RunSetup();
        Console.WriteLine();
        Console.WriteLine(result.PreviousVersion is null
            ? "  First run: installed Claude skills."
            : $"  Upgraded {result.PreviousVersion} → {result.CurrentVersion}: refreshed Claude skills.");
        return true;
    }
}
