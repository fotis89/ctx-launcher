using wl.Helpers;

namespace wl.tests;

public class PathHelperTests
{
    // ResolvePath normalizes to forward slashes on Linux/macOS only (on Windows both
    // separators work and we keep native `\` to avoid mixed output).
    private readonly string _home = OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/');

    private static string N(string path) => OperatingSystem.IsWindows() ? path : path.Replace('\\', '/');

    [Fact]
    public void ResolveTilde_WithTilde_ReplacesWithHome()
    {
        var result = PathHelper.ResolveTilde("~/foo/bar");
        Assert.Equal(N(Path.Combine(_home, "foo", "bar")), result);
    }

    [Fact]
    public void ResolveTilde_WithoutTilde_ReturnsUnchanged()
    {
        var absPath = Path.Combine(Path.GetTempPath(), "some-project");
        var result = PathHelper.ResolveTilde(absPath);
        Assert.Equal(N(absPath), result);
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
    public void ResolvePath_WithDollarVar_SubstitutesFromLookup()
    {
        var result = PathHelper.ResolvePath("$REPOS_ROOT/project", name => name == "REPOS_ROOT" ? "/home/user/repos" : null);
        Assert.Equal("/home/user/repos/project", result);
    }

    [Fact]
    public void ResolvePath_WithBracedVar_SubstitutesFromLookup()
    {
        var result = PathHelper.ResolvePath("${REPOS_ROOT}/project", name => name == "REPOS_ROOT" ? "/x" : null);
        Assert.Equal("/x/project", result);
    }

    [Fact]
    public void ResolvePath_UndefinedVar_LeavesPlaceholder()
    {
        var result = PathHelper.ResolvePath("$MISSING/project", _ => null);
        Assert.Equal("$MISSING/project", result);
    }

    [Fact]
    public void ResolvePath_NullLookup_MatchesResolveTilde()
    {
        Assert.Equal(N(Path.Combine(_home, "foo")), PathHelper.ResolvePath("~/foo"));
        Assert.Equal(_home, PathHelper.ResolvePath("~"));
    }

    [Fact]
    public void ResolvePath_VarValueContainsTilde_ExpandsTilde()
    {
        var result = PathHelper.ResolvePath("$DOCS/notes", name => name == "DOCS" ? "~/OneDrive" : null);
        Assert.Equal(N(Path.Combine(_home, "OneDrive", "notes")), result);
    }

    [Fact]
    public void ResolvePath_WindowsBackslashes_NormalizedOnNonWindows()
    {
        // On Linux/macOS `\` is literal, so workspace.json authored on Windows must be
        // normalized for portability. On Windows both separators work so we leave `\` alone.
        var result = PathHelper.ResolvePath(@"$X\sub\dir", name => name == "X" ? "C:/users/me" : null);
        var expected = OperatingSystem.IsWindows() ? @"C:/users/me\sub\dir" : "C:/users/me/sub/dir";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolvePath_AbsolutePathWithNoVars_ReturnsUnchanged()
    {
        var absPath = Path.Combine(Path.GetTempPath(), "plain-project");
        Assert.Equal(N(absPath), PathHelper.ResolvePath(absPath, _ => "should-not-be-called"));
    }

    [Fact]
    public void ValidatePath_WithLookup_ResolvesVar()
    {
        var (exists, resolved) = PathHelper.ValidatePath("$HOME_ALIAS", name => name == "HOME_ALIAS" ? _home : null);
        Assert.True(exists);
        Assert.Equal(_home, resolved);
    }

    [Fact]
    public void ExtractVariables_FindsDollarAndBracedForms()
    {
        var vars = PathHelper.ExtractVariables("$REPOS_ROOT/sub/${ALT}/x$TRAILING").ToList();
        Assert.Equal(new[] { "REPOS_ROOT", "ALT", "TRAILING" }, vars);
    }

    [Fact]
    public void ExtractVariables_NoVars_ReturnsEmpty()
    {
        Assert.Empty(PathHelper.ExtractVariables("~/repos/plain"));
        Assert.Empty(PathHelper.ExtractVariables("D:/absolute/path"));
    }

    [Fact]
    public void FindOnPath_WhenFileExists_ReturnsFullPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "claude.cmd");
            File.WriteAllText(filePath, "@echo off");

            var result = PathHelper.FindOnPath("claude.cmd", tempDir);

            Assert.Equal(filePath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindOnPath_WithQuotedEntry_ReturnsFullPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl test path " + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "claude.cmd");
            File.WriteAllText(filePath, "@echo off");

            var result = PathHelper.FindOnPath("claude.cmd", $"\"{tempDir}\"");

            Assert.Equal(filePath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindOnPath_WhenFileMissing_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = PathHelper.FindOnPath("claude.cmd", tempDir);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindCommandOnPath_HonorsPathextOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var comPath = Path.Combine(tempDir, "claude.com");
            var cmdPath = Path.Combine(tempDir, "claude.cmd");
            File.WriteAllText(comPath, "");
            File.WriteAllText(cmdPath, "@echo off");

            var result = PathHelper.FindCommandOnPath("claude", tempDir, ".CMD;.COM");

            if (OperatingSystem.IsWindows())
            {
                Assert.NotNull(result);
                Assert.Equal(cmdPath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Null(result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindCommandOnPath_NormalizesPathextEntriesWithoutDots()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var cmdPath = Path.Combine(tempDir, "claude.cmd");
            File.WriteAllText(cmdPath, "@echo off");

            var result = PathHelper.FindCommandOnPath("claude", tempDir, "CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.NotNull(result);
                Assert.Equal(cmdPath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Null(result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
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
