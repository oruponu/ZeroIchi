using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace ZeroIchi.Models.Buffers;

public sealed class MappedByteBuffer : ByteBuffer
{
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private readonly string _filePath;
    private readonly long _length;

    public MappedByteBuffer(string filePath)
    {
        _filePath = filePath;
        _length = new FileInfo(filePath).Length;

        // 空ファイルはメモリマップできないため、マップを持たない長さ0のバッファとして扱う
        if (_length == 0) return;

        CreateMapping();
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
        _accessor = null;
        _mmf = null;
    }

    // 失敗経路の catch から呼ばれるため、復元できない場合も例外を出さず解放済みのまま戻る
    public void RestoreMapping()
    {
        if (_length == 0 || _accessor is not null) return;

        var file = new FileInfo(_filePath);
        if (!file.Exists || file.Length < _length) return;

        try
        {
            CreateMapping();
        }
        catch
        {
        }
    }

    public override void Dispose()
    {
        ReleaseMapping();
        GC.SuppressFinalize(this);
    }

    private void CreateMapping()
    {
        var mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
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
