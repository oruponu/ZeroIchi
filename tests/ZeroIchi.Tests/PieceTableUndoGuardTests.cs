using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests;

public class PieceTableUndoGuardTests
{
    [Fact]
    public void UndoEdit_EditFromAnotherTable_Throws()
    {
        var tableA = new PieceTable(new ArrayByteBuffer([1, 2, 3]));
        var tableB = new PieceTable(new ArrayByteBuffer([1, 2, 3]));
        var edit = tableA.WriteByte(1, 0xFF);

        Assert.Throws<InvalidOperationException>(() => tableB.UndoEdit(edit));
    }

    [Fact]
    public void UndoEdit_EditFromSameTable_RestoresContent()
    {
        var table = new PieceTable(new ArrayByteBuffer([1, 2, 3]));
        var edit = table.WriteByte(1, 0xFF);

        table.UndoEdit(edit);

        Assert.Equal(2, table.ReadByte(1));
    }

    [Fact]
    public void UndoEdit_StaleEditAfterLaterEdit_Throws()
    {
        var table = new PieceTable(new ArrayByteBuffer([1, 2, 3]));
        var first = table.WriteByte(0, 0xAA);
        table.WriteByte(1, 0xBB);

        Assert.Throws<InvalidOperationException>(() => table.UndoEdit(first));
    }

    [Fact]
    public void UndoEdit_StaleEditAfterUndoAndNewEdit_Throws()
    {
        var table = new PieceTable(new ArrayByteBuffer([1, 2, 3]));
        var first = table.WriteByte(0, 0xAA);
        table.UndoEdit(first);
        table.WriteByte(1, 0xBB);

        Assert.Throws<InvalidOperationException>(() => table.UndoEdit(first));
    }

    [Fact]
    public void UndoEdit_InReverseOrder_RestoresContent()
    {
        var table = new PieceTable(new ArrayByteBuffer([1, 2, 3]));
        var first = table.WriteByte(0, 0xAA);
        var second = table.WriteByte(1, 0xBB);

        table.UndoEdit(second);
        table.UndoEdit(first);

        Assert.Equal(1, table.ReadByte(0));
        Assert.Equal(2, table.ReadByte(1));
    }
}
