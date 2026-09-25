using wl.Helpers;
using wl.Services;

namespace wl.tests;

public class SetupServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n\t\n  ")]
    [InlineData("# User stuff\n.DS_Store\nsecret.env")]
    [InlineData("# User stuff\r\n.DS_Store\r\nsecret.env")]
    [InlineData(".last-session\n\n# Added by wl")]
    [InlineData("# Added by wl\n.last-session\n\n# my custom stuff\n*.bak")]
    public void MergeGitignore_PreservesContentAndUsesOneManagedBlock(string existing)
    {
        var first = SetupService.MergeGitignore(existing, ["*/.copilot/plugin.json"]);
        var result = SetupService.MergeGitignore(first, [".paths.json"]);
        Assert.Equal(1, result.Split("# Added by wl").Length - 1);
        Assert.Contains("*/.copilot/plugin.json", result);
        Assert.DoesNotContain("*/AGENTS.md", result);
        Assert.Contains(".paths.json", result);
        foreach (var line in existing.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            Assert.Contains(line, result);
        if (string.IsNullOrWhiteSpace(existing))
            Assert.StartsWith("# Added by wl", result);
        if (existing.Contains("*.bak"))
            Assert.True(result.IndexOf(".paths.json", StringComparison.Ordinal) < result.IndexOf("*.bak", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void MergeGitignore_PreservesNewlineStyle(string newline)
    {
        var result = SetupService.MergeGitignore("# User stuff" + newline + ".DS_Store" + newline, [".paths.json"]);
        Assert.Contains(newline, result);
        if (newline == "\r\n") Assert.DoesNotMatch(@"(?<!\r)\n", result);
        else Assert.DoesNotContain("\r\n", result);
    }

    [Fact]
    public void Setup_InstallsSkillsAndDoesNotRewriteSessions()
    {
        var root = Directory.CreateTempSubdirectory("wl-setup-").FullName;
        try
        {
            var paths = new WlPaths(root);
            var workspace = Directory.CreateDirectory(Path.Combine(root, "old")).FullName;
            var sessionPath = Path.Combine(workspace, ".last-session");
            const string session = "old-id";
            File.WriteAllText(sessionPath, session);
            var service = new SetupService(new VersionService(paths), paths);
            Assert.True(service.EnsureInstalled());
            Assert.False(service.EnsureInstalled());
            var name = "wl-workspace";
            Assert.True(File.Exists(WlPaths.SkillFile(paths.SharedSkillsDir, name)));
            using (var resource = typeof(SetupService).Assembly.GetManifestResourceStream($"wl.Resources.{name}.md")!)
            using (var reader = new StreamReader(resource))
            {
                var expected = reader.ReadToEnd();
                Assert.Equal(expected, File.ReadAllText(WlPaths.SkillFile(paths.SharedSkillsDir, name)));
                Assert.Contains(".copilot/skills", expected);
                Assert.Contains("wl-", expected);
                Assert.Contains("allowed-tools", expected);
                var frontmatter = expected.Split("---")[1];
                Assert.DoesNotContain("allowed-tools:", frontmatter);
                Assert.Contains("optional permission pre-approval", expected);
                Assert.Contains("wl which", expected);
            }
            Assert.Contains(".shared/.copilot/skills/wl-workspace/", File.ReadAllText(paths.GitignoreFile));
            Assert.DoesNotContain("*/AGENTS.md", File.ReadAllText(paths.GitignoreFile));
            Assert.Equal(session, File.ReadAllText(sessionPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Setup_RefreshesSkillsWhenInstalledVersionDiffers()
    {
        var root = Directory.CreateTempSubdirectory("wl-setup-").FullName;
        try
        {
            var paths = new WlPaths(root);
            var version = new VersionService(paths);
            var service = new SetupService(version, paths);
            service.RunSetup();
            File.WriteAllText(paths.VersionFile, "0.0.0");
            var skill = WlPaths.SkillFile(paths.SharedSkillsDir, "wl-workspace");
            File.WriteAllText(skill, "stale content");
            Assert.True(service.EnsureInstalled());
            Assert.Equal(version.GetCurrentVersion(), File.ReadAllText(paths.VersionFile));
            Assert.NotEqual("stale content", File.ReadAllText(skill));
        }
        finally { Directory.Delete(root, true); }
    }
}