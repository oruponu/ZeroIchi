using System;
using System.Buffers.Binary;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

public sealed partial class KsStream : IKsStream
{
    private readonly ByteBuffer? _buffer;
    private readonly byte[]? _array;
    private readonly long _base;
    private byte[]? _scratch;
    private ulong _bits;
    private int _bitsLeft;

    private KsStream(ByteBuffer? buffer, byte[]? array, long @base, long size)
    {
        _buffer = buffer;
        _array = array;
        _base = @base;
        Size = size;
    }

    public static KsStream FromBuffer(ByteBuffer buffer) => new(buffer, null, 0, buffer.Length);

    public static KsStream FromBytes(byte[] data) => new(null, data, 0, data.Length);

    public long Size { get; }
    public long Position { get; private set; }
    public bool IsEof => _bitsLeft == 0 && Position >= Size;
    public bool IsFileBacked => _buffer is not null;

    internal long BitStart => _bitsLeft > 0 ? Position - 1 : Position;

    public long FileOffset(long position) => _base + position;

    public KsStream Substream(long start, long length) => new(_buffer, _array, _base + start, length);

    public void Seek(long position)
    {
        if (position < 0 || position > Size)
            throw new KsyParseException($"Cannot seek to position {position} (stream size {Size})");
        AlignToByte();
        Position = position;
    }

    public void AlignToByte()
    {
        _bits = 0;
        _bitsLeft = 0;
    }

    internal State SaveState() => new(Position, _bits, _bitsLeft);

    internal void RestoreState(State state)
    {
        Position = state.Position;
        _bits = state.Bits;
        _bitsLeft = state.BitsLeft;
    }

    internal readonly record struct State(long Position, ulong Bits, int BitsLeft);

    public Int128 ReadInteger(int size, bool signed, bool bigEndian)
    {
        AlignToByte();
        var bytes = Take(size);
        ulong raw = size switch
        {
            1 => bytes[0],
            2 => bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            4 => bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            8 => bigEndian ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
        if (!signed) return raw;

        return size switch
        {
            1 => (sbyte)raw,
            2 => (short)raw,
            4 => (int)raw,
            _ => (long)raw,
        };
    }

    public double ReadFloat(int size, bool bigEndian)
    {
        AlignToByte();
        var bytes = Take(size);
        return size switch
        {
            4 => bigEndian ? BinaryPrimitives.ReadSingleBigEndian(bytes) : BinaryPrimitives.ReadSingleLittleEndian(bytes),
            8 => bigEndian ? BinaryPrimitives.ReadDoubleBigEndian(bytes) : BinaryPrimitives.ReadDoubleLittleEndian(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    public ulong ReadBits(int width, bool bigEndian)
    {
        if (width is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(width));

        UInt128 acc = _bits;
        var total = _bitsLeft;
        var needed = width - _bitsLeft;
        if (needed > 0)
        {
            var bytes = Take((needed + 7) / 8);
            foreach (var b in bytes)
            {
                acc = bigEndian ? acc << 8 | b : acc | (UInt128)b << total;
                total += 8;
            }
        }

        var rest = total - width;
        ulong result;
        if (bigEndian)
        {
            result = (ulong)(acc >> rest);
            _bits = (ulong)(acc & ((UInt128.One << rest) - 1));
        }
        else
        {
            result = (ulong)(acc & ((UInt128.One << width) - 1));
            _bits = (ulong)(acc >> width);
        }
        _bitsLeft = rest;
        return result;
    }

    internal void Require(long count)
    {
        if (count > Size - Position)
        {
            throw new KsyParseException(
                $"Unexpected end of stream: needed {count} byte(s) at position {Position}, but only {Math.Max(0, Size - Position)} remain");
        }
    }

    internal void CopyTo(long position, byte[] destination, int offset, int count)
    {
        if (_array is not null)
            Array.Copy(_array, _base + position, destination, offset, count);
        else
            _buffer!.ReadBytes(_base + position, destination, offset, count);
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        Require(count);
        _scratch ??= new byte[8];
        CopyTo(Position, _scratch, 0, count);
        Position += count;
        return _scratch.AsSpan(0, count);
    }
}
