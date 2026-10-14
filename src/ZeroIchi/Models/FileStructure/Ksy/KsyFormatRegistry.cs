using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy;

public sealed class KsyFormatRegistry
{
    private readonly List<KsyFormat> _formats = [];

    public static KsyFormatRegistry Default { get; } = new();

    internal void Register(KsyFormat format) => _formats.Add(format);

    public KsyFormat? Detect(ByteBuffer buffer, string? filePath)
    {
        KsyFormat? best = null;
        foreach (var format in _formats)
        {
            if (format.Magic is { } magic && Matches(buffer, magic) && magic.Length > (best?.Magic?.Length ?? 0))
                best = format;
        }
        if (best is not null) return best;

        var extension = Path.GetExtension(filePath ?? "").TrimStart('.');
        if (extension.Length == 0) return null;

        foreach (var format in _formats)
        {
            if (format.Magic is not null) continue;
            if (format.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return format;
        }
        return null;
    }

    private static bool Matches(ByteBuffer buffer, byte?[] magic)
    {
        if (buffer.Length < magic.Length) return false;
        for (var i = 0; i < magic.Length; i++)
        {
            if (magic[i] is { } expected && buffer.ReadByte(i) != expected) return false;
        }
        return true;
    }
}
