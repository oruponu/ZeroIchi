using System.Buffers.Binary;
using System.Text;

namespace ZeroIchi.Tests;

internal sealed class ZipBuilder
{
    private const int DosDate = (2020 - 1980) << 9 | 1 << 5 | 1;

    private readonly List<byte> _files = [];
    private readonly List<byte> _central = [];
    private int _count;

    public List<long> BodyStarts { get; } = [];

    public ZipBuilder Add(string name, byte[] body, bool descriptor = false, bool signature = true, bool zip64 = false,
        uint crc = 0x12345678)
    {
        var offset = _files.Count;
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var saturated = zip64 && !descriptor;
        var extra = zip64 ? Zip64Extra(descriptor ? 0 : body.Length, descriptor ? 0 : body.Length) : [];
        var flags = (ushort)(descriptor ? 0x0008 : 0);

        Add(_files, U4(0x04034B50), U2(zip64 ? 45 : 20), U2(flags), U2(0), U2(0), U2(DosDate),
            U4(descriptor ? 0 : crc), U4(saturated ? 0xFFFFFFFF : descriptor ? 0 : (uint)body.Length),
            U4(saturated ? 0xFFFFFFFF : descriptor ? 0 : (uint)body.Length), U2(nameBytes.Length), U2(extra.Length),
            nameBytes, extra);
        BodyStarts.Add(_files.Count);
        _files.AddRange(body);
        if (descriptor)
        {
            if (signature) _files.AddRange(U4(0x08074B50));
            _files.AddRange(U4(crc));
            _files.AddRange(zip64 ? U8(body.Length) : U4((uint)body.Length));
            _files.AddRange(zip64 ? U8(body.Length) : U4((uint)body.Length));
        }

        var centralExtra = zip64 ? Zip64Extra(body.Length, body.Length, offset) : [];
        Add(_central, U4(0x02014B50), U2(zip64 ? 45 : 20), U2(zip64 ? 45 : 20), U2(flags), U2(0), U2(0), U2(DosDate),
            U4(crc), U4(zip64 ? 0xFFFFFFFF : (uint)body.Length), U4(zip64 ? 0xFFFFFFFF : (uint)body.Length),
            U2(nameBytes.Length), U2(centralExtra.Length), U2(0), U2(0), U2(0), U4(0),
            U4(zip64 ? 0xFFFFFFFF : (uint)offset), nameBytes, centralExtra);
        _count++;
        return this;
    }

    public byte[] Build(bool centralDirectory = true, bool zip64End = false)
    {
        var data = new List<byte>(_files);
        if (!centralDirectory) return [.. data];

        var centralOffset = data.Count;
        data.AddRange(_central);
        if (zip64End)
        {
            var record = data.Count;
            Add(data, U4(0x06064B50), U8(44), U2(45), U2(45), U4(0), U4(0), U8(_count), U8(_count),
                U8(_central.Count), U8(centralOffset));
            Add(data, U4(0x07064B50), U4(0), U8(record), U4(1));
        }
        Add(data, U4(0x06054B50), U2(0), U2(0), U2(zip64End ? 0xFFFF : _count), U2(zip64End ? 0xFFFF : _count),
            U4((uint)_central.Count), U4(zip64End ? 0xFFFFFFFF : (uint)centralOffset), U2(0));
        return [.. data];
    }

    private static byte[] Zip64Extra(params long[] values) =>
        [.. U2(0x0001), .. U2(values.Length * 8), .. values.SelectMany(U8)];

    private static void Add(List<byte> target, params byte[][] parts)
    {
        foreach (var part in parts) target.AddRange(part);
    }

    private static byte[] U2(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
        return bytes;
    }

    private static byte[] U4(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] U8(long value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes;
    }
}
