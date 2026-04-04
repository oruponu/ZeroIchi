using Avalonia.Interactivity;

namespace ZeroIchi.Controls;

public class ByteModifiedEventArgs(RoutedEvent routedEvent, object source, int index, byte value)
    : RoutedEventArgs(routedEvent, source)
{
    public int Index { get; } = index;
    public byte Value { get; } = value;
}
