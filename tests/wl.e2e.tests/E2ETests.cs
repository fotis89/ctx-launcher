using System.Text.RegularExpressions;

namespace wl.e2e.tests;

public class E2ETests
{
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+(-[\w\.]+)?(\+[0-9a-f]+)?$");

    [SkippableFact]
    public void Version_exits_zero_and_matches_semver()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "--version");
        Assert.Equal(0, result.ExitCode);
        Assert.Matches(VersionPattern, result.Stdout.Trim());
    }

    [SkippableFact]
    public void Help_exits_zero_and_prints_usage()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "--help");
        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
        var text = result.Stdout + result.Stderr;
        Assert.True(
            text.Contains("Usage", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Commands", StringComparison.OrdinalIgnoreCase),
            $"Expected help output to contain 'Usage' or 'Commands'. Got:\n{text}");
    }

    [SkippableFact]
    public void Unknown_command_exits_nonzero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "not-a-real-command");
        Assert.NotEqual(0, result.ExitCode);
    }

    [SkippableFact]
    public void List_command_is_removed()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "list");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("list", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void Create_basic_exits_zero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, result.ExitCode);
        var json = File.ReadAllText(System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName, "workspace.json"));
        Assert.Contains("\"schemaVersion\": 2", json);
        Assert.DoesNotContain("\"tool\"", json);
    }

    [SkippableFact]
    public void Which_known_workspace_exits_zero()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var result = WlRunner.Run(home.Path, extraPathDir: null, "which", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(home.WorkspaceName, result.Stdout, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Unknown_workspace_lists_available_workspaces()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var result = WlRunner.Run(home.Path, extraPathDir: null, "launch", "missing-workspace");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Workspaces root:", result.Stderr, StringComparison.Ordinal);
        Assert.Contains(home.WorkspaceName, result.Stderr, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Edit_command_is_removed()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, extraPathDir: null, "edit", "nonexistent-ws");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("edit", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void Launch_invokes_copilot_shim()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        var fakeBin = Path.Combine(home.Path, "fake-bin");
        var copilotLog = Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(fakeBin, copilotLog);

        var result = WlRunner.Run(home.Path, extraPathDir: fakeBin, "launch", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(copilotLog), $"Expected fake copilot to be invoked. stdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(copilotLog)));
    }

    [SkippableFact]
    public void Paths_command_is_removed()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();

        var result = WlRunner.Run(home.Path, extraPathDir: null, "paths", "list");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("paths", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void Which_with_unset_variable_shows_warning()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();

        var create = WlRunner.Run(home.Path, extraPathDir: null, "create", home.WorkspaceName, "--basic");
        Assert.Equal(0, create.ExitCode);

        // Inject a $VAR reference into the scaffolded workspace.json.
        var wsPath = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName, "workspace.json");
        var json = File.ReadAllText(wsPath);
        json = json.Replace("\"additionalDirs\": []", "\"additionalDirs\": [\"$E2E_UNSET/foo\"]");
        File.WriteAllText(wsPath, json);

        var result = WlRunner.Run(home.Path, extraPathDir: null, "which", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("$E2E_UNSET", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("unset", result.Stdout, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Setup_exits_zero_when_copilot_on_path()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var fakeBin = Path.Combine(home.Path, "fake-bin");
        var copilotLog = Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(fakeBin, copilotLog);

        var result = WlRunner.Run(home.Path, extraPathDir: fakeBin, "setup");
        Assert.Equal(0, result.ExitCode);
    }

    [SkippableFact]
    public void Fresh_resume_and_new_preserve_copilot_arguments_and_context()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var folder = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName);
        var wsPath = System.IO.Path.Combine(folder, "workspace.json");
        var wsJson = File.ReadAllText(wsPath);
        wsJson = wsJson.TrimEnd().TrimEnd('}') + ",\n  \"copilotArgs\": [\"--yolo\"]\n}";
        File.WriteAllText(wsPath, wsJson);
        File.WriteAllText(System.IO.Path.Combine(folder, "AGENTS.md"), "Project instructions");
        var skills = Directory.CreateDirectory(System.IO.Path.Combine(folder, ".copilot", "skills", "wl-review")).FullName;
        File.WriteAllText(System.IO.Path.Combine(skills, "SKILL.md"), "---\nname: wl-review\ndescription: Review code\n---\nReview this repo.");
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log);
        var fresh = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--", "-i", "Review this project");
        Assert.Equal(0, fresh.ExitCode);
        var sessionPath = System.IO.Path.Combine(folder, ".last-session");
        var firstSession = File.ReadAllText(sessionPath);
        var args = File.ReadAllText(log);
        Assert.True(Guid.TryParseExact(firstSession, "D", out _));
        Assert.Contains($"--session-id={firstSession}", args);
        Assert.Contains($"--name={home.WorkspaceName}-", args);
        Assert.Contains("--yolo", args);
        Assert.Contains("-i", args);
        Assert.Contains("Review this project", args);
        Assert.Contains("--plugin-dir", args);
        Assert.Contains(System.IO.Path.Combine(folder, ".copilot"), args);
        Assert.Equal($"{home.Path},{folder}", File.ReadAllText(log + ".env").Trim());
        Assert.Contains("Project instructions", File.ReadAllText(System.IO.Path.Combine(folder, "AGENTS.md")));
        Assert.True(File.Exists(System.IO.Path.Combine(folder, ".copilot", "plugin.json")));

        File.Delete(log);
        var resumed = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName);
        Assert.Equal(0, resumed.ExitCode);
        Assert.Contains($"--resume={firstSession}", File.ReadAllText(log));
        Assert.DoesNotContain("--name=", File.ReadAllText(log));
        Assert.DoesNotContain("--session-id=", File.ReadAllText(log));
        Assert.Equal(firstSession, File.ReadAllText(sessionPath));

        File.Delete(log);
        var lastUsedPassThrough = WlRunner.Run(home.Path, bin, "launch", "--", "--model", "x");
        Assert.Equal(0, lastUsedPassThrough.ExitCode);
        Assert.Contains("--model x", File.ReadAllText(log));
        Assert.DoesNotContain($"--resume={firstSession}", File.ReadAllText(log));

        File.Delete(log);
        var workspacePassThrough = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--", "-i", "a b");
        Assert.Equal(0, workspacePassThrough.ExitCode);
        Assert.Contains("-i", File.ReadAllText(log));
        Assert.Contains("a b", File.ReadAllText(log));

        File.Delete(log);
        Assert.Equal(0, WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--new").ExitCode);
        Assert.NotEqual(firstSession, File.ReadAllText(sessionPath));
        Assert.True(Guid.TryParseExact(File.ReadAllText(sessionPath), "D", out _));
        Assert.Contains($"--session-id={File.ReadAllText(sessionPath)}", File.ReadAllText(log));
        Assert.Contains("--name=", File.ReadAllText(log));
        Assert.DoesNotContain("--resume=", File.ReadAllText(log));
        var rememberedSession = File.ReadAllText(sessionPath);

        File.Delete(log);
        Assert.Equal(0, WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--temp").ExitCode);
        var tempArgs = File.ReadAllText(log);
        Assert.Contains($"--name={home.WorkspaceName}-temp-", tempArgs);
        Assert.Contains("--session-id=", tempArgs);
        Assert.DoesNotContain("--resume=", tempArgs);
        Assert.Equal(rememberedSession, File.ReadAllText(sessionPath));

        File.Delete(log);
        Assert.Equal(0, WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName).ExitCode);
        Assert.Contains($"--resume={rememberedSession}", File.ReadAllText(log));
    }

    [SkippableFact]
    public void Failed_copilot_does_not_replace_saved_pointers()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var pointer = System.IO.Path.Combine(root, home.WorkspaceName, ".last-session");
        File.WriteAllText(pointer, "previous-session");
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        FakeCopilot.Install(bin, System.IO.Path.Combine(home.Path, "copilot.log"), exitCode: 42);
        var result = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--new");
        Assert.Equal(42, result.ExitCode);
        Assert.Contains("exited with code 42", result.Stderr);
        Assert.Equal("previous-session", File.ReadAllText(pointer));
    }

    [SkippableFact]
    public void PreviouslySavedSessionName_StillResumesWithoutMigration()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var pointer = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName, ".last-session");
        File.WriteAllText(pointer, "previous-session-name");
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log);
        Assert.Equal(0, WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName).ExitCode);
        Assert.Contains("--resume=previous-session-name", File.ReadAllText(log));
        Assert.DoesNotContain("--session-id=", File.ReadAllText(log));
        Assert.Equal("previous-session-name", File.ReadAllText(pointer));
    }

    [SkippableFact]
    public void Missing_copilot_fails_without_saving_a_session()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("'copilot' not found", result.Stderr);
        Assert.False(File.Exists(System.IO.Path.Combine(root, home.WorkspaceName, ".last-session")));
        Assert.NotEqual(0, WlRunner.Run(home.Path, null, "setup").ExitCode);
    }

    [SkippableFact]
    public void Folder_mode_remembers_sessions_per_folder()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log);

        var first = WlRunner.Run(home.Path, bin, "launch");
        Assert.Equal(0, first.ExitCode);
        var firstArgs = File.ReadAllText(log);
        Assert.Contains("--session-id=", firstArgs);
        Assert.True(File.Exists(System.IO.Path.Combine(home.Path, ".wl-workspaces", ".folder-sessions.json")));

        File.Delete(log);
        var second = WlRunner.Run(home.Path, bin, "launch");
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("--resume=", File.ReadAllText(log));
    }

    [SkippableFact]
    public void Removed_resume_option_fails()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var result = WlRunner.Run(home.Path, null, "launch", home.WorkspaceName, "--resume");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--resume", result.Stderr);
    }

    [SkippableFact]
    public void Temp_with_new_fails_and_temp_ignores_malformed_pointer()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var folder = System.IO.Path.Combine(home.Path, ".wl-workspaces", home.WorkspaceName);
        var session = System.IO.Path.Combine(folder, ".last-session");
        File.WriteAllText(session, "bad\npointer");
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log);

        var conflict = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--temp", "--new");
        Assert.NotEqual(0, conflict.ExitCode);
        Assert.Contains("Cannot use", conflict.Stderr);

        var temp = WlRunner.Run(home.Path, bin, "launch", home.WorkspaceName, "--temp");
        Assert.Equal(0, temp.ExitCode);
        Assert.Contains("--name=", File.ReadAllText(log));
        Assert.Equal("bad\npointer", File.ReadAllText(session));
    }

    [SkippableFact]
    public void Removed_tool_flag_is_rejected_and_help_is_copilot_only()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var result = WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic", "--tool", "copilot");
        Assert.NotEqual(0, result.ExitCode);
        var help = WlRunner.Run(home.Path, null, "create", "--help");
        Assert.Contains("Copilot", help.Stdout);
        Assert.DoesNotContain("--tool", help.Stdout);
    }

    [SkippableFact]
    public void Create_uses_copilot_skill_and_propagates_failure()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var bin = System.IO.Path.Combine(home.Path, "fake-bin");
        var log = System.IO.Path.Combine(home.Path, "copilot.log");
        FakeCopilot.Install(bin, log, exitCode: 7);
        var result = WlRunner.Run(home.Path, bin, "create", "My Workspace");
        Assert.Equal(7, result.ExitCode);
        var args = File.ReadAllText(log);
        Assert.Contains("wl-create-workspace", args);
        Assert.Contains("my-workspace", args);
        Assert.Contains("--plugin-dir", args);
        Assert.Contains("--add-dir", args);
    }

    [SkippableFact]
    public void Which_is_read_only_including_launch_preparation()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var folder = System.IO.Path.Combine(root, home.WorkspaceName);
        File.WriteAllText(System.IO.Path.Combine(folder, "AGENTS.md"), "Context");
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
        var result = WlRunner.Run(home.Path, null, "which", home.WorkspaceName);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("AGENTS.md", result.Stdout);
        Assert.Contains("copilot --name=", result.Stdout);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order());
        foreach (var (path, content) in before) Assert.Equal(content, File.ReadAllText(path));
    }

    [SkippableFact]
    public void Which_folder_mode_and_workspace_passthrough_are_read_only()
    {
        Skip.If(BinaryFixture.ExePath is null, BinaryFixture.SkipReason);
        using var home = new TempHome();
        var folderResult = WlRunner.Run(home.Path, null, "which");
        Assert.Equal(0, folderResult.ExitCode);
        Assert.Contains("Folder:", folderResult.Stdout);
        Assert.Contains("copilot --name=", folderResult.Stdout);
        Assert.False(File.Exists(System.IO.Path.Combine(home.Path, ".wl-workspaces", ".folder-sessions.json")));

        Assert.Equal(0, WlRunner.Run(home.Path, null, "create", home.WorkspaceName, "--basic").ExitCode);
        var root = System.IO.Path.Combine(home.Path, ".wl-workspaces");
        var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);

        var workspaceResult = WlRunner.Run(home.Path, null, "which", home.WorkspaceName, "--", "--model", "x");

        Assert.Equal(0, workspaceResult.ExitCode);
        Assert.Contains("--model x", workspaceResult.Stdout);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order());
        foreach (var (path, content) in before) Assert.Equal(content, File.ReadAllText(path));
    }

    private sealed class TempHome : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("wl-e2e-").FullName;
        public string WorkspaceName { get; } = "e2e-" + Guid.NewGuid().ToString("N")[..8];

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }

        }
    }
}