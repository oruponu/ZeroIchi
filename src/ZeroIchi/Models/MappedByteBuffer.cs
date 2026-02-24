using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace ZeroIchi.Models;

public class MappedByteBuffer : ByteBuffer
{
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private readonly long _length;

    public MappedByteBuffer(string filePath)
    {
        _length = new FileInfo(filePath).Length;
        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _accessor = _mmf.CreateViewAccessor(0, _length, MemoryMappedFileAccess.Read);
    }

    public override long Length => _length;

    public override byte ReadByte(long index) => _accessor!.ReadByte(index);

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var available = (int)Math.Min(count, _length - offset);
        if (available <= 0) return;

        _accessor!.ReadArray(offset, buffer, bufferOffset, available);
    }

    public void ReleaseMapping()
    {
        _accessor?.Dispose();
        _mmf?.Dispose();
        _accessor = null;
        _mmf = null;
    }

    public override void Dispose()
    {
        ReleaseMapping();
        GC.SuppressFinalize(this);
    }
}
