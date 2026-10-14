using System;
using System.Globalization;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

internal static class KsPrimitiveTypes
{
    public static long? SizeInBits(KsTypeRef type)
    {
        if (type.Path.Count != 1) return null;

        var name = StripEndian(type.Path[0]);
        return name switch
        {
            "u1" or "s1" => 8,
            "u2" or "s2" => 16,
            "u4" or "s4" or "f4" => 32,
            "u8" or "s8" or "f8" => 64,
            _ => TryParseBits(name, out var bits) ? bits : null,
        };
    }

    public static KsValueKind? KindOf(KsTypeRef type)
    {
        if (type.Path.Count != 1) return null;

        var raw = type.Path[0];
        switch (raw)
        {
            case "str" or "strz":
                return KsValueKind.String;
            case "bytes":
                return KsValueKind.Bytes;
            case "bool":
                return KsValueKind.Bool;
        }

        var name = StripEndian(raw);
        return name switch
        {
            "u1" or "u2" or "u4" or "u8" or "s1" or "s2" or "s4" or "s8" => KsValueKind.Int,
            "f4" or "f8" => KsValueKind.Float,
            "b1" => KsValueKind.Bool,
            _ => TryParseBits(name, out _) ? KsValueKind.Int : null,
        };
    }

    private static string StripEndian(string name) =>
        name.Length > 2 && (name.EndsWith("le", StringComparison.Ordinal) || name.EndsWith("be", StringComparison.Ordinal))
            ? name[..^2]
            : name;

    private static bool TryParseBits(string name, out int bits)
    {
        bits = 0;
        return name.Length > 1
            && name[0] == 'b'
            && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out bits)
            && bits is >= 1 and <= 64;
    }
}
