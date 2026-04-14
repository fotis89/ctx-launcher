using wl.Helpers;
using wl.Services;

namespace wl.Commands;

public class CreateCommand(WorkspaceService workspaces)
{
    public void Execute(string name)
    {
        name = PathHelper.Slugify(name);
        var existing = workspaces.LoadWorkspace(name);
        if (existing is not null)
        {
            Console.Error.WriteLine($"Workspace '{name}' already exists.");
            return;
        }

        var cwd = Directory.GetCurrentDirectory();
        string primaryRepo;

        if (Directory.Exists(Path.Combine(cwd, ".git")))
        {
            primaryRepo = cwd;
            Console.WriteLine($"  Detected git repo: {cwd}");
        }
        else
        {
            Console.Write("  Primary repo path: ");
            primaryRepo = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(primaryRepo))
            {
                Console.Error.WriteLine("Primary repo path is required.");
                return;
            }
        }

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

        workspaces.CreateWorkspace(name, primaryRepo, additionalDirs);

        var root = workspaces.GetWorkspacesRoot();
        Console.WriteLine();
        Console.WriteLine($"  Workspace created: {Path.Combine(root, name)}");
        Console.WriteLine($"  Edit instructions.md, then run: wl launch {name}");
        Console.WriteLine();
        Console.WriteLine("  Tip: Run 'wl setup' to enable tab completion.");
    }
}