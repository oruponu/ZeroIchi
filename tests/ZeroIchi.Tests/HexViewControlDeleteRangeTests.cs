using Avalonia.Input;
using ZeroIchi.Controls;

namespace ZeroIchi.Tests;

public class HexViewControlDeleteRangeTests
{
    [Theory]
    // 仮想セルまで届く選択 → 実データ末尾までにクランプ
    [InlineData(Key.Delete, 10, 7, 16, 16, 10, 6)]
    // 最後のバイト + 仮想セルの選択 (最後のバイトで Shift+→) → 最後の1バイトにクランプ
    [InlineData(Key.Delete, 15, 2, 16, 16, 15, 1)]
    // 全データ + 仮想セルの選択 (先頭から Ctrl+Shift+End) → 全データにクランプ
    [InlineData(Key.Delete, 0, 17, 16, 16, 0, 16)]
    // 仮想セルを含まない通常の選択 → クランプされずそのまま
    [InlineData(Key.Delete, 2, 3, 5, 16, 2, 3)]
    // 選択ありの Backspace → Delete と同様に選択範囲を削除
    [InlineData(Key.Back, 10, 7, 16, 16, 10, 6)]
    // 選択なしの Delete → カーソル位置の1バイト
    [InlineData(Key.Delete, 0, 0, 5, 16, 5, 1)]
    // 選択なしの Backspace → カーソル手前の1バイト
    [InlineData(Key.Back, 0, 0, 5, 16, 4, 1)]
    public void GetDeleteRange_ReturnsClampedRange(
        Key key, int selectionStart, int selectionLength, int cursorPosition, int dataLength,
        int expectedIndex, int expectedCount)
    {
        var range = HexViewControl.GetDeleteRange(key, selectionStart, selectionLength, cursorPosition, dataLength);

        Assert.NotNull(range);
        Assert.Equal(expectedIndex, range.Value.Index);
        Assert.Equal(expectedCount, range.Value.Count);
    }

    [Theory]
    // 仮想セルのみの選択
    [InlineData(Key.Delete, 16, 1, 16, 16)]
    // 空ドキュメントでの選択 (Shift+矢印で SelectionLength = 1 になり得る)
    [InlineData(Key.Delete, 0, 1, 0, 0)]
    // 選択なしで仮想セル上の Delete
    [InlineData(Key.Delete, 0, 0, 16, 16)]
    // 選択なしで先頭の Backspace
    [InlineData(Key.Back, 0, 0, 0, 16)]
    public void GetDeleteRange_ReturnsNull_WhenNothingToDelete(
        Key key, int selectionStart, int selectionLength, int cursorPosition, int dataLength)
    {
        Assert.Null(HexViewControl.GetDeleteRange(key, selectionStart, selectionLength, cursorPosition, dataLength));
    }
}
