using System;
using System.Collections.Generic;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Natives;

namespace ZeroIchi.Models.FileStructure.Ksy;

internal static class KsyBuiltinFormats
{
    internal const string ResourcePrefix = "ZeroIchi.Formats.";

    internal static IReadOnlyList<KsyFormatRoot> Roots { get; } =
    [
        new("archive/zip", ["application/zip"]),
        new("image/bmp", ["image/bmp"]),
        new("image/jpeg", ["image/jpeg"]),
        new("image/png", ["image/png"]),
        new("image/webp", ["image/webp"]),
        new("media/mp3", ["audio/mpeg"]),
        new("media/standard_midi_file", ["audio/midi"]),
        new("media/wav", ["audio/vnd.wave"]),
    ];

    internal static KsyNativeTypeRegistry CreateNativeTypes()
    {
        var natives = new KsyNativeTypeRegistry();
        natives.Register(new JpegEntropyData());
        natives.Register(new MidiTrackEvents());
        natives.Register(new ZipDataWithDescriptor());
        return natives;
    }

    internal static IKsyImportSource CreateSource() =>
        new KsyResourceImportSource(typeof(KsyBuiltinFormats).Assembly, ResourcePrefix);

    internal static KsyFormat? Load(string path) => Load(CreateSource, path, CreateNativeTypes);

    internal static KsyFormat? Load(Func<IKsyImportSource> createSource, string path,
        Func<KsyNativeTypeRegistry> createNatives)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var result = KsyModelBuilder.Load(path, createSource(), createNatives());
            return result.Diagnostics.Count == 0 ? result.Format : null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Console.Error.WriteLine(e);
            return null;
        }
    }
}
