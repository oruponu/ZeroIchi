using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace ZeroIchi.Models.Buffers;

public sealed class MappedByteBuffer : ByteBuffer
{
    private FileStream? _file;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private readonly string _filePath;
    private readonly long _length;

    public MappedByteBuffer(string filePath)
    {
        _filePath = filePath;
        _file = OpenFile(filePath);

        try
        {
            _length = _file.Length;
            CreateMapping();
        }
        catch
        {
            ReleaseMapping();
            throw;
        }
    }

    public override long Length => _length;

    public bool IsMapped => _length == 0 || _accessor is not null;

    public override byte ReadByte(long index)
    {
        ThrowIfUnmapped();
        return _accessor!.ReadByte(index);
    }

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
    {
        ThrowIfUnmapped();
        var available = (int)Math.Min(count, _length - offset);
        if (available <= 0) return;

        _accessor!.ReadArray(offset, buffer, bufferOffset, available);
    }

    public void ReleaseMapping()
    {
        _accessor?.Dispose();
        _mmf?.Dispose();
        _file?.Dispose();
        _accessor = null;
        _mmf = null;
        _file = null;
    }

    // Called from catch blocks on failure paths, so if the mapping cannot be restored, return without throwing and leave it released
    public void RestoreMapping()
    {
        if (_file is not null) return;

        try
        {
            _file = OpenFile(_filePath);
            if (_file.Length >= _length)
            {
                CreateMapping();
                return;
            }
        }
        catch
        {
        }

        ReleaseMapping();
    }

    public override void Dispose()
    {
        ReleaseMapping();
        GC.SuppressFinalize(this);
    }

    private static FileStream OpenFile(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0);

    private void CreateMapping()
    {
        // Empty files cannot be memory-mapped, so only the file handle is kept to lock the file
        if (_length == 0) return;

        var mmf = MemoryMappedFile.CreateFromFile(
            _file!, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        try
        {
            _accessor = mmf.CreateViewAccessor(0, _length, MemoryMappedFileAccess.Read);
            _mmf = mmf;
        }
        catch
        {
            mmf.Dispose();
            throw;
        }
    }

    private void ThrowIfUnmapped()
    {
        if (_length > 0 && _accessor is null)
            throw new ObjectDisposedException(nameof(MappedByteBuffer));
    }
}
