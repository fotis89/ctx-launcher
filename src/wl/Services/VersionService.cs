using System.Reflection;

namespace wl.Services;

public class VersionService(WorkspaceService workspaces)
{
    private string VersionFilePath
        => Path.Combine(workspaces.GetWorkspacesRoot(), ".version");

    public string GetCurrentVersion()
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? throw new InvalidOperationException("Assembly version not set");

    public string? GetInstalledVersion()
        => File.Exists(VersionFilePath) ? File.ReadAllText(VersionFilePath).Trim() : null;

    public void StampVersion()
        => File.WriteAllText(VersionFilePath, GetCurrentVersion());
}
