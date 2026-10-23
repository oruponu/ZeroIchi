using ZeroIchi.Models.FileTypes;

namespace ZeroIchi.Tests.FileTypes;

public sealed class TikaMagicParserTests
{
    [Theory]
    [InlineData("\\x89PNG\\x0d\\x0a", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A })]
    [InlineData("0x4D5A", new byte[] { 0x4D, 0x5A })]
    [InlineData("a\\\\b", new byte[] { 0x61, 0x5C, 0x62 })]
    [InlineData("\\r\\n", new byte[] { 0x0D, 0x0A })]
    [InlineData("\\012", new byte[] { 0x0A })]
    public void String_DecodesLikeTika(string value, byte[] expected)
    {
        var match = Parse("string", "0", value);

        Assert.Equal(MagicMatchKind.Bytes, match.Kind);
        Assert.Equal(expected, match.Pattern);
    }

    [Fact]
    public void MissingType_IsString()
    {
        var match = Parse(null, "0", "BM");

        Assert.Equal(MagicMatchKind.Bytes, match.Kind);
        Assert.Equal("BM"u8.ToArray(), match.Pattern);
    }

    [Fact]
    public void MinShouldMatch_WithoutValue_IsAGroup()
    {
        MagicMatch[] children = [Parse("string", "0", "a"), Parse("string", "1", "b"), Parse("string", "2", "c")];

        var match = TikaMagicParser.Parse(null, null, null, null, "2", children, out _);

        Assert.Equal(MagicMatchKind.MinShouldMatch, match.Kind);
        Assert.Equal(2, match.MinShouldMatch);
        Assert.Equal(3, match.Children.Length);
    }

    [Fact]
    public void Offset_RangeIsInclusive()
    {
        var range = Parse("string", "4:8", "a");
        var single = Parse("string", "12", "a");
        var missing = Parse("string", null, "a");

        Assert.Equal((4, 8), (range.OffsetStart, range.OffsetEnd));
        Assert.Equal((12, 12), (single.OffsetStart, single.OffsetEnd));
        Assert.Equal((0, 0), (missing.OffsetStart, missing.OffsetEnd));
    }

    [Fact]
    public void Big32_IsBigEndian()
    {
        Assert.Equal(new byte[] { 0x00, 0x00, 0xFE, 0xFF }, Parse("big32", "0", "0x0000FEFF").Pattern);
    }

    [Fact]
    public void Little16_IsLittleEndian()
    {
        Assert.Equal(new byte[] { 0x34, 0x12 }, Parse("little16", "0", "0x1234").Pattern);
    }

    [Fact]
    public void Host32_IsLittleEndian()
    {
        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, Parse("host32", "0", "0x01020304").Pattern);
    }

    [Fact]
    public void Integer_WithoutHexPrefix_IsOctal()
    {
        Assert.Equal(new byte[] { 0x00, 0x08 }, Parse("big16", "0", "10").Pattern);
    }

    [Fact]
    public void Mask_IsAppliedToThePatternAndPadsIt()
    {
        var match = TikaMagicParser.Parse("string", "0", "0x0F", "0xF0FF", null, [], out _);

        Assert.Equal(new byte[] { 0x00, 0x00 }, match.Pattern);
        Assert.Equal(new byte[] { 0xF0, 0xFF }, match.Mask);
    }

    [Fact]
    public void NoMask_IsAllOnes()
    {
        var match = Parse("string", "0", "PK\\003\\004");

        Assert.Equal(match.Pattern.Length, match.Mask.Length);
        Assert.All(match.Mask, b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void StringIgnoreCase_LowercasesTheValue()
    {
        var match = Parse("stringignorecase", "0", "User-Agent:");

        Assert.Equal(MagicMatchKind.BytesIgnoreCase, match.Kind);
        Assert.Equal("user-agent:"u8.ToArray(), match.Pattern);
    }

    [Fact]
    public void UnicodeLE_EncodesTwoBytesPerChar()
    {
        Assert.Equal(new byte[] { 0x77, 0, 0x6D, 0, 0x76, 0, 0x32, 0 }, Parse("unicodeLE", "0", "wmv2").Pattern);
    }

    [Fact]
    public void Regex_KeepsThePatternAfterDecoding()
    {
        var match = Parse("regex", "0", "BZh[1-9]");

        Assert.Equal(MagicMatchKind.Regex, match.Kind);
        Assert.Equal("BZh[1-9]", match.Regex);
        Assert.False(match.RegexNeedsBacktracking);
    }

    [Fact]
    public void Regex_DoubleBackslashBecomesARegexEscape()
    {
        Assert.Equal("\\x02", Parse("regex", "0", "\\\\x02").Regex);
    }

    [Fact]
    public void Regex_WithLookbehind_NeedsBacktracking()
    {
        var match = Parse("regex", "0", "(?<=a)b");

        Assert.Equal(MagicMatchKind.Regex, match.Kind);
        Assert.True(match.RegexNeedsBacktracking);
    }

    [Fact]
    public void Regex_ThatDotNetRejects_IsUnsupported()
    {
        var match = TikaMagicParser.Parse("regex", "0", "a{2,1}", null, null, [], out var reason);

        Assert.Equal(MagicMatchKind.Unsupported, match.Kind);
        Assert.NotNull(reason);
    }

    [Fact]
    public void UnknownType_Throws()
    {
        Assert.Throws<FormatException>(() => Parse("unicodeBE", "0", "a"));
    }

    private static MagicMatch Parse(string? type, string? offset, string value)
    {
        var match = TikaMagicParser.Parse(type, offset, value, null, null, [], out var reason);
        Assert.Null(reason);
        return match;
    }
}
