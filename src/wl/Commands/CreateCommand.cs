using System.Diagnostics;
using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces)
{
    public void Execute(string name)
    {
        var slug = PathHelper.Slugify(name);
        var existing = workspaces.LoadWorkspace(slug);
        if (existing is not null)
        {
            Console.Error.WriteLine($"Workspace '{slug}' already exists.");
            return;
        }

        var primaryRepo = Directory.GetCurrentDirectory();
        Console.WriteLine($"  Primary directory: {primaryRepo}");

        var additionalDirs = new List<string>();
        while (true)
        {
            Console.Write("  Add another directory? (y/n): ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (answer != "y")
            {
                break;
            }

            Console.Write("  Directory path: ");
            var dir = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(dir))
            {
                additionalDirs.Add(dir);
            }
        }

        workspaces.CreateWorkspace(slug, primaryRepo, additionalDirs, name);

        var workspacePath = Path.Combine(workspaces.GetWorkspacesRoot(), slug);
        Console.WriteLine();
        Console.WriteLine($"  Workspace created: {workspacePath}");
        Console.WriteLine($"  Edit instructions.md, then run: wl launch {slug}");
        Console.WriteLine();
        Console.WriteLine("  Tip: Run 'wl setup' to enable tab completion.");

        Process.Start(new ProcessStartInfo(workspacePath) { UseShellExecute = true });
    }
}