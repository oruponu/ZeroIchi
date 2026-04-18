using Avalonia.Controls;
using Avalonia.Input.Platform;
using System.Threading.Tasks;
using ZeroIchi.Services;

namespace ZeroIchi.Infrastructure;

public class AvaloniaClipboardService(Window window) : IClipboardService
{
    public async Task<string?> GetTextAsync() =>
        window.Clipboard is { } cb ? await cb.TryGetTextAsync() : null;
}
