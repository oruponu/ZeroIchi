namespace ZeroIchi.Models.FileStructure;

public sealed class CaseDefinition
{
    public required string Name { get; init; }
    public required long[] Range { get; init; }
    public FieldDefinition[]? Fields { get; init; }
}
