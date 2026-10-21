using ZeroIchi.Controls;

namespace ZeroIchi.Tests;

public sealed class HexViewControlSelectionRangeTests
{
    [Theory]
    [InlineData(2L, 5L, 16L, 2L, 4L)]
    [InlineData(5L, 2L, 16L, 2L, 4L)]
    [InlineData(10L, 16L, 16L, 10L, 6L)]
    [InlineData(16L, 10L, 16L, 10L, 6L)]
    [InlineData(15L, 16L, 16L, 15L, 1L)]
    [InlineData(16L, 16L, 16L, 16L, 0L)]
    [InlineData(0L, 16L, 16L, 0L, 16L)]
    [InlineData(0L, 0L, 0L, 0L, 0L)]
    public void GetSelectionRange_ExcludesTheCellPastTheEnd(
        long anchor, long current, long dataLength, long expectedStart, long expectedLength)
    {
        Assert.Equal((expectedStart, expectedLength), HexViewControl.GetSelectionRange(anchor, current, dataLength));
    }
}
