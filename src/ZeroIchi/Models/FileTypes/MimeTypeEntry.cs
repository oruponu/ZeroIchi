namespace ZeroIchi.Models.FileTypes;

internal sealed record MimeMagic(int Priority, MagicMatch Clause);

internal sealed record MimeTypeEntry(string Name, string? Parent, string[] Extensions, MimeMagic[] Magics);
