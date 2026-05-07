namespace wl.Services;

public class ToolAdapterRegistry
{
    private readonly Dictionary<string, IToolAdapter> _adapters = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IToolAdapter adapter)
    {
        _adapters[adapter.ExecutableName] = adapter;
    }

    public string[] Names => [.. _adapters.Keys];

    public bool TryResolve(string tool, out IToolAdapter adapter)
        => _adapters.TryGetValue(tool, out adapter!);

    public IToolAdapter Resolve(string tool)
    {
        if (TryResolve(tool, out var adapter))
        {
            return adapter;
        }

        var available = _adapters.Count == 0 ? "(none)" : string.Join(", ", _adapters.Keys);
        throw new InvalidOperationException(
            $"No adapter registered for tool '{tool}'. Available: {available}.");
    }
}
