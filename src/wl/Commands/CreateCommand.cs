using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces, CopilotRunner runner, SetupService setup, CopilotService copilot)
{
    public int Execute(string? name, bool basic = false)
    {
        if (basic && name is null)
        {
            Console.Error.WriteLine("Name required with --basic.");
            return 1;
        }

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
        if (basic)
        {
            var ws = new Workspace
            {
                Name = slug!,
                PrimaryRepo = Directory.GetCurrentDirectory(),
            };
            workspaces.SaveWorkspace(ws, slug!);
            Console.WriteLine($"Created workspace '{slug}' at {ws.FolderPath}");
            return 0;
        }

        return copilot.InvokeCreateSkill("wl-create-workspace", slug, Directory.GetCurrentDirectory(), workspaces.GetSharedDirPath(), runner);
    }
}