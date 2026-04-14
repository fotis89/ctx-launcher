using wl.Services;

namespace wl.Commands;

public class ListCommand(WorkspaceService workspaces)
{
    public void Execute()
    {
        var list = workspaces.ListWorkspaces();
        if (list.Count == 0)
        {
            Console.WriteLine("  No workspaces yet. Run: wl create <name>");
            return;
        }

        var maxSlug = list.Max(w => w.FolderName.Length);
        Console.WriteLine();
        foreach (var ws in list)
        {
            var slug = ws.FolderName.PadRight(maxSlug);
            Console.WriteLine($"  {slug}   {ws.Name}");
        }
        Console.WriteLine();
    }
}