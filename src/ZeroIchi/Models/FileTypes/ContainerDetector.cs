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
}
