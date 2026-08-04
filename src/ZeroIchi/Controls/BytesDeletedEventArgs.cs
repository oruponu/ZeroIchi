using Avalonia.Interactivity;

namespace ZeroIchi.Controls;

public sealed class BytesDeletedEventArgs(RoutedEvent routedEvent, object source, int index, int count)
    : RoutedEventArgs(routedEvent, source)
{
    public int Index { get; } = index;
    public int Count { get; } = count;
}
