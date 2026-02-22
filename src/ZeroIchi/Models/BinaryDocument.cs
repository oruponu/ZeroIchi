using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZeroIchi.Models;

public class BinaryDocument
{
    public string FilePath { get; }
    public string FileName { get; }
    public byte[] Data { get; internal set; }
    public long FileSize => Data.Length;
    public HashSet<int> ModifiedIndices { get; } = [];
    internal bool StructurallyModified { get; set; }
    public bool IsModified => StructurallyModified || ModifiedIndices.Count > 0;

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

    public void DeleteBytes(int index, int count)
    {
        if (count <= 0 || index < 0 || index + count > Data.Length) return;

        var newData = new byte[Data.Length - count];
        Array.Copy(Data, 0, newData, 0, index);
        Array.Copy(Data, index + count, newData, index, Data.Length - index - count);

        var shifted = new List<int>();
        foreach (var i in ModifiedIndices)
        {
            if (i < index)
                shifted.Add(i);
            else if (i >= index + count)
                shifted.Add(i - count);
        }

        ModifiedIndices.Clear();
        ModifiedIndices.UnionWith(shifted);

        Data = newData;
        StructurallyModified = true;
    }

    public async Task SaveAsync()
    {
        await File.WriteAllBytesAsync(FilePath, Data);
        ModifiedIndices.Clear();
        StructurallyModified = false;
    }

    public async Task SaveAsAsync(string path)
    {
        await File.WriteAllBytesAsync(path, Data);
        ModifiedIndices.Clear();
        StructurallyModified = false;
    }
}
