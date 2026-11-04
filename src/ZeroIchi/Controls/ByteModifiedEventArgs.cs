using Avalonia.Interactivity;
using ZeroIchi.Models;

namespace ZeroIchi.Controls;

public sealed class ByteModifiedEventArgs(
    RoutedEvent routedEvent, object source, long index, byte value, long cursorPositionAfter,
    bool isInsert, ByteInputPart part)
    : RoutedEventArgs(routedEvent, source)
{
    public long Index { get; } = index;
    public byte Value { get; } = value;
    public long CursorPositionAfter { get; } = cursorPositionAfter;
    public bool IsInsert { get; } = isInsert;
    public ByteInputPart Part { get; } = part;
}
