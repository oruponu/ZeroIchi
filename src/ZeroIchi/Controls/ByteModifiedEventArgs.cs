using Avalonia.Interactivity;

namespace ZeroIchi.Controls;

public sealed class ByteModifiedEventArgs(
    RoutedEvent routedEvent, object source, int index, byte value, int cursorPositionAfter)
    : RoutedEventArgs(routedEvent, source)
{
    public int Index { get; } = index;
    public byte Value { get; } = value;
    public int CursorPositionAfter { get; } = cursorPositionAfter;
}
