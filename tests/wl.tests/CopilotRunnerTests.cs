using wl.Services;

namespace wl.tests;

public class CopilotRunnerTests
{
    private const string NpmShimContent = """
        @ECHO off
        SETLOCAL
        CALL :find_dp0
        SET "_prog=%dp0%\node.exe"
        endLocal & goto #_undefined_# 2>NUL || title %COMSPEC% & "%_prog%"  "%dp0%\node_modules\@github\copilot\index.js" %*
        """;

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

    [Fact]
    public void TryResolveNpmShim_WithLocalNode_UsesNodeBesideShim()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-shim-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var shimPath = Path.Combine(tempDir, "copilot.cmd");
            var nodePath = Path.Combine(tempDir, "node.exe");
            var scriptPath = Path.Combine(tempDir, "node_modules", "@github", "copilot", "index.js");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            File.WriteAllText(nodePath, "");
            File.WriteAllText(scriptPath, "");

            var result = CopilotRunner.TryResolveNpmShim(shimPath, NpmShimContent, File.Exists, _ => throw new InvalidOperationException());

            Assert.NotNull(result);
            Assert.Equal(nodePath, result.Value.FileName);
            Assert.Equal([scriptPath], result.Value.PrefixArgs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryResolveNpmShim_WithoutLocalNode_UsesResolvedNode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-shim-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var shimPath = Path.Combine(tempDir, "copilot.cmd");
            var scriptPath = Path.Combine(tempDir, "node_modules", "@github", "copilot", "index.js");
            var resolvedNode = Path.Combine(tempDir, "other-node.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            File.WriteAllText(scriptPath, "");

            var result = CopilotRunner.TryResolveNpmShim(shimPath, NpmShimContent, File.Exists, command => command == "node" ? resolvedNode : null);

            Assert.NotNull(result);
            Assert.Equal(resolvedNode, result.Value.FileName);
            Assert.Equal([scriptPath], result.Value.PrefixArgs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryResolveNpmShim_WithUnparseableContent_ReturnsNull()
    {
        var result = CopilotRunner.TryResolveNpmShim(
            Path.Combine(Path.GetTempPath(), "copilot.cmd"),
            "@echo off\r\ncopilot %*",
            _ => true,
            _ => "node");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("say \"hi")]
    [InlineData("%PATH%")]
    [InlineData("!VAR!")]
    [InlineData("a^b")]
    [InlineData("a&b")]
    [InlineData("a|b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("line\rbreak")]
    [InlineData("line\nbreak")]
    public void ContainsCmdArgumentMetacharacter_RejectsUnsafeCharacters(string arg)
    {
        Assert.True(CopilotRunner.ContainsCmdArgumentMetacharacter(arg));
    }

    [Theory]
    [InlineData(@"C:\repos\ctx-launcher")]
    [InlineData("--name=workspace-abcd1234")]
    [InlineData("6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("-i")]
    [InlineData("prompt with spaces")]
    public void ContainsCmdArgumentMetacharacter_AllowsNormalArguments(string arg)
    {
        Assert.False(CopilotRunner.ContainsCmdArgumentMetacharacter(arg));
    }

    [Fact]
    public void TryCreateProcessStartInfo_WithNonBatchExecutable_PreservesArguments()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wl-test-runner-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var exePath = Path.Combine(tempDir, "copilot.exe");
            File.WriteAllText(exePath, "");

            var ok = CopilotRunner.TryCreateProcessStartInfo(
                exePath,
                tempDir,
                ["say \"hi & keep literal", "%PATH%"],
                environment: null,
                redirectStandardOutput: false,
                out var psi,
                out var exitCode);

            Assert.True(ok);
            Assert.Equal(0, exitCode);
            Assert.Equal(exePath, psi.FileName);
            Assert.Equal(tempDir, psi.WorkingDirectory);
            Assert.Equal(["say \"hi & keep literal", "%PATH%"], psi.ArgumentList);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
