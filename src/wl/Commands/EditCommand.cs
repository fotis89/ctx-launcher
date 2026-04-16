using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class EditCommand(WorkspaceService workspaces)
{
    public void Execute(string name)
    {
        var ws = workspaces.LoadWorkspace(name);
        if (ws is null)
        {
            Console.Error.WriteLine($"Workspace '{name}' not found.");
            return;
        }

        ShellHelper.OpenFolder(ws.FolderPath);
    }
}
