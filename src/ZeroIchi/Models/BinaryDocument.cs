using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZeroIchi.Models;

public class BinaryDocument
{
    public string FilePath { get; private set; }
    public string FileName { get; private set; }
    public ByteBuffer Buffer { get; private set; }
    public long FileSize => Buffer.Length;
    public HashSet<int> ModifiedIndices { get; } = [];
    internal bool StructurallyModified { get; set; }
    public bool IsModified => StructurallyModified || ModifiedIndices.Count > 0;

    public bool IsNew => FilePath == "";

    private BinaryDocument(string filePath, ByteBuffer buffer)
    {
        FilePath = filePath;
        FileName = filePath == "" ? "Untitled" : Path.GetFileName(filePath);
        Buffer = buffer;
    }

    public static BinaryDocument CreateNew() => new("", new ArrayByteBuffer([]));

    public static Task<BinaryDocument> OpenAsync(string path)
    {
        var buffer = new MappedByteBuffer(path);
        return Task.FromResult(new BinaryDocument(path, buffer));
    }

    public void WriteByte(int index, byte value)
    {
        if ((uint)index >= (uint)Buffer.Length) return;
        Buffer.WriteByte(index, value);
        ModifiedIndices.Add(index);
    }

    public void AppendByte(byte value)
    {
        EnsureMaterialized();
        var arr = ((ArrayByteBuffer)Buffer).Array;
        ((ArrayByteBuffer)Buffer).Array = [.. arr, value];
        ModifiedIndices.Add((int)Buffer.Length - 1);
    }

    public void InsertBytes(int index, byte[] bytes)
    {
        if (bytes.Length == 0 || index < 0 || index > Buffer.Length) return;
        EnsureMaterialized();

        var data = ((ArrayByteBuffer)Buffer).Array;
        var newData = new byte[data.Length + bytes.Length];
        Array.Copy(data, 0, newData, 0, index);
        Array.Copy(bytes, 0, newData, index, bytes.Length);
        Array.Copy(data, index, newData, index + bytes.Length, data.Length - index);

        var shifted = new List<int>();
        foreach (var i in ModifiedIndices)
        {
            if (i < index)
                shifted.Add(i);
            else
                shifted.Add(i + bytes.Length);
        }

        ModifiedIndices.Clear();
        ModifiedIndices.UnionWith(shifted);
        for (var i = 0; i < bytes.Length; i++)
            ModifiedIndices.Add(index + i);

        ((ArrayByteBuffer)Buffer).Array = newData;
        StructurallyModified = true;
    }

    public void DeleteBytes(int index, int count)
    {
        if (count <= 0 || index < 0 || index + count > Buffer.Length) return;
        EnsureMaterialized();

        var data = ((ArrayByteBuffer)Buffer).Array;
        var newData = new byte[data.Length - count];
        Array.Copy(data, 0, newData, 0, index);
        Array.Copy(data, index + count, newData, index, data.Length - index - count);

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

        ((ArrayByteBuffer)Buffer).Array = newData;
        StructurallyModified = true;
    }

    internal void EnsureMaterialized()
    {
        if (Buffer is MappedByteBuffer mapped)
        {
            Buffer = mapped.ToArrayByteBuffer();
            mapped.Dispose();
        }
    }

    internal void ReleaseData()
    {
        Buffer.Dispose();
        Buffer = new ArrayByteBuffer([]);
    }

    public async Task SaveAsync()
    {
        if (Buffer is MappedByteBuffer mapped)
        {
            if (!mapped.HasOverlay)
                return;

            var overlay = mapped.Overlay;
            mapped.ReleaseMapping();

            await using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                foreach (var (idx, val) in overlay)
                {
                    fs.Position = idx;
                    fs.WriteByte(val);
                }
            }

            mapped.Remap(FilePath);
        }
        else if (Buffer is ArrayByteBuffer arrayBuf)
        {
            await File.WriteAllBytesAsync(FilePath, arrayBuf.Array);
        }

        ModifiedIndices.Clear();
        StructurallyModified = false;
    }

    public async Task SaveAsAsync(string path)
    {
        if (Buffer is MappedByteBuffer mapped)
        {
            var length = (int)mapped.Length;
            var data = new byte[length];
            mapped.ReadBytes(0, data, 0, length);
            mapped.Dispose();

            await File.WriteAllBytesAsync(path, data);
            Buffer = new MappedByteBuffer(path);
        }
        else if (Buffer is ArrayByteBuffer arrayBuf)
        {
            await File.WriteAllBytesAsync(path, arrayBuf.Array);
        }

        FilePath = path;
        FileName = Path.GetFileName(path);
        ModifiedIndices.Clear();
        StructurallyModified = false;
    }
}
