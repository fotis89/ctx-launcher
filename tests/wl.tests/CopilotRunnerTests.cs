using wl.Services;

namespace wl.tests;

public class CopilotRunnerTests
{
    [Fact]
    public void ResolveExecutable_PrefersExeOverCmd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var exePath = Path.Combine(tempDir, "copilot.exe");
            var cmdPath = Path.Combine(tempDir, "copilot.cmd");
            File.WriteAllText(exePath, "");
            File.WriteAllText(cmdPath, "@echo off");

            var result = CopilotRunner.ResolveExecutable("copilot", tempDir, ".EXE;.CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(exePath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Equal("copilot", result);
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
            var cmdPath = Path.Combine(tempDir, "copilot.cmd");
            File.WriteAllText(cmdPath, "@echo off");

            var result = CopilotRunner.ResolveExecutable("copilot", tempDir, ".EXE;.CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(cmdPath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Equal("copilot", result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveExecutable_WhenNoMatch_FallsBackToCopilot()
    {
        var result = CopilotRunner.ResolveExecutable("copilot", @"C:\this\does\not\exist", ".EXE;.CMD");

        Assert.Equal("copilot", result);
    }

    [Fact]
    public void ResolveExecutable_HonorsPathextForOtherExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-path-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var comPath = Path.Combine(tempDir, "copilot.com");
            File.WriteAllText(comPath, "");

            var result = CopilotRunner.ResolveExecutable("copilot", tempDir, ".COM;.EXE;.CMD");

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(comPath, result, ignoreCase: true, ignoreLineEndingDifferences: false, ignoreWhiteSpaceDifferences: false);
            }
            else
            {
                Assert.Equal("copilot", result);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}