using System.Text.Json;
using System.Text.Json.Nodes;

using wl.Models;

namespace wl.Services;

public class ConfigService(string filePath)
{
    private WlConfig? _cache;
    private bool _loaded;

    private static readonly HashSet<string> KnownConfigKeys = new(StringComparer.Ordinal)
    {
        "defaultTool",
    };

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

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (IOException)
        {
            return null;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Warning: {filePath} is not valid JSON; ignoring.");
            return null;
        }

        // Warn on unknown top-level keys so a typo like "defaulttool" doesn't
        // silently fall back to defaults.
        if (parsed is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                if (KnownConfigKeys.Contains(kv.Key))
                {
                    continue;
                }
                var suggestion = KnownConfigKeys
                    .FirstOrDefault(k => string.Equals(k, kv.Key, StringComparison.OrdinalIgnoreCase));
                var hint = suggestion is null ? "" : $" (did you mean '{suggestion}'?)";
                Console.Error.WriteLine($"Warning: unknown key '{kv.Key}' in {filePath}{hint}");
            }
        }

        try
        {
            _cache = JsonSerializer.Deserialize(json, WlJsonContext.Default.WlConfig);
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Warning: {filePath} could not be deserialized; ignoring.");
        }

        return _cache;
    }

    public string? DefaultTool => Load()?.DefaultTool;

    public string ResolveTool(Workspace ws, string? overrideTool = null)
        => ResolveToolWithSource(ws, overrideTool).Tool;

    public bool TryResolveValidatedTool(Workspace ws, string? overrideTool, ToolAdapterRegistry adapters, out string tool)
    {
        var (resolved, source) = ResolveToolWithSource(ws, overrideTool);
        tool = resolved;
        if (adapters.TryResolve(resolved, out _))
        {
            return true;
        }

        var sourceLabel = source switch
        {
            ToolSource.Override => "--tool override",
            ToolSource.Workspace => $"'{ws.Name}'/workspace.json (tool field)",
            ToolSource.Config => "~/.wl-workspaces/.config.json (defaultTool)",
            _ => "default",
        };
        Console.Error.WriteLine(
            $"Error: unknown tool '{resolved}' from {sourceLabel}. " +
            $"Available: {string.Join(", ", adapters.Names)}.");
        return false;
    }

    public (string Tool, ToolSource Source) ResolveToolWithSource(Workspace ws, string? overrideTool = null)
    {
        if (!string.IsNullOrEmpty(overrideTool))
        {
            return (overrideTool, ToolSource.Override);
        }

        if (!string.IsNullOrEmpty(ws.Tool))
        {
            return (ws.Tool, ToolSource.Workspace);
        }

        var configDefault = DefaultTool;
        if (!string.IsNullOrEmpty(configDefault))
        {
            return (configDefault, ToolSource.Config);
        }

        return ("claude", ToolSource.Default);
    }
}

public enum ToolSource
{
    Override,
    Workspace,
    Config,
    Default,
}
