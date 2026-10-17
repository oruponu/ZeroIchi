using System;
using System.Collections.Generic;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Natives;

namespace ZeroIchi.Models.FileStructure.Ksy;

internal static class KsyBuiltinFormats
{
    internal const string ResourcePrefix = "ZeroIchi.Formats.";

    internal static IReadOnlyList<string> Roots { get; } =
    [
        "archive/zip",
        "image/bmp",
        "image/jpeg",
        "image/png",
        "image/webp",
        "media/mp3",
        "media/standard_midi_file",
        "media/wav",
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

    internal static List<KsyFormat> Load() => Load(CreateSource, Roots, CreateNativeTypes);

    internal static List<KsyFormat> Load(Func<IKsyImportSource> createSource, IEnumerable<string> roots,
        Func<KsyNativeTypeRegistry> createNatives)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Load(createSource(), roots, createNatives());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Console.Error.WriteLine(e);
            return [];
        }
    }

    internal static List<KsyFormat> Load(IKsyImportSource source, IEnumerable<string> roots,
        KsyNativeTypeRegistry natives)
    {
        var formats = new List<KsyFormat>();
        foreach (var root in roots)
        {
            try
            {
                var result = KsyModelBuilder.Load(root, source, natives);
                if (result.Format is { } format && result.Diagnostics.Count == 0) formats.Add(format);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Console.Error.WriteLine(e);
            }
        }
        return formats;
    }
}
