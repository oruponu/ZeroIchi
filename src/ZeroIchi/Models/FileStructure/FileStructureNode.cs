using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure;

public sealed class FileStructureNode
{
    public required string Name { get; init; }
    public string? FieldId { get; init; }
    public required long Offset { get; init; }
    public required int Length { get; init; }
    public string Description { get; init; } = "";
    public List<FileStructureNode> Children { get; init; } = [];
}
