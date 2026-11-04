using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests.PieceTables;

public sealed class PieceTableArgumentTests
{
    private static PieceTable Create() => new(new ArrayByteBuffer(new byte[16]));

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void DeleteBytes_WithoutAPositiveCount_Throws(long count)
    {
        var table = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.DeleteBytes(4, count));
        Assert.Equal(16, table.Length);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(10, 7)]
    [InlineData(16, 1)]
    public void DeleteBytes_OutsideTheData_Throws(long index, long count)
    {
        var table = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.DeleteBytes(index, count));
        Assert.Equal(16, table.Length);
    }

    [Fact]
    public void InsertBytes_WithNoBytes_Throws()
    {
        var table = Create();

        var exception = Assert.Throws<ArgumentException>(() => table.InsertBytes(2, []));

        Assert.Contains("must not be empty", exception.Message);
        Assert.Equal(16, table.Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(17)]
    public void InsertBytes_OutsideTheData_Throws(long index)
    {
        var table = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.InsertBytes(index, [1]));
        Assert.Equal(16, table.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void ReplaceBytes_WithoutAPositiveCount_Throws(long count)
    {
        var table = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.ReplaceBytes(4, count, [1]));
        Assert.Equal(16, table.Length);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(10, 7)]
    [InlineData(16, 1)]
    public void ReplaceBytes_OutsideTheData_Throws(long index, long count)
    {
        var table = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.ReplaceBytes(index, count, [1]));
        Assert.Equal(16, table.Length);
        Assert.Equal(0, table.AddBufferLength);
    }

    [Fact]
    public void ReplaceBytes_WithNoBytes_Throws()
    {
        var table = Create();

        var exception = Assert.Throws<ArgumentException>(() => table.ReplaceBytes(2, 1, []));

        Assert.Contains("must not be empty", exception.Message);
        Assert.Equal(16, table.Length);
    }
}
