using wl.Services;

namespace wl.tests;

public class ClaudeRunnerTests
{
    [Fact]
    public void ResolveExecutable_PrefersExeOverCmd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var exePath = Path.Combine(tempDir, "claude.exe");
            var cmdPath = Path.Combine(tempDir, "claude.cmd");
            File.WriteAllText(exePath, "");
            File.WriteAllText(cmdPath, "@echo off");

            var result = ClaudeRunner.ResolveExecutable(tempDir, ".EXE;.CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(exePath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Equal("claude", result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveExecutable_UsesCmdWhenExeMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var cmdPath = Path.Combine(tempDir, "claude.cmd");
            File.WriteAllText(cmdPath, "@echo off");

            var result = ClaudeRunner.ResolveExecutable(tempDir, ".EXE;.CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(cmdPath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Equal("claude", result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveExecutable_WhenNoMatch_FallsBackToClaude()
    {
        var result = ClaudeRunner.ResolveExecutable(@"C:\this\does\not\exist", ".EXE;.CMD");

        Assert.Equal("claude", result);
    }

    [Fact]
    public void TryGetVersion_DoesNotThrow()
    {
        var ok = new ClaudeRunner().TryGetVersion(out var version);

        if (ok) Assert.False(string.IsNullOrWhiteSpace(version));
        else Assert.Equal("", version);
    }

    [Fact]
    public void ResolveExecutable_HonorsPathextForOtherExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var comPath = Path.Combine(tempDir, "claude.com");
            File.WriteAllText(comPath, "");

            var result = ClaudeRunner.ResolveExecutable(tempDir, ".COM;.EXE;.CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(comPath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Equal("claude", result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
