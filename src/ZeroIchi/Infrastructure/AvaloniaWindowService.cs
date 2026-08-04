using Avalonia.Controls;
using ZeroIchi.Services;

namespace ZeroIchi.Infrastructure;

public sealed class AvaloniaWindowService(Window window) : IWindowService
{
    public void Close() => window.Close();
}
