namespace ZeroIchi.Models;

public sealed record AppSettings
{
    public static AppSettings Default { get; } = new();

    public string? Language { get; init; }
}
