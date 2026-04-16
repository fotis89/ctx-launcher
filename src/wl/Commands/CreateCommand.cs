using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces, ClaudeRunner claudeRunner)
{
    public void Execute(string? name)
    {
        if (name is not null)
        {
            var slug = PathHelper.Slugify(name);
            if (slug == WorkspaceService.SharedDirName.TrimStart('.'))
            {
                Console.Error.WriteLine("'shared' is reserved. Choose a different name.");
                return;
            }

            if (workspaces.LoadWorkspace(slug) is not null)
            {
                Console.Error.WriteLine($"Workspace '{slug}' already exists.");
                return;
            }
        }

        var prompt = name is not null
            ? $"/wl-create-workspace {name}"
            : "/wl-create-workspace";

        claudeRunner.Run(Directory.GetCurrentDirectory(), [prompt]);
    }
}
