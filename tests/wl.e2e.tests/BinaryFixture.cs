namespace wl.e2e.tests;

public static class BinaryFixture
{
    public static readonly string? ExePath = Resolve();

    public static string SkipReason => $"wl binary not found; set WL_BINARY_PATH or run 'dotnet publish src/wl -c Release -r <rid>' first";

    private static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("WL_BINARY_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            return env;
        }

        var exeName = OperatingSystem.IsWindows() ? "wl.exe" : "wl";
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var publishRoot = Path.Combine(dir, "src", "wl", "bin", "Release", "net10.0");
            if (Directory.Exists(publishRoot))
            {
                foreach (var ridDir in Directory.EnumerateDirectories(publishRoot))
                {
                    var candidate = Path.Combine(ridDir, "publish", exeName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
