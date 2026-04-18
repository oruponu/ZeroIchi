using System.Threading.Tasks;

namespace ZeroIchi.Services;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
}
