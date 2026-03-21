using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace ZeroIchi.Models.FileStructure;

public sealed class FormatDefinition
{
    public required string Name { get; init; }
    public required string[] Extensions { get; init; }
    public required string Magic { get; init; }
    public string Endian { get; init; } = "little";
    public required FieldDefinition[] Fields { get; init; }

    [JsonIgnore]
    public bool IsBigEndian => Endian == "big";

    [JsonIgnore]
    public byte?[] MagicBytes => field ??= [.. Magic
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s == "??" ? (byte?)null : Convert.ToByte(s, 16))];
}
