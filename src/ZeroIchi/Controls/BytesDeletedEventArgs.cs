using Avalonia.Interactivity;

namespace ZeroIchi.Controls;

public sealed class BytesDeletedEventArgs(
    RoutedEvent routedEvent, object source, long index, long count, long cursorPositionAfter)
    : RoutedEventArgs(routedEvent, source)
{
    public long Index { get; } = index;
    public long Count { get; } = count;
    public long CursorPositionAfter { get; } = cursorPositionAfter;
}
