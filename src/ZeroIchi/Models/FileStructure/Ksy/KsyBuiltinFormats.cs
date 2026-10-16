using System;
using System.Collections.Generic;
using System.Text;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy;

internal static class KsyBuiltinFormats
{
    internal const string ResourcePrefix = "ZeroIchi.Formats.";

    internal static IReadOnlyList<string> Roots { get; } =
    [
        "image/bmp",
        "image/png",
    ];

    internal static KsyNativeTypeRegistry CreateNativeTypes() => new();

    internal static IKsyImportSource CreateSource() =>
        new KsyResourceImportSource(typeof(KsyBuiltinFormats).Assembly, ResourcePrefix);

    internal static List<KsyFormat> Load()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Load(CreateSource(), Roots, CreateNativeTypes());
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
