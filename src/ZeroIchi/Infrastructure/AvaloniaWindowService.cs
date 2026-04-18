using Avalonia.Controls;
using ZeroIchi.Services;

namespace ZeroIchi.Infrastructure;

public class AvaloniaWindowService(Window window) : IWindowService
{
    public void Close() => window.Close();
}
