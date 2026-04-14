using System.Text.RegularExpressions;

namespace wl.Helpers;

public static partial class PathHelper
{
    public static string ResolveTilde(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var remainder = path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(home, remainder));
        }
        return path;
    }

    public static string QuotePath(string path)
    {
        if (path.Contains(' '))
        {
            return $"\"{path}\"";
        }

        return path;
    }

    public static (bool Exists, string ResolvedPath) ValidatePath(string path)
    {
        var resolved = ResolveTilde(path);
        return (Directory.Exists(resolved) || File.Exists(resolved), resolved);
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
}