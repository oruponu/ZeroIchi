using System.Threading.Tasks;

namespace ZeroIchi.Services;

public interface IClipboardService
{
    Task SetBytesAsync(byte[] bytes, string? text);
    Task<byte[]?> TryGetBytesAsync();
    Task<string?> GetTextAsync();
}
