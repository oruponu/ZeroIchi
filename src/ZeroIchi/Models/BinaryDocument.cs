using System.IO;
using System.Threading.Tasks;

namespace ZeroIchi.Models;

public class BinaryDocument
{
    public string FilePath { get; }
    public string FileName { get; }
    public byte[] Data { get; }
    public long FileSize => Data.Length;

    private BinaryDocument(string filePath, byte[] data)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Data = data;
    }

    public static async Task<BinaryDocument> OpenAsync(string path)
    {
        var data = await File.ReadAllBytesAsync(path);
        return new BinaryDocument(path, data);
    }
}
