using System.Text.RegularExpressions;

namespace wl.Helpers;

public static partial class PathHelper
{
    public static string ResolvePath(string path, Func<string, string?>? lookup = null)
    {
        if (lookup is not null && path.Contains('$'))
        {
            path = VarRegex().Replace(path, match =>
            {
                var name = match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : match.Groups[2].Value;
                return lookup(name) ?? match.Value;
            });
        }

        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var remainder = path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            path = Path.GetFullPath(Path.Combine(home, remainder));
        }

        // On Linux/macOS, `\` is a literal character (not a separator), so workspace.json
        // authored on Windows with `\` must be normalized for portability. On Windows both
        // separators work natively, so we leave paths alone — keeps `wl which` output
        // visually consistent (no mixed `C:/Users/foo\bar` after Path.Combine).
        return OperatingSystem.IsWindows() ? path : path.Replace('\\', '/');
    }

    public static string ResolveTilde(string path) => ResolvePath(path);

    public static IEnumerable<string> ExtractVariables(string path)
    {
        foreach (Match m in VarRegex().Matches(path))
        {
            yield return m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : m.Groups[2].Value;
        }
    }

    public static string QuotePath(string path)
    {
        if (path.Contains(' '))
        {
            return $"\"{path}\"";
        }

        return path;
    }

    public static (bool Exists, string ResolvedPath) ValidatePath(string path, Func<string, string?>? lookup = null)
    {
        var resolved = ResolvePath(path, lookup);
        return (Directory.Exists(resolved) || File.Exists(resolved), resolved);
    }

    public static string? FindOnPath(string fileName, string? pathEnv = null)
    {
        pathEnv ??= Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dir = entry.Trim('"');
            if (dir.Length == 0)
            {
                continue;
            }

            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? FindCommandOnPath(string commandName, string? pathEnv = null, string? pathExtEnv = null)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(commandName))
        {
            return FindOnPath(commandName, pathEnv);
        }

        pathExtEnv ??= Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathExtEnv))
        {
            pathExtEnv = ".COM;.EXE;.BAT;.CMD";
        }

        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in pathExtEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = extension.StartsWith('.') ? extension : "." + extension;
            if (!seenExtensions.Add(normalized))
            {
                continue;
            }

            var match = FindOnPath(commandName + normalized, pathEnv);
            if (match is not null)
            {
                return match;
            }
        }

        return FindOnPath(commandName, pathEnv);
    }

    public static string Slugify(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = SlugRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        slug = MultipleDashRegex().Replace(slug, "-");
        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9-]")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleDashRegex();

    [GeneratedRegex(@"\$(?:\{(\w+)\}|(\w+))")]
    private static partial Regex VarRegex();
}
