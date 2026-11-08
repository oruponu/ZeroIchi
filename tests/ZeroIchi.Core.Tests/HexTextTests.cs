using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class HexTextTests
{
    [Fact]
    public void Format_JoinsUppercaseHexPairsWithSpaces()
    {
        Assert.Equal("00 0A FF 10", HexText.Format([0x00, 0x0A, 0xFF, 0x10]));
    }

    [Fact]
    public void Format_SingleByte_HasNoSeparator()
    {
        Assert.Equal("7F", HexText.Format([0x7F]));
    }

    [Fact]
    public void Format_Empty_ReturnsEmptyString()
    {
        Assert.Equal("", HexText.Format([]));
    }

    [Fact]
    public void Parse_RoundTripsFormattedText()
    {
        byte[] bytes = [0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF];

        Assert.Equal(bytes, HexText.Parse(HexText.Format(bytes)));
    }

    [Theory]
    [InlineData("ff", new byte[] { 0xFF })]
    [InlineData("10 20", new byte[] { 0x10, 0x20 })]
    [InlineData("a\tb\r\nc", new byte[] { 0x0A, 0x0B, 0x0C })]
    public void Parse_AcceptsWhitespaceSeparatedHexTokens(string text, byte[] expected)
    {
        Assert.Equal(expected, HexText.Parse(text));
    }

    [Theory]
    [InlineData("DEADBEEF", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    [InlineData("deadbeef 01", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01 })]
    [InlineData("0010", new byte[] { 0x00, 0x10 })]
    public void Parse_SplitsUnseparatedHexIntoBytePairs(string text, byte[] expected)
    {
        Assert.Equal(expected, HexText.Parse(text));
    }

    [Theory]
    [InlineData("0xFF", new byte[] { 0xFF })]
    [InlineData("0XfF", new byte[] { 0xFF })]
    [InlineData("0xF", new byte[] { 0x0F })]
    [InlineData("0xDEADBEEF", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    [InlineData("0x10 0x20", new byte[] { 0x10, 0x20 })]
    public void Parse_AcceptsHexPrefix(string text, byte[] expected)
    {
        Assert.Equal(expected, HexText.Parse(text));
    }

    [Theory]
    [InlineData("de-ad-be-ef", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    [InlineData("de:ad:be:ef", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })]
    [InlineData("0xDE, 0xAD,0xBE", new byte[] { 0xDE, 0xAD, 0xBE })]
    public void Parse_AcceptsCommaHyphenAndColonSeparators(string text, byte[] expected)
    {
        Assert.Equal(expected, HexText.Parse(text));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("10 2G")]
    [InlineData("100")]
    [InlineData("DEADBEE")]
    [InlineData("0x")]
    [InlineData("0xABC")]
    [InlineData("0x0x10")]
    [InlineData("x10")]
    [InlineData("+10")]
    public void Parse_ReturnsNull_WhenAnyTokenIsNotAByte(string text)
    {
        Assert.Null(HexText.Parse(text));
    }
}
