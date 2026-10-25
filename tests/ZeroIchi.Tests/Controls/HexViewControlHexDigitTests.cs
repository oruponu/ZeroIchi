using ZeroIchi.Controls;

namespace ZeroIchi.Tests.Controls;

public sealed class HexViewControlHexDigitTests
{
    [Theory]
    [InlineData('0', 0x0)]
    [InlineData('9', 0x9)]
    [InlineData('a', 0xA)]
    [InlineData('f', 0xF)]
    [InlineData('A', 0xA)]
    [InlineData('F', 0xF)]
    public void ParseHexDigit_ReturnsNibble(char c, int expected)
    {
        Assert.Equal(expected, HexViewControl.ParseHexDigit(c));
    }

    [Theory]
    [InlineData('(')]
    [InlineData('*')]
    [InlineData('g')]
    [InlineData('G')]
    [InlineData(' ')]
    // Full-width digit
    [InlineData('０')]
    public void ParseHexDigit_ReturnsNullForNonHexChar(char c)
    {
        Assert.Null(HexViewControl.ParseHexDigit(c));
    }
}
