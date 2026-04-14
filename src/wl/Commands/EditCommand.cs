using System.Diagnostics;

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

        Process.Start(new ProcessStartInfo(ws.FolderPath) { UseShellExecute = true });
    }
}