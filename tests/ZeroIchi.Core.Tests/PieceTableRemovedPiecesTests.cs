using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests;

public sealed class PieceTableRemovedPiecesTests
{
    [Fact]
    public void DeleteBytes_InsideOnePiece_RecordsOnlyDeletedRange()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]));

        var edit = table.DeleteBytes(3, 4);

        Assert.Equal([3, 4, 5, 6], table.ReadPieces(edit.RemovedPieces));
    }

    [Fact]
    public void DeleteBytes_AcrossAddPieces_RecordsBytesAsTheyWere()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3, 4, 5, 6, 7]));
        table.WriteByte(2, 0xAA);
        table.WriteByte(5, 0xBB);

        var edit = table.DeleteBytes(1, 6);

        Assert.Equal([1, 0xAA, 3, 4, 0xBB, 6], table.ReadPieces(edit.RemovedPieces));
    }

    [Fact]
    public void DeleteBytes_WholeTable_RecordsAllBytes()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2]));

        var edit = table.DeleteBytes(0, 3);

        Assert.Equal([0, 1, 2], table.ReadPieces(edit.RemovedPieces));
    }

    [Fact]
    public void ReadPieces_AfterLaterEdits_StillReturnsDeletedBytes()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3]));
        var edit = table.DeleteBytes(1, 2);
        table.InsertBytes(1, [0xCC]);

        Assert.Equal([1, 2], table.ReadPieces(edit.RemovedPieces));
    }

    [Fact]
    public void WriteByte_RecordsNoRemovedPieces()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2]));

        var edit = table.WriteByte(1, 0xFF);

        Assert.Empty(edit.RemovedPieces);
    }
}
