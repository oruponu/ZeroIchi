using System;
using ZeroIchi.Models.Buffers;
using static ZeroIchi.Models.FileTypes.ContainerDetector;

namespace ZeroIchi.Models.FileTypes;

internal static class ShellLinkSignature
{
    private const int HeaderLength = 0x4C;

    private static readonly byte[] Signature =
    [
        0x4C, 0x00, 0x00, 0x00,
        0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46,
    ];

    public static string? Detect(ByteBuffer buffer)
    {
        if (buffer.Length < HeaderLength) return null;
        var header = Read(buffer, 0, Signature.Length);
        return header is not null && header.AsSpan().SequenceEqual(Signature) ? "application/x-ms-shortcut" : null;
    }
}
