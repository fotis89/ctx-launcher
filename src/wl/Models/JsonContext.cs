using System.Text.Json.Serialization;

namespace wl.Models;

[JsonSerializable(typeof(Workspace))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
public partial class WlJsonContext : JsonSerializerContext;