using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace ZeroIchi.Models;

public class MappedByteBuffer : ByteBuffer
{
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private readonly long _length;
    private readonly Dictionary<long, byte> _overlay = [];

    public MappedByteBuffer(string filePath)
    {
        _length = new FileInfo(filePath).Length;
        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _accessor = _mmf.CreateViewAccessor(0, _length, MemoryMappedFileAccess.Read);
    }

    public override long Length => _length;

    public bool HasOverlay => _overlay.Count > 0;

    public IReadOnlyDictionary<long, byte> Overlay => _overlay;

    public override byte ReadByte(long index) => _overlay.TryGetValue(index, out var value) ? value : _accessor!.ReadByte(index);

    public override void WriteByte(long index, byte value) => _overlay[index] = value;

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var available = (int)Math.Min(count, _length - offset);
        if (available <= 0) return;

        _accessor!.ReadArray(offset, buffer, bufferOffset, available);

        if (_overlay.Count > 0)
        {
            var end = offset + available;
            foreach (var (idx, val) in _overlay)
            {
                if (idx >= offset && idx < end)
                    buffer[bufferOffset + (int)(idx - offset)] = val;
            }
        }
    }

    public ArrayByteBuffer ToArrayByteBuffer()
    {
        var array = new byte[_length];
        _accessor!.ReadArray(0, array, 0, (int)_length);

        foreach (var (idx, val) in _overlay)
            array[idx] = val;

        return new ArrayByteBuffer(array);
    }

    public void ReleaseMapping()
    {
        _accessor?.Dispose();
        _mmf?.Dispose();
        _accessor = null;
        _mmf = null;
    }

    public void Remap(string filePath)
    {
        _overlay.Clear();
        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _accessor = _mmf.CreateViewAccessor(0, _length, MemoryMappedFileAccess.Read);
    }

    public override void Dispose()
    {
        ReleaseMapping();
        GC.SuppressFinalize(this);
    }
}
