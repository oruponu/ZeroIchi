using System;
using System.Buffers.Binary;
using ZeroIchi.Models.Buffers;
using static ZeroIchi.Models.FileTypes.ContainerDetector;

namespace ZeroIchi.Models.FileTypes;

internal static class PeContainer
{
    private const int PeOffsetPosition = 0x3C;
    private const int PeHeaderLength = 24;
    private const ushort DllFlag = 0x2000;

    public static string? Detect(ByteBuffer buffer)
    {
        var dosHeader = Read(buffer, 0, PeOffsetPosition + 4);
        if (dosHeader is null || dosHeader[0] != 'M' || dosHeader[1] != 'Z') return null;

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader.AsSpan(PeOffsetPosition));
        if (peOffset <= 0 || (long)peOffset + PeHeaderLength > MagicEvaluator.MagicReadLimit) return null;

        var peHeader = Read(buffer, peOffset, PeHeaderLength);
        if (peHeader is null || !peHeader.AsSpan(0, 4).SequenceEqual("PE\0\0"u8)) return null;

        var characteristics = BinaryPrimitives.ReadUInt16LittleEndian(peHeader.AsSpan(22));
        return (characteristics & DllFlag) != 0 ? "application/x-msdownload" : "application/x-dosexec";
    }
}
