using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
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

    [Fact]
    public void Detect_PrefersLongestMatchingMagic_ThenRegistrationOrder()
    {
        var registry = new KsyFormatRegistry();
        var shortMagic = Format("meta:\n  id: short_magic\nseq:\n  - id: m\n    contents: [1]\n");
        var longMagic = Format("meta:\n  id: long_magic\nseq:\n  - id: m\n    contents: [1, 2]\n");
        var sameLength = Format("meta:\n  id: same_length\nseq:\n  - id: m\n    contents: [1, 2]\n");
        registry.Register(shortMagic);
        registry.Register(longMagic);
        registry.Register(sameLength);

        Assert.Same(longMagic, registry.Detect(Bytes(1, 2, 3), null));
        Assert.Same(shortMagic, registry.Detect(Bytes(1, 9), null));
        Assert.Null(registry.Detect(Bytes(9), null));
    }

    [Fact]
    public void Detect_MagicLongerThanFile_DoesNotMatch()
    {
        var registry = new KsyFormatRegistry();
        registry.Register(Format("meta:\n  id: x\nseq:\n  - id: m\n    contents: [1, 2]\n"));

        Assert.Null(registry.Detect(Bytes(1), null));
    }

    [Fact]
    public void Detect_WildcardBytesMatchAnything()
    {
        var registry = new KsyFormatRegistry();
        var format = Format("meta:\n  id: x\n  -zeroichi-magic: 01 ?? 03\n");
        registry.Register(format);

        Assert.Same(format, registry.Detect(Bytes(1, 0xFF, 3), null));
    }

    [Fact]
    public void Detect_ExtensionIsUsedOnlyForFormatsWithoutMagic()
    {
        var registry = new KsyFormatRegistry();
        var withMagic = Format("meta:\n  id: with_magic\n  file-extension: bin\nseq:\n  - id: m\n    contents: [1]\n");
        var withoutMagic = Format("meta:\n  id: without_magic\n  file-extension: [dat, BIN]\n");
        registry.Register(withMagic);
        registry.Register(withoutMagic);

        Assert.Same(withoutMagic, registry.Detect(Bytes(9), @"C:\files\sample.bin"));
        Assert.Same(withoutMagic, registry.Detect(Bytes(9), "sample.DAT"));
        Assert.Same(withMagic, registry.Detect(Bytes(1), "sample.dat"));
        Assert.Null(registry.Detect(Bytes(9), "sample.txt"));
        Assert.Null(registry.Detect(Bytes(9), null));
    }

    [Fact]
    public void Formats_AreLoadedOnceAtTheFirstDetection()
    {
        var loads = 0;
        var format = Format("meta:\n  id: x\nseq:\n  - id: m\n    contents: [7]\n");
        var registry = new KsyFormatRegistry(() =>
        {
            loads++;
            return [format];
        });

        Assert.Equal(0, loads);
        Assert.Same(format, registry.Detect(Bytes(7), null));
        Assert.Null(registry.Detect(Bytes(8), null));
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Preload_LoadsTheFormatsOnlyOnce()
    {
        var loads = 0;
        var registry = new KsyFormatRegistry(() => { loads++; return []; });

        registry.Preload();
        registry.Detect(Bytes(1, 2), "a.bin");

        Assert.Equal(1, loads);
    }
}
