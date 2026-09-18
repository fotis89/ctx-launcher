using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class EditCommand(WorkspaceService workspaces)
{
    public int Execute(string name)
    {
        var folder = workspaces.GetWorkspaceFolder(name);
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            return 1;
        }

        ShellHelper.OpenFolder(folder);
        return 0;
    }
}