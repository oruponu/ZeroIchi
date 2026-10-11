using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using System.Threading.Tasks;
using ZeroIchi.Services;

namespace ZeroIchi.Infrastructure;

public sealed class AvaloniaClipboardService(Window window) : IClipboardService
{
    private static readonly DataFormat<byte[]> BytesFormat = DataFormat.CreateBytesApplicationFormat("ZeroIchi.Bytes");

    public async Task SetBytesAsync(byte[] bytes, string? text)
    {
        if (window.Clipboard is not { } cb) return;

        var item = new DataTransferItem();
        item.Set(BytesFormat, bytes);
        if (text is not null)
            item.SetText(text);

        var data = new DataTransfer();
        data.Add(item);

        await cb.SetDataAsync(data);
        await cb.FlushAsync();
    }

    public async Task<byte[]?> TryGetBytesAsync() =>
        window.Clipboard is { } cb ? await cb.TryGetValueAsync(BytesFormat) : null;

    public async Task<string?> GetTextAsync() =>
        window.Clipboard is { } cb ? await cb.TryGetTextAsync() : null;
}
