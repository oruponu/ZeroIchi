using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileTypes;
using ZeroIchi.Tests.Ksy.Model;

namespace ZeroIchi.Tests.Ksy;

public sealed class KsyFormatRegistryTests
{
    private static ByteBuffer Bytes(params byte[] data) => new ArrayByteBuffer(data);

    private static KsyFormat Format(string yaml) => KsyTestLoader.Load(yaml);

    [Fact]
    public void Magic_IsDerivedFromLeadingContents()
    {
        var format = Format("""
            meta:
              id: x
            seq:
              - id: a
                contents: [0x89, 'P']
              - id: b
                contents: 'NG'
              - id: c
                contents: [1]
                if: false
              - id: d
                contents: [2]
            """);

        Assert.Equal(new byte?[] { 0x89, 0x50, 0x4E, 0x47 }, format.Magic);
    }

    [Theory]
    [InlineData("  - id: a\n    type: u1\n")]
    [InlineData("  - id: a\n    contents: [1]\n    repeat: expr\n    repeat-expr: 2\n")]
    [InlineData("  - id: a\n    contents: [1]\n    process: xor(1)\n")]
    public void Magic_IsNullWithoutUsableLeadingContents(string seq)
    {
        Assert.Null(Format("meta:\n  id: x\nseq:\n" + seq).Magic);
    }

    [Fact]
    public void Magic_ExplicitMetaKeyWins()
    {
        var format = Format("meta:\n  id: x\n  -zeroichi-magic: 52 49 ?? 46\nseq:\n  - id: a\n    contents: 'ZZZZ'\n");

        Assert.Equal(new byte?[] { 0x52, 0x49, null, 0x46 }, format.Magic);
    }

    [Theory]
    [InlineData("'52 4'")]
    [InlineData("'zz'")]
    [InlineData("''")]
    [InlineData("[52]")]
    public void Magic_InvalidMetaKey_IsError(string value)
    {
        Assert.Contains("-zeroichi-magic", KsyTestLoader.SingleError($"meta:\n  id: x\n  -zeroichi-magic: {value}\n").Message);
    }

    private static MimeTypeEntry Type(string name, string? parent, string value) =>
        new(name, parent, [], [new MimeMagic(50, TikaMagicParser.Parse("string", "0", value, null, null, [], out _))]);

    private static Func<FileTypeDetector> Detector(params MimeTypeEntry[] types)
    {
        var detector = new FileTypeDetector(types);
        return () => detector;
    }

    private static ByteBuffer Text(string text) => new ArrayByteBuffer(Encoding.ASCII.GetBytes(text));

    private static KsyFormat Named(string id) => Format($"meta:\n  id: {id}\n");

    [Fact]
    public void Detect_UsesTheFormatMappedToTheDetectedType()
    {
        var registry = new KsyFormatRegistry(Detector(Type("x/a", null, "A")));
        var format = Named("a");
        registry.Register(format, "x/a");

        Assert.Same(format, registry.Detect(Text("A1")));
    }

    [Fact]
    public void Detect_DescendantTypeUsesTheAncestorsFormat()
    {
        var registry = new KsyFormatRegistry(Detector(Type("x/child", "x/parent", "PC"), Type("x/parent", null, "P")));
        var format = Named("parent");
        registry.Register(format, "x/parent");

        Assert.Same(format, registry.Detect(Text("PC")));
    }

    [Fact]
    public void Detect_NearestMappedAncestorWins()
    {
        var registry = new KsyFormatRegistry(Detector(Type("x/child", "x/parent", "PC"), Type("x/parent", null, "P")));
        var parent = Named("parent");
        var child = Named("child");
        registry.Register(parent, "x/parent");
        registry.Register(child, "x/child");

        Assert.Same(child, registry.Detect(Text("PC")));
        Assert.Same(parent, registry.Detect(Text("PX")));
    }

    [Fact]
    public void Detect_UnmappedOrUndetectedData_IsNull()
    {
        var registry = new KsyFormatRegistry(Detector(Type("x/a", null, "A"), Type("x/b", null, "B")));
        registry.Register(Named("a"), "x/a");

        Assert.Null(registry.Detect(Text("B")));
        Assert.Null(registry.Detect(Text("Q")));
        Assert.Null(registry.Detect(Bytes()));
    }

    [Fact]
    public void Detect_DefinitionMagicMustMatch()
    {
        var registry = new KsyFormatRegistry(Detector(Type("x/p", null, "PC")));
        var format = Format("meta:\n  id: p\nseq:\n  - id: m\n    contents: [0x50, 0x43, 0x44]\n");
        registry.Register(format, "x/p");

        Assert.Same(format, registry.Detect(Text("PCD")));
        Assert.Null(registry.Detect(Text("PCX")));
        Assert.Null(registry.Detect(Text("PC")));
    }

    [Fact]
    public void Detect_LoadsAFormatOnlyWhenSelectedAndOnlyOnce()
    {
        var loaded = new List<string>();
        var registry = new KsyFormatRegistry(Detector(Type("x/a", null, "A"), Type("x/b", null, "B")),
            [new KsyFormatRoot("a", ["x/a"]), new KsyFormatRoot("b", ["x/b"])],
            path =>
            {
                loaded.Add(path);
                return Named(path);
            });

        Assert.Empty(loaded);
        var first = registry.Detect(Text("A"));
        var second = registry.Detect(Text("A"));

        Assert.Equal(["a"], loaded);
        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void Detect_FormatThatFailsToLoad_IsNullAndIsNotRetried()
    {
        var loads = 0;
        var registry = new KsyFormatRegistry(Detector(Type("x/a", null, "A")), [new KsyFormatRoot("a", ["x/a"])],
            _ =>
            {
                loads++;
                return null;
            });

        Assert.Null(registry.Detect(Text("A")));
        Assert.Null(registry.Detect(Text("A")));
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Detect_ConcurrentFirstDetections_LoadTheFormatOnce()
    {
        var loads = 0;
        var format = Named("a");
        var registry = new KsyFormatRegistry(Detector(Type("x/a", null, "A")), [new KsyFormatRoot("a", ["x/a"])],
            _ =>
            {
                Interlocked.Increment(ref loads);
                Thread.Sleep(50);
                return format;
            });
        var results = new KsyFormat?[16];

        Parallel.For(0, results.Length, i => results[i] = registry.Detect(Text("A")));

        Assert.Equal(1, loads);
        Assert.All(results, result => Assert.Same(format, result));
    }
}
