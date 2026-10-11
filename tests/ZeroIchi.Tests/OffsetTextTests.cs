using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class OffsetTextTests
{
    [Theory]
    [InlineData("80000000", true, 0x80000000L)]
    [InlineData("1A", true, 0x1AL)]
    [InlineData("1a", true, 0x1AL)]
    [InlineData("100", true, 0x100L)]
    [InlineData("0x1A", true, 0x1AL)]
    [InlineData("0X1A", false, 0x1AL)]
    [InlineData("100", false, 100L)]
    [InlineData("80000000", false, 80000000L)]
    public void TryParse_ParsesByRadix(string text, bool isHex, long expected)
    {
        Assert.True(OffsetText.TryParse(text, isHex, out var offset));
        Assert.Equal(expected, offset);
    }

    [Theory]
    [InlineData("1A", false)]
    [InlineData("G", true)]
    [InlineData("0x", true)]
    [InlineData("0x", false)]
    [InlineData("", true)]
    public void TryParse_RejectsInvalidText(string text, bool isHex)
    {
        Assert.False(OffsetText.TryParse(text, isHex, out _));
    }
}
