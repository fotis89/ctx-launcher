using wl.Services;

namespace wl.tests;

public class SetupServiceTests
{
    [Fact]
    public void MergeGitignore_FreshBlock_AppendsHeaderAndPatterns()
    {
        var existing = """
            # User stuff
            .DS_Store
            *.log
            """;

        var result = SetupService.MergeGitignore(existing, ["*/AGENTS.md", ".config.json"]);

        Assert.Contains("# User stuff", result);
        Assert.Contains(".DS_Store", result);
        Assert.Contains("# Added by `wl setup`", result);
        Assert.Contains("*/AGENTS.md", result);
        Assert.Contains(".config.json", result);
        // exactly one header
        var headerCount = result.Split("# Added by `wl setup`").Length - 1;
        Assert.Equal(1, headerCount);
    }

    [Fact]
    public void MergeGitignore_ExistingManagedBlock_AppendsUnderSameHeader()
    {
        var firstPass = SetupService.MergeGitignore(
            "# User stuff\n.DS_Store",
            ["*/AGENTS.md"]);

        var secondPass = SetupService.MergeGitignore(firstPass, [".config.json"]);

        // Still exactly one "Added by wl setup" header after two migration rounds.
        var headerCount = secondPass.Split("# Added by `wl setup`").Length - 1;
        Assert.Equal(1, headerCount);

        // Both patterns are present.
        Assert.Contains("*/AGENTS.md", secondPass);
        Assert.Contains(".config.json", secondPass);
    }

    [Fact]
    public void MergeGitignore_WhitespaceOnlyBlankLine_DoesNotDuplicateHeader()
    {
        // Hand-edited .gitignore files often have stray whitespace on
        // visually-blank lines. The block-boundary scan must treat those
        // as separators, otherwise we'd append a second "Added by wl
        // setup" header instead of merging into the existing block.
        var firstPass = SetupService.MergeGitignore(
            "# User stuff\n.DS_Store",
            ["*/AGENTS.md"]);

        // Inject whitespace into the blank line between user content and
        // our managed block before merging again.
        var withWhitespace = firstPass.Replace("\n\n#", "\n   \n#");

        var secondPass = SetupService.MergeGitignore(withWhitespace, [".config.json"]);

        var headerCount = secondPass.Split("# Added by `wl setup`").Length - 1;
        Assert.Equal(1, headerCount);
        Assert.Contains(".config.json", secondPass);
    }

    [Fact]
    public void MergeGitignore_HandlesCrlfLineEndings()
    {
        var existing = "# User stuff\r\n.DS_Store\r\n";

        var result = SetupService.MergeGitignore(existing, [".config.json"]);

        Assert.Contains("# Added by `wl setup`", result);
        Assert.Contains(".config.json", result);
    }

    [Fact]
    public void MergeGitignore_PreservesUserContent()
    {
        var existing = """
            # User stuff
            .DS_Store
            secret.env
            """;

        var result = SetupService.MergeGitignore(existing, [".config.json"]);

        // User's lines remain intact and in order.
        var userIdx = result.IndexOf(".DS_Store", StringComparison.Ordinal);
        var secretIdx = result.IndexOf("secret.env", StringComparison.Ordinal);
        var configIdx = result.IndexOf(".config.json", StringComparison.Ordinal);
        Assert.True(userIdx >= 0);
        Assert.True(secretIdx > userIdx);
        Assert.True(configIdx > secretIdx);
    }

    [Fact]
    public void StripStaleCopilotSkillDirs_RemovesEntriesUnderWorkspacesRoot()
    {
        var temp = Path.Combine(Path.GetTempPath(), "wl-strip-" + Guid.NewGuid().ToString("N")[..8]);
        var copilotDir = Path.Combine(temp, ".copilot");
        var workspacesRoot = Path.Combine(temp, ".wl-workspaces");
        Directory.CreateDirectory(copilotDir);
        try
        {
            var settings = Path.Combine(copilotDir, "settings.json");
            var inside = Path.Combine(workspacesRoot, "ws-a", ".claude", "skills").Replace("\\", "\\\\");
            var outside = (temp + Path.DirectorySeparatorChar + "other-tool" + Path.DirectorySeparatorChar + "skills").Replace("\\", "\\\\");
            File.WriteAllText(settings, $$"""
                {"model":"x","skillDirectories":["{{inside}}","{{outside}}"]}
                """);

            SetupService.StripStaleCopilotSkillDirs(temp, workspacesRoot);

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settings))!.AsObject();
            var dirs = json["skillDirectories"]!.AsArray().Select(d => d!.GetValue<string>()).ToList();
            Assert.Single(dirs);
            Assert.Equal("x", json["model"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void StripStaleCopilotSkillDirs_PrefixIsBoundedToDirectorySeparator()
    {
        // Sibling dirs that share a prefix (e.g. .wl-workspaces vs
        // .wl-workspaces-backup) must not be matched.
        var temp = Path.Combine(Path.GetTempPath(), "wl-strip-" + Guid.NewGuid().ToString("N")[..8]);
        var copilotDir = Path.Combine(temp, ".copilot");
        var workspacesRoot = Path.Combine(temp, ".wl-workspaces");
        Directory.CreateDirectory(copilotDir);
        try
        {
            var settings = Path.Combine(copilotDir, "settings.json");
            var sibling = Path.Combine(temp, ".wl-workspaces-backup", "ws", ".claude", "skills").Replace("\\", "\\\\");
            File.WriteAllText(settings, $$"""
                {"skillDirectories":["{{sibling}}"]}
                """);

            SetupService.StripStaleCopilotSkillDirs(temp, workspacesRoot);

            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settings))!.AsObject();
            var dirs = json["skillDirectories"]!.AsArray().Select(d => d!.GetValue<string>()).ToList();
            Assert.Single(dirs);
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void StripStaleCopilotSkillDirs_NoSettingsFile_NoError()
    {
        var temp = Path.Combine(Path.GetTempPath(), "wl-strip-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // No ~/.copilot dir, no settings.json — must not throw.
            SetupService.StripStaleCopilotSkillDirs(temp, Path.Combine(temp, ".wl-workspaces"));
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
