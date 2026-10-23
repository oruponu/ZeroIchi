using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class ZipContainerTests
{
    private const string WordMain = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private static DetectedFileType? Detect(byte[] data) => FileTypeDetector.Default.Detect(new ArrayByteBuffer(data));

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name, CompressionLevel.Optimal).Open());
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    private static string ContentTypes(string main) =>
        $"""<?xml version="1.0"?><Types><Override PartName="/main.xml" ContentType="{main}"/></Types>""";

    private static byte[] Docx() =>
        Zip(("[Content_Types].xml", ContentTypes(WordMain)), ("_rels/.rels", "<Relationships/>"), ("word/document.xml", "<w/>"));

    private static void AssertDetected(byte[] data, string mimeType, string extension)
    {
        var detected = Detect(data);

        Assert.Equal(mimeType, detected?.MimeType);
        Assert.Equal(extension, detected?.Extension);
    }

    private static void AssertFallsBackToZip(byte[] data)
    {
        var detected = Detect(data);

        Assert.NotNull(detected);
        Assert.True(FileTypeDetector.Default.IsA(detected.MimeType, "application/zip"), detected.MimeType);
        Assert.Equal("zip", detected.Extension);
    }

    [Fact]
    public void Docx_IsDetected()
    {
        AssertDetected(Docx(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx");
    }

    [Fact]
    public void Xlsx_IsDetected()
    {
        var data = Zip(("[Content_Types].xml",
            ContentTypes("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));

        AssertDetected(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx");
    }

    [Fact]
    public void Xlsm_IsDetected()
    {
        var data = Zip(("[Content_Types].xml", ContentTypes("application/vnd.ms-excel.sheet.macroEnabled.main+xml")));

        AssertDetected(data, "application/vnd.ms-excel.sheet.macroenabled.12", "xlsm");
    }

    [Fact]
    public void Pptx_IsDetected()
    {
        var data = Zip(("[Content_Types].xml",
            ContentTypes("application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml")));

        AssertDetected(data, "application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx");
    }

    [Fact]
    public void Jar_IsDetected()
    {
        var data = Zip(("META-INF/MANIFEST.MF", "Manifest-Version: 1.0\n"), ("App.class", "x"));

        AssertDetected(data, "application/java-archive", "jar");
    }

    [Fact]
    public void Apk_IsDetected()
    {
        var data = Zip(("META-INF/MANIFEST.MF", "Manifest-Version: 1.0\n"), ("AndroidManifest.xml", "x"));

        AssertDetected(data, "application/vnd.android.package-archive", "apk");
    }

    [Theory]
    [InlineData("application/vnd.oasis.opendocument.text", "odt")]
    [InlineData("application/vnd.oasis.opendocument.spreadsheet", "ods")]
    [InlineData("application/vnd.oasis.opendocument.presentation", "odp")]
    [InlineData("application/vnd.oasis.opendocument.base", "odb")]
    [InlineData("application/epub+zip", "epub")]
    public void MimetypeEntry_IsDetected(string mime, string extension)
    {
        var data = new ZipBuilder()
            .Add("mimetype", Encoding.ASCII.GetBytes(mime))
            .Add("content.xml", "<x/>"u8.ToArray())
            .Build();

        AssertDetected(data, mime, extension);
    }

    [Fact]
    public void Appx_IsDetected()
    {
        var data = Zip(("AppxManifest.xml", "<Package/>"), ("[Content_Types].xml", ContentTypes("application/xml")));

        AssertDetected(data, "application/vnd.ms-appx", "appx");
    }

    [Fact]
    public void PlainZip_StaysZip()
    {
        AssertDetected(Zip(("readme.txt", "hello"), ("data/a.bin", "a")), "application/zip", "zip");
    }

    [Fact]
    public void EntriesAreFoundRegardlessOfOrder()
    {
        var data = Zip(("word/document.xml", "<w/>"), ("_rels/.rels", "<Relationships/>"),
            ("[Content_Types].xml", ContentTypes(WordMain)));

        AssertDetected(data, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx");
    }

    [Fact]
    public void ContentTypesOverTheExpandedLimit_FallsBackToZip()
    {
        var data = Zip(("[Content_Types].xml", new string(' ', 300 * 1024) + ContentTypes(WordMain)));

        AssertFallsBackToZip(data);
    }

    [Fact]
    public void CentralDirectoryPointingOutside_FallsBackToZip()
    {
        var data = Docx();
        var end = data.AsSpan().LastIndexOf("PK\x05\x06"u8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(end + 16), (uint)data.Length + 100);

        AssertFallsBackToZip(data);
    }

    [Fact]
    public void Zip64EndRecord_FallsBackToZip()
    {
        var builder = new ZipBuilder().Add("[Content_Types].xml", Encoding.ASCII.GetBytes(ContentTypes(WordMain)));

        AssertDetected(builder.Build(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx");
        AssertFallsBackToZip(builder.Build(zip64End: true));
    }

    [Fact]
    public void TruncatedZip_FallsBackToZip()
    {
        var docx = Docx();

        AssertFallsBackToZip(docx[..(docx.Length / 2)]);
    }
}
