using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces, CopilotRunner runner, SetupService setup, CopilotService copilot)
{
    public int Execute(string? name)
    {
        string? slug = null;
        if (name is not null)
        {
            slug = PathHelper.Slugify(name);
            if (slug.Length == 0 || slug == WorkspaceService.SharedDirName.TrimStart('.'))
            {
                Console.Error.WriteLine("Choose a non-empty workspace name other than 'shared'.");
                return 1;
            }
            if (Directory.Exists(workspaces.GetWorkspaceFolder(slug)))
            {
                Console.Error.WriteLine($"Workspace folder '{slug}' already exists. Update it manually or choose another name.");
                return 1;
            }
        }
        setup.EnsureInstalled();
        setup.EnsureInstalled();
        var before = Directory.Exists(workspaces.GetWorkspacesRoot())
            ? Directory.GetDirectories(workspaces.GetWorkspacesRoot()).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var exitCode = copilot.InvokeCreateSkill("wl-workspace", slug, Directory.GetCurrentDirectory(), workspaces.GetSharedDirPath(), runner);
        if (exitCode == 0 && Directory.Exists(workspaces.GetWorkspacesRoot()))
        {
            foreach (var dir in Directory.GetDirectories(workspaces.GetWorkspacesRoot()).Where(d => !before.Contains(d)))
            {
                Console.WriteLine($"Created workspace at {dir}");
        }
        }
        return exitCode;
    }
}