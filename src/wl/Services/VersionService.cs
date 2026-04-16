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

    public void PrintSetupHintIfNeeded()
    {
        var installed = GetInstalledVersion();
        if (installed is null)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Hint: run 'wl setup' to install Claude skills.");
            return;
        }

        var current = GetCurrentVersion();
        if (installed != current)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  Hint: run 'wl setup' to update skills ({installed} → {current}).");
        }
    }
}
