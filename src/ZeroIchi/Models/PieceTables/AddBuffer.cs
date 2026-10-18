using System;

namespace ZeroIchi.Models.PieceTables;

internal sealed class AddBuffer
{
    internal const int ChunkSize = 64 * 1024;

    private byte[][] _chunks = [];
    private int _chunkCount;

    public int Length { get; private set; }

    public byte this[long index] => View()[index];

    public void Append(byte value)
    {
        EnsureSpace();
        _chunks[Length / ChunkSize][Length % ChunkSize] = value;
        Length++;
    }

    public void Append(ReadOnlySpan<byte> bytes)
    {
        while (bytes.Length > 0)
        {
            EnsureSpace();
            var offset = Length % ChunkSize;
            var count = Math.Min(bytes.Length, ChunkSize - offset);
            bytes[..count].CopyTo(_chunks[Length / ChunkSize].AsSpan(offset, count));
            Length += count;
            bytes = bytes[count..];
        }
    }

    public void CopyTo(long offset, Span<byte> destination) => View().CopyTo(offset, destination);

    public AddBufferView View() => new(_chunks, Length);

    private void EnsureSpace()
    {
        if (Length < _chunkCount * ChunkSize) return;

        if (_chunkCount == _chunks.Length)
        {
            var grown = new byte[Math.Max(4, _chunks.Length * 2)][];
            Array.Copy(_chunks, grown, _chunkCount);
            _chunks = grown;
        }
        _chunks[_chunkCount++] = new byte[ChunkSize];
    }
}

internal readonly struct AddBufferView(byte[][] chunks, int length)
{
    public int Length => length;

    public byte this[long index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, length);
            return chunks[index / AddBuffer.ChunkSize][index % AddBuffer.ChunkSize];
        }
    }

    public void CopyTo(long offset, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + destination.Length, length);
        while (destination.Length > 0)
        {
            var chunkOffset = (int)(offset % AddBuffer.ChunkSize);
            var count = Math.Min(destination.Length, AddBuffer.ChunkSize - chunkOffset);
            chunks[offset / AddBuffer.ChunkSize].AsSpan(chunkOffset, count).CopyTo(destination);
            offset += count;
            destination = destination[count..];
        }
    }
}
