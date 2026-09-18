namespace wl.e2e.tests;

public static class FakeCopilot
{
    public static void Install(string binDir, string logPath, int exitCode = 0)
    {
        Directory.CreateDirectory(binDir);

        if (OperatingSystem.IsWindows())
        {
            var p = Path.Combine(binDir, "copilot.cmd");
            File.WriteAllText(p,
                "@echo off" + Environment.NewLine +
                $"echo %* >> \"{logPath}\"" + Environment.NewLine +
                $"echo %COPILOT_CUSTOM_INSTRUCTIONS_DIRS% >> \"{logPath}.env\"" + Environment.NewLine +
                $"exit /b {exitCode}" + Environment.NewLine);
        }
        else
        {
            var p = Path.Combine(binDir, "copilot");
            File.WriteAllText(p,
                "#!/bin/sh" + Environment.NewLine +
                $"echo \"$@\" >> \"{logPath}\"" + Environment.NewLine +
                $"echo \"$COPILOT_CUSTOM_INSTRUCTIONS_DIRS\" >> \"{logPath}.env\"" + Environment.NewLine +
                $"exit {exitCode}" + Environment.NewLine);
            File.SetUnixFileMode(p,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}