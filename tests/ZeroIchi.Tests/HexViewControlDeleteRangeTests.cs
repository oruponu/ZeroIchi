using Avalonia.Input;
using ZeroIchi.Controls;

namespace ZeroIchi.Tests;

public sealed class HexViewControlDeleteRangeTests
{
    [Theory]
    // Selection reaching the virtual cell → clamped to the end of the data
    [InlineData(Key.Delete, 10, 7, 16, 16, 10, 6)]
    // Last byte + virtual cell selected (Shift+→ on the last byte) → clamped to the last byte
    [InlineData(Key.Delete, 15, 2, 16, 16, 15, 1)]
    // All data + virtual cell selected (Ctrl+Shift+End from the start) → clamped to all data
    [InlineData(Key.Delete, 0, 17, 16, 16, 0, 16)]
    // Normal selection without the virtual cell → not clamped
    [InlineData(Key.Delete, 2, 3, 5, 16, 2, 3)]
    // Backspace with a selection → deletes the selection like Delete
    [InlineData(Key.Back, 10, 7, 16, 16, 10, 6)]
    // Delete without a selection → the byte at the cursor
    [InlineData(Key.Delete, 0, 0, 5, 16, 5, 1)]
    // Backspace without a selection → the byte before the cursor
    [InlineData(Key.Back, 0, 0, 5, 16, 4, 1)]
    // Deleting a selection beyond 2GB
    [InlineData(Key.Delete, 0x80000000L, 0x40000000L, 0x80000000L, 0xC0000000L, 0x80000000L, 0x40000000L)]
    // End of a 3GB file + virtual cell selected → clamped to the end of the data
    [InlineData(Key.Delete, 0xBFFFFFFFL, 2L, 0xC0000000L, 0xC0000000L, 0xBFFFFFFFL, 1L)]
    public void GetDeleteRange_ReturnsClampedRange(
        Key key, long selectionStart, long selectionLength, long cursorPosition, long dataLength,
        long expectedIndex, long expectedCount)
    {
        var range = HexViewControl.GetDeleteRange(key, selectionStart, selectionLength, cursorPosition, dataLength);

        Assert.NotNull(range);
        Assert.Equal(expectedIndex, range.Value.Index);
        Assert.Equal(expectedCount, range.Value.Count);
    }

    [Theory]
    // Only the virtual cell selected
    [InlineData(Key.Delete, 16, 1, 16, 16)]
    // Selection in an empty document (Shift+arrow can make SelectionLength = 1)
    [InlineData(Key.Delete, 0, 1, 0, 0)]
    // Delete on the virtual cell without a selection
    [InlineData(Key.Delete, 0, 0, 16, 16)]
    // Backspace at the start without a selection
    [InlineData(Key.Back, 0, 0, 0, 16)]
    // Only the virtual cell at the end of a 3GB file selected
    [InlineData(Key.Delete, 0xC0000000L, 1L, 0xC0000000L, 0xC0000000L)]
    public void GetDeleteRange_ReturnsNull_WhenNothingToDelete(
        Key key, long selectionStart, long selectionLength, long cursorPosition, long dataLength)
    {
        Assert.Null(HexViewControl.GetDeleteRange(key, selectionStart, selectionLength, cursorPosition, dataLength));
    }
}
