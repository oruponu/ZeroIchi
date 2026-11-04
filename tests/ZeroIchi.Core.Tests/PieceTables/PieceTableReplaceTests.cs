using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests.PieceTables;

public sealed class PieceTableReplaceTests
{
    private static byte[] ReadAll(PieceTable table)
    {
        var bytes = new byte[table.Length];
        table.ReadBytes(0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void ReplaceBytes_InsideOnePiece_ReplacesRangeAndRecordsRemovedBytes()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]));

        var edit = table.ReplaceBytes(3, 4, [0xAA, 0xBB]);

        Assert.Equal([0, 1, 2, 0xAA, 0xBB, 7, 8, 9], ReadAll(table));
        Assert.Equal(-2, edit.LengthDelta);
        Assert.Equal([3, 4, 5, 6], table.ReadPieces(edit.RemovedPieces));
    }

    [Fact]
    public void ReplaceBytes_AcrossAddPieces_RecordsBytesAsTheyWere()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3, 4, 5, 6, 7]));
        table.WriteByte(2, 0xAA);
        table.WriteByte(5, 0xBB);

        var edit = table.ReplaceBytes(1, 6, [0xCC, 0xDD, 0xEE]);

        Assert.Equal([0, 0xCC, 0xDD, 0xEE, 7], ReadAll(table));
        Assert.Equal([1, 0xAA, 3, 4, 0xBB, 6], table.ReadPieces(edit.RemovedPieces));
    }

    [Fact]
    public void ReplaceBytes_WholeTable_ReplacesEverything()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2]));

        table.ReplaceBytes(0, 3, [9]);

        Assert.Equal([9], ReadAll(table));
        Assert.Equal(1, table.Length);
    }

    [Fact]
    public void ReplaceBytes_ToEnd_KeepsLeftPart()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3]));

        table.ReplaceBytes(2, 2, [7, 8, 9]);

        Assert.Equal([0, 1, 7, 8, 9], ReadAll(table));
    }

    [Fact]
    public void ReplaceBytes_UndoEditThenRedoEdit_RestoresEachState()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3, 4, 5]));

        var edit = table.ReplaceBytes(1, 3, [0xAA]);
        table.UndoEdit(edit);

        Assert.Equal([0, 1, 2, 3, 4, 5], ReadAll(table));
        Assert.Equal(6, table.Length);

        table.RedoEdit(edit);

        Assert.Equal([0, 0xAA, 4, 5], ReadAll(table));
    }
}
