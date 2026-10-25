using ZeroIchi.Controls;

namespace ZeroIchi.Tests.Controls;

public sealed class HexViewControlSelectionAnchorTests
{
    [Theory]
    [InlineData(0x80000000L, 0x80000000L, 1L, 0x80000000L)]
    [InlineData(9L, 5L, 5L, 5L)]
    [InlineData(5L, 5L, 5L, 9L)]
    [InlineData(0x110L, 0x110L, 4L, 0x113L)]
    [InlineData(16L, 16L, 0L, 16L)]
    [InlineData(0L, 0L, 0L, 0L)]
    [InlineData(16L, 10L, 7L, 10L)]
    public void GetSelectionAnchor_ReturnsEndOppositeToCursor(
        long cursorPosition, long selectionStart, long selectionLength, long expected)
    {
        Assert.Equal(expected, HexViewControl.GetSelectionAnchor(cursorPosition, selectionStart, selectionLength));
    }
}
