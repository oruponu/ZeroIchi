using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace ZeroIchi.Models.Buffers;

// Reads the file without mapping it, so a full sequential read does not fill the process working set
public sealed class FileByteBuffer : ByteBuffer
{
    private readonly SafeFileHandle _handle;
    private readonly long _length;

    public FileByteBuffer(string filePath)
    {
        _handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
        try
        {
            _length = RandomAccess.GetLength(_handle);
        }
        catch
        {
            _handle.Dispose();
            throw;
        }
    }

    public override long Length => _length;

    public override byte ReadByte(long index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length);
        Span<byte> value = stackalloc byte[1];
        RandomAccess.Read(_handle, value, index);
        return value[0];
    }

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        var available = (int)Math.Min(count, _length - offset);
        var read = 0;
        while (read < available)
        {
            var n = RandomAccess.Read(_handle, buffer.AsSpan(bufferOffset + read, available - read), offset + read);
            if (n == 0) break;
            read += n;
        }
    }

    public override void Dispose()
    {
        _handle.Dispose();
        GC.SuppressFinalize(this);
    }
}
