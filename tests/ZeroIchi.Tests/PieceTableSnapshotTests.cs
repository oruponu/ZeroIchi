using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests;

public sealed class PieceTableSnapshotTests
{
    private static byte[] ReadAll(ByteBuffer buffer)
    {
        var bytes = new byte[buffer.Length];
        buffer.ReadBytes(0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] ReadEach(ByteBuffer buffer)
    {
        var bytes = new byte[buffer.Length];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = buffer.ReadByte(i);
        return bytes;
    }

    [Fact]
    public void Snapshot_KeepsTheContentAfterLaterEdits()
    {
        var table = new PieceTable(new ArrayByteBuffer([0, 1, 2, 3, 4, 5]));
        table.InsertBytes(2, [0xA, 0xB]);
        table.WriteByte(5, 0xC);
        byte[] expected = [0, 1, 0xA, 0xB, 2, 0xC, 4, 5];

        var snapshot = table.CreateSnapshot();
        table.WriteByte(0, 0xFF);
        table.DeleteBytes(1, 3);
        table.InsertBytes(0, [9, 9, 9]);

        Assert.Equal(expected.Length, snapshot.Length);
        Assert.Equal(expected, ReadAll(snapshot));
        Assert.Equal(expected, ReadEach(snapshot));
    }

    [Fact]
    public void Snapshot_KeepsReadingAfterTheAddBufferGrows()
    {
        var table = new PieceTable(new ArrayByteBuffer([1, 2]));
        table.InsertBytes(1, [7, 8, 9]);
        var snapshot = table.CreateSnapshot();

        table.InsertBytes(0, new byte[AddBuffer.ChunkSize * 3 + 5]);
        table.WriteByte(1, 0xEE);

        Assert.Equal([1, 7, 8, 9, 2], ReadAll(snapshot));
    }

    [Fact]
    public void Snapshot_ReadsAcrossChunkBoundaries()
    {
        var table = new PieceTable(new ArrayByteBuffer([0xAA]));
        var head = Enumerable.Range(0, AddBuffer.ChunkSize - 1).Select(i => (byte)i).ToArray();
        table.InsertBytes(1, head);
        table.InsertBytes(1 + head.Length, [0x11, 0x22, 0x33]);

        var snapshot = table.CreateSnapshot();

        var tail = new byte[5];
        snapshot.ReadBytes(snapshot.Length - 5, tail, 0, 5);
        Assert.Equal([.. head[^2..], 0x11, 0x22, 0x33], tail);
        Assert.Equal(0x22, snapshot.ReadByte(snapshot.Length - 2));
    }

    [Fact]
    public void Snapshot_OfAnEmptyTableIsEmpty()
    {
        var snapshot = new PieceTable(new ArrayByteBuffer([])).CreateSnapshot();

        Assert.Equal(0, snapshot.Length);
    }

    [Fact]
    public void Document_CreateSnapshot_ReadsTheCurrentContent()
    {
        var path = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        try
        {
            using var document = BinaryDocument.Open(path);
            document.InsertBytes(1, [9]);

            var snapshot = document.CreateSnapshot();
            document.WriteByte(0, 0xFF);

            Assert.Equal([1, 9, 2, 3], ReadAll(snapshot));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
