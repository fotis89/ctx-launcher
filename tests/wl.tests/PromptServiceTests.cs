using wl.Services;

namespace wl.tests;

public class PromptServiceTests
{
    [Fact]
    public void ParsePromptFile_ValidFrontmatter_ExtractsLabelAndBody()
    {
        var content = """
            ---
            label: Review changes
            ---
            Review the latest changes.
            """;
        var result = PromptService.ParsePromptFile(content, "review-changes");
        Assert.Equal("Review changes", result.Label);
        Assert.Equal("Review the latest changes.", result.Body);
        Assert.Equal("review-changes", result.Slug);
    }

    [Fact]
    public void ParsePromptFile_NoFrontmatter_WholeContentIsBody()
    {
        var content = "Just a raw prompt text.";
        var result = PromptService.ParsePromptFile(content, "my-prompt");
        Assert.Equal("my-prompt", result.Label);
        Assert.Equal("Just a raw prompt text.", result.Body);
    }

    [Fact]
    public void ParsePromptFile_EmptyContent_ReturnsEmptyBody()
    {
        var result = PromptService.ParsePromptFile("", "empty");
        Assert.Equal("empty", result.Label);
        Assert.Equal("", result.Body);
    }

    [Fact]
    public void ParsePromptFile_FrontmatterOnly_EmptyBody()
    {
        var content = """
            ---
            label: Test
            ---
            """;
        var result = PromptService.ParsePromptFile(content, "test");
        Assert.Equal("Test", result.Label);
        Assert.Equal("", result.Body);
    }

    [Fact]
    public void ParsePromptFile_MultipleFrontmatterFields_OnlyLabelExtracted()
    {
        var content = """
            ---
            label: My Label
            author: someone
            ---
            The body text.
            """;
        var result = PromptService.ParsePromptFile(content, "test");
        Assert.Equal("My Label", result.Label);
        Assert.Equal("The body text.", result.Body);
    }

    [Fact]
    public void ParsePromptFile_MalformedFrontmatter_NoClosingDashes()
    {
        var content = """
            ---
            label: Broken
            This never closes.
            """;
        var result = PromptService.ParsePromptFile(content, "broken");
        Assert.Equal("broken", result.Label);
        Assert.Contains("label: Broken", result.Body);
    }

    [Fact]
    public void ParsePromptFile_MultilineBody_Preserved()
    {
        var content = """
            ---
            label: Multi
            ---
            Line one.
            Line two.
            Line three.
            """;
        var result = PromptService.ParsePromptFile(content, "multi");
        Assert.Equal("Multi", result.Label);
        Assert.Contains("Line one.", result.Body);
        Assert.Contains("Line three.", result.Body);
    }
}