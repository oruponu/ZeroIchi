using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests;

internal static class KsyTestRegistry
{
    public static KsyFormatRegistry Create(params KsyFormat[] formats)
    {
        var types = formats.Select((format, i) => Type($"x-test/{i}", format)).ToList();
        var detector = new FileTypeDetector(types);
        var registry = new KsyFormatRegistry(() => detector);
        for (var i = 0; i < formats.Length; i++) registry.Register(formats[i], types[i].Name);
        return registry;
    }

    private static MimeTypeEntry Type(string name, KsyFormat format)
    {
        var magic = format.Magic ?? throw new InvalidOperationException($"Format '{format.Id}' has no magic");
        var pattern = magic.Select(b => b ?? 0).ToArray();
        var mask = magic.Select(b => b is null ? (byte)0x00 : (byte)0xFF).ToArray();
        return new MimeTypeEntry(name, null, [],
            [new MimeMagic(50, new MagicMatch(MagicMatchKind.Bytes, 0, 0, pattern, mask, null, 0, []))]);
    }
}
