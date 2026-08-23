using Avalonia.Interactivity;

namespace ZeroIchi.Controls;

public sealed class ByteModifiedEventArgs(
    RoutedEvent routedEvent, object source, long index, byte value, long cursorPositionAfter)
    : RoutedEventArgs(routedEvent, source)
{
    public long Index { get; } = index;
    public byte Value { get; } = value;
    public long CursorPositionAfter { get; } = cursorPositionAfter;
}
