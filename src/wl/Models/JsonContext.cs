using System.Text.Json.Serialization;

namespace wl.Models;

[JsonSerializable(typeof(Workspace))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
public partial class WlJsonContext : JsonSerializerContext;