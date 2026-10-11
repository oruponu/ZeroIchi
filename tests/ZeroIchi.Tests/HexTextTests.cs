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
    [InlineData("hello")]
    [InlineData("10 2G")]
    [InlineData("100")]
    public void Parse_ReturnsNull_WhenAnyTokenIsNotAByte(string text)
    {
        Assert.Null(HexText.Parse(text));
    }
}
