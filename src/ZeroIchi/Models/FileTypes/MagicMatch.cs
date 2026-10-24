namespace ZeroIchi.Models.FileTypes;

internal enum MagicMatchKind
{
    Bytes,
    BytesIgnoreCase,
    Regex,
    MinShouldMatch,
    Unsupported,
}

internal sealed record MagicMatch(
    MagicMatchKind Kind,
    int OffsetStart,
    int OffsetEnd,
    byte[] Pattern,
    byte[] Mask,
    string? Regex,
    int MinShouldMatch,
    MagicMatch[] Children);
