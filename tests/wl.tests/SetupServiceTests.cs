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
}
