using System.Text.Json;
using System.Text.RegularExpressions;

using wl.Models;

namespace wl.Services;

public partial class PathsService(string filePath)
{
    private Dictionary<string, string>? _cache;
    private bool _loadFailed;

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ValidNameRegex();

    public string FilePath => filePath;

    private Dictionary<string, string> Load()
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(filePath))
        {
            _cache = new Dictionary<string, string>(StringComparer.Ordinal);
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            _cache = JsonSerializer.Deserialize(json, WlJsonContext.Default.DictionaryStringString)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Warning: {filePath} is not valid JSON; treating as empty.");
            _cache = new Dictionary<string, string>(StringComparer.Ordinal);
            _loadFailed = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // File exists but isn't readable. Treat as empty rather than
            // crashing wl on every command that resolves a $VAR path.
            Console.Error.WriteLine($"Warning: cannot read {filePath} ({ex.GetType().Name}); treating as empty.");
            _cache = new Dictionary<string, string>(StringComparer.Ordinal);
            _loadFailed = true;
        }

        return _cache;
    }

    public string? Get(string name)
        => Load().TryGetValue(name, out var value) ? value : null;

    public IReadOnlyDictionary<string, string> All() => Load();

    public void Set(string name, string value)
    {
        if (!ValidNameRegex().IsMatch(name))
        {
            throw new ArgumentException(
                $"Invalid variable name '{name}'. Names must start with a letter or underscore and contain only letters, digits, and underscores.",
                nameof(name));
        }

        var existing = Load();
        if (_loadFailed)
        {
            throw new InvalidOperationException($"{filePath}: cannot save path variables because the existing file could not be loaded. Fix or delete this file and try again.");
        }

        var map = new Dictionary<string, string>(existing, StringComparer.Ordinal);
        map[name] = value;
        var json = JsonSerializer.Serialize(map, WlJsonContext.Default.DictionaryStringString);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var tmp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, filePath, overwrite: true);
            _cache = map;
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
