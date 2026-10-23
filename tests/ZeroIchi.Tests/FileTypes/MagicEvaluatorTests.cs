using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class MagicEvaluatorTests
{
    private static MagicMatch Match(string offset, string value, string? mask = null, string type = "string",
        params MagicMatch[] children) =>
        TikaMagicParser.Parse(type, offset, value, mask, null, children, out _);

    private static bool Matches(MagicMatch match, byte[] data) =>
        new MagicEvaluator(new ArrayByteBuffer(data)).Matches(match);

    [Fact]
    public void Bytes_MatchAtTheOffset()
    {
        var match = Match("2", "PNG");

        Assert.True(Matches(match, [0x89, 0x00, (byte)'P', (byte)'N', (byte)'G']));
        Assert.False(Matches(match, "PNG\x00\x00"u8.ToArray()));
    }

    [Fact]
    public void Bytes_DoNotMatchWhenTheDataIsShorterThanThePattern()
    {
        Assert.False(Matches(Match("0", "PNG"), "PN"u8.ToArray()));
    }

    [Fact]
    public void Range_MatchesAnywhereFromStartToEndInclusive()
    {
        var match = Match("4:8", "AB");
        var atEight = new byte[12];
        atEight[8] = (byte)'A';
        atEight[9] = (byte)'B';
        var atNine = new byte[12];
        atNine[9] = (byte)'A';
        atNine[10] = (byte)'B';

        Assert.True(Matches(match, atEight));
        Assert.False(Matches(match, atNine));
    }

    [Fact]
    public void Mask_IsAppliedToTheData()
    {
        var match = Match("4", "0x01", "0x0F");

        Assert.True(Matches(match, [0, 0, 0, 0, 0x31]));
        Assert.False(Matches(match, [0, 0, 0, 0, 0x32]));
    }

    [Fact]
    public void IgnoreCase_FoldsAsciiOnly()
    {
        Assert.True(Matches(Match("0", "abc", type: "stringignorecase"), "ABC"u8.ToArray()));
        Assert.False(Matches(Match("0", "\\xE0", type: "stringignorecase"), [0xC0]));
    }

    [Fact]
    public void Child_RequiresTheParentAndAnyChild()
    {
        var match = Match("0", "AB", children: [Match("2", "X"), Match("2", "Y")]);

        Assert.True(Matches(match, "ABY"u8.ToArray()));
        Assert.False(Matches(match, "ABZ"u8.ToArray()));
        Assert.False(Matches(match, "CDY"u8.ToArray()));
    }

    [Fact]
    public void MinShouldMatch_CountsMatchingChildren()
    {
        var match = TikaMagicParser.Parse(null, null, null, null, "2",
            [Match("0", "A"), Match("1", "B"), Match("2", "C")], out _);

        Assert.False(Matches(match, "AXX"u8.ToArray()));
        Assert.True(Matches(match, "AXC"u8.ToArray()));
    }

    [Fact]
    public void UnsupportedChildren_DoNotLetTheParentMatchAlone()
    {
        var unsupported = TikaMagicParser.Parse("regex", "0", "a{2,1}", null, null, [], out _);
        var match = Match("0", "AB", children: [unsupported]);

        Assert.False(Matches(match, "AB"u8.ToArray()));
    }

    [Fact]
    public void RulesBeyondTheReadLimit_DoNotMatch()
    {
        var match = Match("70000", "X");
        var buffer = new SparseByteBuffer(200_000, (70000, "X"u8.ToArray()));

        Assert.False(new MagicEvaluator(buffer).Matches(match));
    }

    [Fact]
    public void Iso9660_AtOffset32769_Matches()
    {
        var buffer = new SparseByteBuffer(40_000, (32769, "CD001"u8.ToArray()));

        Assert.Equal("application/x-iso9660-image", FileTypeDetector.Default.Detect(buffer)?.MimeType);
    }
}
