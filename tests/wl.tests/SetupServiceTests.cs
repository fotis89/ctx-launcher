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
    public void MergeGitignore_FreshDefaultGitignore_UpgradeAddsPatternUnderHeader()
    {
        // First-run scenario: SetupService writes DefaultGitignore (which
        // includes the "# Added by `wl setup`" sentinel at the end). A
        // future wl version adds a new required pattern; on the next
        // setup run, MergeGitignore must insert it under the existing
        // sentinel — not append a second managed block at EOF.
        // Mimics the shape of the real DefaultGitignore: a few well-
        // known patterns, then the sentinel header.
        var defaultIsh =
            ".last-session\n" +
            ".last\n" +
            "\n" +
            "*/AGENTS.md\n" +
            "\n" +
            "# Added by `wl setup`";

        var result = SetupService.MergeGitignore(defaultIsh, [".future-pattern"]);

        var headerCount = result.Split("# Added by `wl setup`").Length - 1;
        Assert.Equal(1, headerCount);

        // The new pattern lands AFTER the sentinel, not before any of
        // the existing wl-managed patterns and not as a fresh block.
        var sentinelIdx = result.IndexOf("# Added by `wl setup`", StringComparison.Ordinal);
        var futureIdx = result.IndexOf(".future-pattern", StringComparison.Ordinal);
        var agentsIdx = result.IndexOf("*/AGENTS.md", StringComparison.Ordinal);
        Assert.True(sentinelIdx > 0 && futureIdx > 0 && agentsIdx > 0);
        Assert.True(agentsIdx < sentinelIdx, "existing wl patterns stay in place");
        Assert.True(sentinelIdx < futureIdx, "new pattern follows the sentinel");
    }

    [Fact]
    public void MergeGitignore_UserContentAfterManagedBlock_InsertsIntoBlockNotAtEof()
    {
        // ~/.wl-workspaces/.gitignore is the user's own file; they may
        // legitimately add their own patterns after wl's managed block.
        // The next merge must insert new wl patterns INTO the existing
        // managed block — not append a second header at EOF that splits
        // wl's patterns across two blocks.
        var existing =
            "# Added by `wl setup`\n" +
            ".last-session\n" +
            "\n" +
            "# my custom stuff\n" +
            "*.bak";

        var result = SetupService.MergeGitignore(existing, [".config.json"]);

        // Exactly one header — the existing one was reused.
        var headerCount = result.Split("# Added by `wl setup`").Length - 1;
        Assert.Equal(1, headerCount);

        // The new pattern landed inside wl's block (above the user's
        // content), not after "*.bak".
        var configIdx = result.IndexOf(".config.json", StringComparison.Ordinal);
        var customIdx = result.IndexOf("# my custom stuff", StringComparison.Ordinal);
        var bakIdx = result.IndexOf("*.bak", StringComparison.Ordinal);
        Assert.True(configIdx > 0 && customIdx > 0 && bakIdx > 0);
        Assert.True(configIdx < customIdx, "new wl pattern should come before user content");
        Assert.True(customIdx < bakIdx, "user content should be preserved in original order");
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
