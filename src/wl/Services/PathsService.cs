using System.Text.Json;
using System.Text.RegularExpressions;

using wl.Models;

namespace wl.Services;

public partial class PathsService(string filePath)
{
    private Dictionary<string, string>? _cache;

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

        var map = Load();
        map[name] = value;
        var json = JsonSerializer.Serialize(map, WlJsonContext.Default.DictionaryStringString);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, json);
    }
}
