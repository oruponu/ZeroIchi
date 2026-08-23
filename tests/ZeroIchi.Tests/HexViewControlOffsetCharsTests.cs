using ZeroIchi.Controls;

namespace ZeroIchi.Tests;

public sealed class HexViewControlOffsetCharsTests
{
    [Theory]
    [InlineData(0L, 8)]
    [InlineData(1024L, 8)]
    [InlineData(0xFFFFFFFFL, 8)]
    [InlineData(0x100000000L, 10)]
    [InlineData(0xFFFFFFFFFFL, 10)]
    [InlineData(0x10000000000L, 12)]
    [InlineData(long.MaxValue, 16)]
    public void OffsetCharsFor_RoundsUpToEvenDigitsWithinBounds(long length, int expected)
    {
        Assert.Equal(expected, HexViewControl.OffsetCharsFor(length));
    }
}
