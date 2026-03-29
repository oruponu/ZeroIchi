using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroIchi.Models.FileStructure;

public sealed class FieldDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public JsonElement? Size { get; init; }
    public FieldDefinition[]? Fields { get; init; }
    public string? Until { get; init; }
    public string? NameTemplate { get; init; }
    public string? Endian { get; init; }
    public string? On { get; init; }
    public CaseDefinition[]? Cases { get; init; }
    public int? PeekMin { get; init; }

    [JsonIgnore]
    public int FixedSize => Type switch
    {
        "uint8" or "int8" => 1,
        "uint16" or "int16" => 2,
        "uint32" or "int32" => 4,
        "uint64" or "int64" => 8,
        _ => -1,
    };
}
