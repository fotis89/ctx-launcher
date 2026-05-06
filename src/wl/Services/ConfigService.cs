using System.Text.Json;

using wl.Models;

namespace wl.Services;

public class ConfigService(string filePath)
{
    private WlConfig? _cache;
    private bool _loaded;

    public string FilePath => filePath;

    private WlConfig? Load()
    {
        if (_loaded)
        {
            return _cache;
        }

        _loaded = true;
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            _cache = JsonSerializer.Deserialize(json, WlJsonContext.Default.WlConfig);
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Warning: {filePath} is not valid JSON; ignoring.");
        }

        return _cache;
    }

    public string? DefaultTool => Load()?.DefaultTool;

    public string ResolveTool(Workspace ws)
    {
        if (!string.IsNullOrEmpty(ws.Tool))
        {
            return ws.Tool;
        }

        var configDefault = DefaultTool;
        if (!string.IsNullOrEmpty(configDefault))
        {
            return configDefault;
        }

        return "claude";
    }
}
