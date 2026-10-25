using System.Collections.Generic;
using System.Globalization;

namespace ZeroIchi.Models.FileStructure;

public enum FileStructureErrorKind
{
    Other,
    EndOfData,
    MissingTerminator,
    ContentsMismatch,
    ValidEq,
    ValidMin,
    ValidMax,
    ValidAnyOf,
    ValidInEnum,
    ValidExpr,
    EndianSwitch,
    Seek,
    Zlib,
    Native,
    NodeBudget,
    NestingLimit,
    LoadLimit,
    BytesLoadLimit,
    DecompressLimit,
    StackLimit,
}

public sealed record FileStructureError(FileStructureErrorKind Kind, IReadOnlyList<string> Arguments, string Message)
{
    public bool InConvertedData { get; init; }

    public static FileStructureError Other(string message) => new(FileStructureErrorKind.Other, [], message);

    public static FileStructureError EndOfData(long position, bool converted, long needed, long remaining,
        string message) =>
        new(FileStructureErrorKind.EndOfData, [Hex(position), Number(needed), Number(remaining)], message)
        {
            InConvertedData = converted,
        };

    public static FileStructureError MissingTerminator(string terminator, long position, bool converted,
        string message) =>
        new(FileStructureErrorKind.MissingTerminator, [terminator, Hex(position)], message)
        {
            InConvertedData = converted,
        };

    public static FileStructureError Seek(long position, long end, bool converted, string message) =>
        new(FileStructureErrorKind.Seek, [Hex(position), Hex(end)], message) { InConvertedData = converted };

    public static FileStructureError Limit(FileStructureErrorKind kind, long limit, string message) =>
        new(kind, [Number(limit)], message);

    public static FileStructureError BytesLoadLimit(long size, long limit, string message) =>
        new(FileStructureErrorKind.BytesLoadLimit, [Number(size), Number(limit)], message);

    private static string Hex(long value) => value.ToString("X", CultureInfo.InvariantCulture);

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
