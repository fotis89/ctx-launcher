namespace wl.e2e.tests;

public static class FakeClaude
{
    public static void Install(string binDir, string logPath)
    {
        Directory.CreateDirectory(binDir);

        if (OperatingSystem.IsWindows())
        {
            var p = Path.Combine(binDir, "claude.cmd");
            File.WriteAllText(p,
                "@echo off" + Environment.NewLine +
                $"echo %* >> \"{logPath}\"" + Environment.NewLine +
                "exit /b 0" + Environment.NewLine);
        }
        else
        {
            var p = Path.Combine(binDir, "claude");
            File.WriteAllText(p,
                "#!/bin/sh" + Environment.NewLine +
                $"echo \"$@\" >> \"{logPath}\"" + Environment.NewLine +
                "exit 0" + Environment.NewLine);
            File.SetUnixFileMode(p,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}
