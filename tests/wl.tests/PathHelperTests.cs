using wl.Helpers;

namespace wl.tests;

public class PathHelperTests
{
    private readonly string _home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [Fact]
    public void ResolveTilde_WithTilde_ReplacesWithHome()
    {
        var result = PathHelper.ResolveTilde("~/foo/bar");
        Assert.Equal(Path.Combine(_home, "foo", "bar"), result);
    }

    [Fact]
    public void ResolveTilde_WithoutTilde_ReturnsUnchanged()
    {
        var absPath = Path.Combine(Path.GetTempPath(), "some-project");
        var result = PathHelper.ResolveTilde(absPath);
        Assert.Equal(absPath, result);
    }

    [Fact]
    public void ResolveTilde_TildeOnly_ReturnsHome()
    {
        var result = PathHelper.ResolveTilde("~");
        Assert.Equal(_home, result);
    }

    [Fact]
    public void QuotePath_WithSpaces_Quotes()
    {
        var result = PathHelper.QuotePath(@"C:\My Projects\repo");
        Assert.Equal("\"C:\\My Projects\\repo\"", result);
    }

    [Fact]
    public void QuotePath_WithoutSpaces_NoChange()
    {
        var result = PathHelper.QuotePath(@"C:\repos\project");
        Assert.Equal(@"C:\repos\project", result);
    }

    [Fact]
    public void ValidatePath_ExistingPath_ReturnsTrue()
    {
        var (exists, resolved) = PathHelper.ValidatePath(_home);
        Assert.True(exists);
        Assert.Equal(_home, resolved);
    }

    [Fact]
    public void ValidatePath_MissingPath_ReturnsFalse()
    {
        var (exists, _) = PathHelper.ValidatePath(@"C:\this\does\not\exist\at\all");
        Assert.False(exists);
    }

    [Fact]
    public void ValidatePath_TildePath_Resolves()
    {
        var (exists, resolved) = PathHelper.ValidatePath("~");
        Assert.True(exists);
        Assert.Equal(_home, resolved);
    }

    [Fact]
    public void Slugify_SimpleString_Lowercases()
    {
        Assert.Equal("my-api", PathHelper.Slugify("My-API"));
    }

    [Fact]
    public void Slugify_SpacesAndSlashes_ReplacesWithHyphens()
    {
        Assert.Equal("my-api-docs", PathHelper.Slugify("My API / Docs"));
    }

    [Fact]
    public void Slugify_SpecialChars_Stripped()
    {
        Assert.Equal("my-project-v2", PathHelper.Slugify("My Project! (v2)"));
    }

    [Fact]
    public void Slugify_MultipleDashes_Collapsed()
    {
        Assert.Equal("a-b", PathHelper.Slugify("a---b"));
    }

    [Fact]
    public void Slugify_LeadingTrailingDashes_Trimmed()
    {
        Assert.Equal("test", PathHelper.Slugify("--test--"));
    }
}