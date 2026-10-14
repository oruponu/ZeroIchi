using System;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

internal static class KsIntMath
{
    public static Int128 FloorDiv(Int128 a, Int128 b)
    {
        if (a == Int128.MinValue && b == Int128.NegativeOne) throw new OverflowException();

        var quotient = a / b;
        if (a % b != 0 && (a < 0) != (b < 0)) quotient--;
        return quotient;
    }

    public static Int128 FloorMod(Int128 a, Int128 b)
    {
        if (b == Int128.NegativeOne) return Int128.Zero;

        var remainder = a % b;
        if (remainder != 0 && (remainder < 0) != (b < 0)) remainder += b;
        return remainder;
    }

    public static Int128 ShiftLeft(Int128 value, Int128 count)
    {
        if (value == 0) return Int128.Zero;
        if (count >= 128) throw new OverflowException();

        var shift = (int)count;
        var result = value << shift;
        if (result >> shift != value) throw new OverflowException();
        return result;
    }

    public static Int128 ShiftRight(Int128 value, Int128 count) =>
        count >= 128 ? (value < 0 ? Int128.NegativeOne : Int128.Zero) : value >> (int)count;
}
