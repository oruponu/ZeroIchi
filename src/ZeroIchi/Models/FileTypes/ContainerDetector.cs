using System;
using System.Collections.Generic;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileTypes;

internal static class ContainerDetector
{
    public static IReadOnlyList<MimeTypeEntry> ExtraTypes { get; } =
    [
        new("application/vnd.ms-appx", "application/zip", ["appx"], []),
    ];

    public static string? Detect(ByteBuffer buffer, Func<string, bool> isKnown) => ZipContainer.Detect(buffer, isKnown);

    internal static byte[]? Read(ByteBuffer buffer, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > buffer.Length) return null;
        return buffer.SliceToArray(offset, count);
    }
}
