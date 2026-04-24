using wl.Helpers;
using wl.Models;
using wl.Services;

namespace wl.Commands;

public class PathsCommand(WorkspaceService workspaces, PathsService paths)
{
    public bool Set(string name, string value)
    {
        try
        {
            paths.Set(name, value);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  Error: {ex.Message}");
            Console.Error.WriteLine();
            return false;
        }
        Console.WriteLine();
        Console.WriteLine($"  Set ${name} = {value}");
        Console.WriteLine($"  ({paths.FilePath})");
        Console.WriteLine();
        return true;
    }

    public void List()
    {
        var usage = BuildUsageMap();
        var defined = paths.All();

        Console.WriteLine();

        if (defined.Count == 0 && usage.Count == 0)
        {
            Console.WriteLine("  (no variables defined, no variables referenced)");
            Console.WriteLine();
            return;
        }

        if (defined.Count > 0)
        {
            Console.WriteLine($"  Defined in {paths.FilePath}:");
            var width = defined.Keys.Max(k => k.Length);
            foreach (var (name, value) in defined.OrderBy(kv => kv.Key))
            {
                var users = usage.TryGetValue(name, out var list)
                    ? $"(used by: {string.Join(", ", list)})"
                    : "(not referenced)";
                Console.WriteLine($"    ${name.PadRight(width)} = {value}  {users}");
            }
        }

        var undefined = usage.Keys.Except(defined.Keys).OrderBy(k => k).ToList();
        if (undefined.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Referenced but not defined:");
            var width = undefined.Max(k => k.Length);
            foreach (var name in undefined)
            {
                Console.WriteLine($"    ${name.PadRight(width)} (used by: {string.Join(", ", usage[name])})");
            }
            Console.WriteLine();
            Console.WriteLine("  Run 'wl paths init' to set them interactively.");
        }
        Console.WriteLine();
    }

    public void Init()
    {
        var usage = BuildUsageMap();
        var defined = new HashSet<string>(paths.All().Keys, StringComparer.Ordinal);
        var missing = usage.Where(kv => !defined.Contains(kv.Key))
            .OrderBy(kv => kv.Key)
            .ToList();

        Console.WriteLine();
        if (missing.Count == 0)
        {
            Console.WriteLine("  All referenced variables are defined.");
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"  Found {missing.Count} undefined variables referenced in workspaces:");
        Console.WriteLine();

        var set = 0;
        foreach (var (name, users) in missing)
        {
            Console.WriteLine($"    ${name} (used by: {string.Join(", ", users)})");
            Console.Write("    Set to (or Enter to skip): ");
            var value = Console.ReadLine();
            Console.WriteLine();
            if (!string.IsNullOrWhiteSpace(value))
            {
                paths.Set(name, StripQuotes(value.Trim()));
                set++;
            }
        }

        Console.WriteLine(set > 0
            ? $"  Wrote {set} variable(s) to {paths.FilePath}."
            : "  No variables set.");
        Console.WriteLine();
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }
        return value;
    }

    private Dictionary<string, List<string>> BuildUsageMap()
    {
        var usage = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var ws in workspaces.ListWorkspaces())
        {
            foreach (var variable in ExtractAllVars(ws))
            {
                if (!usage.TryGetValue(variable, out var list))
                {
                    list = [];
                    usage[variable] = list;
                }
                if (!list.Contains(ws.FolderName))
                {
                    list.Add(ws.FolderName);
                }
            }
        }
        return usage;
    }

    private static IEnumerable<string> ExtractAllVars(Workspace ws)
    {
        foreach (var v in PathHelper.ExtractVariables(ws.PrimaryRepo))
        {
            yield return v;
        }
        foreach (var dir in ws.AdditionalDirs)
        {
            foreach (var v in PathHelper.ExtractVariables(dir))
            {
                yield return v;
            }
        }
    }
}
