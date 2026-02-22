using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZeroIchi.Models;

public class BinaryDocument
{
    public string FilePath { get; }
    public string FileName { get; }
    public byte[] Data { get; private set; }
    public long FileSize => Data.Length;
    public HashSet<int> ModifiedIndices { get; } = [];
    public bool IsModified => ModifiedIndices.Count > 0;

    public bool IsNew => FilePath == "";

    private BinaryDocument(string filePath, byte[] data)
    {
        FilePath = filePath;
        FileName = filePath == "" ? "Untitled" : Path.GetFileName(filePath);
        Data = data;
    }

    public static BinaryDocument CreateNew() => new("", []);

    public static async Task<BinaryDocument> OpenAsync(string path)
    {
        var data = await File.ReadAllBytesAsync(path);
        return new BinaryDocument(path, data);
    }

    public void WriteByte(int index, byte value)
    {
        if ((uint)index >= (uint)Data.Length) return;
        Data[index] = value;
        ModifiedIndices.Add(index);
    }

    public void AppendByte(byte value)
    {
        Data = [.. Data, value];
        ModifiedIndices.Add(Data.Length - 1);
    }

    public async Task SaveAsync()
    {
        await File.WriteAllBytesAsync(FilePath, Data);
        ModifiedIndices.Clear();
    }

    public async Task SaveAsAsync(string path)
    {
        await File.WriteAllBytesAsync(path, Data);
        ModifiedIndices.Clear();
    }
}
