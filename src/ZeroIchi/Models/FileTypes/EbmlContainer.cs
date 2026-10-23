using System;
using System.Text;
using ZeroIchi.Models.Buffers;
using static ZeroIchi.Models.FileTypes.ContainerDetector;

namespace ZeroIchi.Models.FileTypes;

internal static class EbmlContainer
{
    private const int MaxHeaderLength = 256;
    private const uint HeaderId = 0x1A45DFA3;
    private const uint DocTypeId = 0x4282;

    public static string? Detect(ByteBuffer buffer)
    {
        var data = Read(buffer, 0, (int)Math.Min(buffer.Length, MaxHeaderLength));
        if (data is null) return null;

        var position = 0;
        if (!TryReadId(data, ref position, out var id) || id != HeaderId) return null;
        if (!TryReadSize(data, ref position, out var headerSize)) return null;
        var end = (int)Math.Min(data.Length, position + headerSize);

        while (position < end)
        {
            if (!TryReadId(data, ref position, out id) || !TryReadSize(data, ref position, out var size)) return null;
            if (position + size > end) return null;
            if (id == DocTypeId)
            {
                return Encoding.ASCII.GetString(data, position, (int)size).TrimEnd('\0') switch
                {
                    "webm" => "video/webm",
                    "matroska" => "video/x-matroska",
                    _ => null,
                };
            }
            position += (int)size;
        }
        return null;
    }

    private static bool TryReadId(byte[] data, ref int position, out uint id)
    {
        id = 0;
        var length = VintLength(data, position);
        if (length is 0 or > 4 || position + length > data.Length) return false;
        for (var i = 0; i < length; i++) id = id << 8 | data[position + i];
        position += length;
        return true;
    }

    private static bool TryReadSize(byte[] data, ref int position, out long size)
    {
        size = 0;
        var length = VintLength(data, position);
        if (length == 0 || position + length > data.Length) return false;
        size = data[position] & (0xFF >> length);
        for (var i = 1; i < length; i++) size = size << 8 | data[position + i];
        position += length;
        return true;
    }

    private static int VintLength(byte[] data, int position)
    {
        if (position >= data.Length || data[position] == 0) return 0;
        return 8 - (31 - int.LeadingZeroCount(data[position]));
    }
}
