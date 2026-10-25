using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy.Expressions;

public sealed class KsIntMathTests
{
    [Theory]
    [InlineData(7, 2, 3)]
    [InlineData(-7, 2, -4)]
    [InlineData(7, -2, -4)]
    [InlineData(-7, -2, 3)]
    [InlineData(6, 3, 2)]
    [InlineData(-6, 3, -2)]
    public void FloorDiv(long a, long b, long expected)
    {
        Assert.Equal((Int128)expected, KsIntMath.FloorDiv(a, b));
    }

    [Theory]
    [InlineData(-5, 3, 1)]
    [InlineData(5, -3, -1)]
    [InlineData(5, 3, 2)]
    [InlineData(-6, 3, 0)]
    public void FloorMod(long a, long b, long expected)
    {
        Assert.Equal((Int128)expected, KsIntMath.FloorMod(a, b));
    }

    [Fact]
    public void FloorDiv_MinValueByMinusOne_Overflows()
    {
        Assert.Throws<OverflowException>(() => KsIntMath.FloorDiv(Int128.MinValue, -1));
    }

    [Fact]
    public void FloorMod_ByMinusOne_IsZero()
    {
        Assert.Equal(Int128.Zero, KsIntMath.FloorMod(Int128.MinValue, -1));
    }

    [Fact]
    public void ShiftLeft_WithinRange()
    {
        Assert.Equal((Int128)16, KsIntMath.ShiftLeft(1, 4));
        Assert.Equal(Int128.MinValue, KsIntMath.ShiftLeft(-1, 127));
        Assert.Equal(Int128.Zero, KsIntMath.ShiftLeft(0, 1000));
    }

    [Theory]
    [InlineData(1, 127)]
    [InlineData(1, 128)]
    [InlineData(3, 126)]
    public void ShiftLeft_Overflow_Throws(long value, long count)
    {
        Assert.Throws<OverflowException>(() => KsIntMath.ShiftLeft(value, count));
    }

    [Fact]
    public void ShiftRight_IsArithmetic()
    {
        Assert.Equal((Int128)(-4), KsIntMath.ShiftRight(-16, 2));
        Assert.Equal(Int128.NegativeOne, KsIntMath.ShiftRight(-1, 500));
        Assert.Equal(Int128.Zero, KsIntMath.ShiftRight(5, 500));
    }
}
