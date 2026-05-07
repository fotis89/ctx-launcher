using System.Reflection;

using wl.Helpers;

namespace wl.Services;

public class VersionService(WlPaths paths)
{
    public string GetCurrentVersion()
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? throw new InvalidOperationException("Assembly version not set");

    public string? GetInstalledVersion()
        => File.Exists(paths.VersionFile) ? File.ReadAllText(paths.VersionFile).Trim() : null;

    public void StampVersion()
        => File.WriteAllText(paths.VersionFile, GetCurrentVersion());
}
