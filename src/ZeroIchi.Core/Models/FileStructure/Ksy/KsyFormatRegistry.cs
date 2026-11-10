using System;
using System.Collections.Generic;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Models.FileStructure.Ksy;

public sealed class KsyFormatRegistry
{
    private readonly Func<FileTypeDetector> _detector;
    private readonly Dictionary<string, Lazy<KsyFormat?>> _formats = new(StringComparer.Ordinal);

    internal KsyFormatRegistry(Func<FileTypeDetector> detector) => _detector = detector;

    internal KsyFormatRegistry(Func<FileTypeDetector> detector, IEnumerable<KsyFormatRoot> roots,
        Func<string, KsyFormat?> load) : this(detector)
    {
        foreach (var root in roots)
        {
            var format = new Lazy<KsyFormat?>(() => load(root.Path));
            foreach (var mimeType in root.MimeTypes) _formats.Add(mimeType, format);
        }
    }

    public static KsyFormatRegistry Default { get; } =
        new(() => FileTypeDetector.Default, KsyBuiltinFormats.Roots, KsyBuiltinFormats.Load);

    internal void Register(KsyFormat format, params string[] mimeTypes)
    {
        var loaded = new Lazy<KsyFormat?>(format);
        foreach (var mimeType in mimeTypes) _formats.Add(mimeType, loaded);
    }

    public KsyFormat? Detect(ByteBuffer buffer)
    {
        var detector = _detector();
        if (detector.Detect(buffer)?.MimeType is not { } mimeType) return null;

        foreach (var name in detector.SelfAndAncestorNames(mimeType))
        {
            if (!_formats.TryGetValue(name, out var entry)) continue;
            if (entry.Value is not { } format) return null;
            return format.Magic is { } magic && !Matches(buffer, magic) ? null : format;
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
