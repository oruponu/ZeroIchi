using Avalonia.Interactivity;

namespace ZeroIchi.Controls;

public sealed class BytesDeletedEventArgs(
    RoutedEvent routedEvent, object source, int index, int count, int cursorPositionAfter)
    : RoutedEventArgs(routedEvent, source)
{
    public int Index { get; } = index;
    public int Count { get; } = count;
    public int CursorPositionAfter { get; } = cursorPositionAfter;
}
