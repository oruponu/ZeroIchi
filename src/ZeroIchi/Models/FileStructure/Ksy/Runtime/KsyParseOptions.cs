namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

public sealed record KsyParseOptions
{
    public static KsyParseOptions Default { get; } = new();

    public int NodeBudget { get; init; } = 100_000;
    public int MaxDepth { get; init; } = 256;
    public long MaxDecompressedSize { get; init; } = 64L * 1024 * 1024;
    public long MaxBytesLoad { get; init; } = 16L * 1024 * 1024;
    public long MaxTotalLoad { get; init; } = 256L * 1024 * 1024;
}
