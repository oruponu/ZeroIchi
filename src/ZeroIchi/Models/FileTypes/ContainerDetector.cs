using System;
using System.Collections.Generic;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileTypes;

internal static class ContainerDetector
{
    public static IReadOnlyList<MimeTypeEntry> ExtraTypes { get; } =
    [
        new("application/vnd.ms-appx", "application/zip", ["appx"], []),
        new("application/x-ms-shortcut", null, ["lnk"], []),
    ];

    public static string? Detect(ByteBuffer buffer, Func<string, bool> isKnown) =>
        ZipContainer.Detect(buffer, isKnown)
        ?? CfbContainer.Detect(buffer)
        ?? EbmlContainer.Detect(buffer)
        ?? PeContainer.Detect(buffer)
        ?? ShellLinkSignature.Detect(buffer);

    internal static byte[]? Read(ByteBuffer buffer, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > buffer.Length) return null;
        return buffer.SliceToArray(offset, count);
    }
}
