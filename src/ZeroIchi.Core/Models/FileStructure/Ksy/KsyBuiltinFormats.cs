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
        new("archive/cpio_old_le", ["application/x-cpio"]),
        new("archive/gzip", ["application/gzip"]),
        new("archive/lzh", ["application/x-lha", "application/x-lharc"]),
        new("archive/rar", ["application/x-rar-compressed"]),
        new("archive/rpm", ["application/x-rpm"]),
        new("archive/zip",
        [
            "application/zip", "application/epub+zip",
            "application/vnd.adobe.air-application-installer-package+zip",
        ]),
        new("database/dbf", ["application/x-dbf"]),
        new("database/sqlite3", ["application/x-sqlite3"]),
        new("executable/dex", ["application/x-dex"]),
        new("executable/elf", ["application/x-elf"]),
        new("executable/java_class", ["application/java-vm"]),
        new("executable/mach_o",
        [
            "application/x-mach-o", "application/x-mach-o-bundle", "application/x-mach-o-core", "application/x-mach-o-dsym",
            "application/x-mach-o-dylib", "application/x-mach-o-dylib-stub", "application/x-mach-o-dylinker",
            "application/x-mach-o-executable", "application/x-mach-o-fvmlib", "application/x-mach-o-kext-bundle",
            "application/x-mach-o-object", "application/x-mach-o-preload",
        ]),
        new("executable/mach_o_fat", ["application/x-mach-o-universal"]),
        new("executable/microsoft_pe", ["application/x-msdownload"]),
        new("executable/swf", ["application/x-shockwave-flash"]),
        new("filesystem/apple_single_double", ["application/applefile", "multipart/appledouble"]),
        new("filesystem/iso9660", ["application/x-iso9660-image"]),
        new("filesystem/vfat", ["application/x-fat-diskimage"]),
        new("firmware/ines", ["application/x-nesrom"]),
        new("font/ttf", ["application/x-font-ttf"]),
        new("geospatial/shapefile_main", ["application/x-shapefile"]),
        new("image/bmp", ["image/bmp"]),
        new("image/dicom", ["application/dicom"]),
        new("image/exif", ["image/tiff"]),
        new("image/gif", ["image/gif"]),
        new("image/icc_4", ["application/vnd.iccprofile"]),
        new("image/ico", ["image/vnd.microsoft.icon"]),
        new("image/jpeg", ["image/jpeg"]),
        new("image/nitf", ["image/nitf"]),
        new("image/pcx", ["image/vnd.zbrush.pcx"]),
        new("image/pcx_dcx", ["image/vnd.zbrush.dcx"]),
        new("image/png", ["image/png"]),
        new("image/tga", ["image/x-tga"]),
        new("image/webp", ["image/webp"]),
        new("image/wmf", ["image/wmf"]),
        new("media/au", ["audio/basic"]),
        new("media/avi", ["video/x-msvideo"]),
        new("media/mp3", ["audio/mpeg"]),
        new("media/ogg", ["application/ogg"]),
        new("media/quicktime_mov", ["application/quicktime", "video/3gpp", "video/3gpp2"]),
        new("media/standard_midi_file", ["audio/midi"]),
        new("media/wav", ["audio/vnd.wave"]),
        new("network/pcap", ["application/vnd.tcpdump.pcap"]),
        new("security/openpgp_message", ["application/pgp-encrypted"]),
        new("serialization/asn1/asn1_der", ["application/x-x509-cert;format=der", "application/x-x509-key;format=der"]),
        new("serialization/microsoft_cfb", ["application/x-tika-msoffice"]),
        new("windows/windows_lnk_file", ["application/x-ms-shortcut"]),
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
