using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.PieceTables;

public class PieceTableByteBuffer(PieceTable pieceTable) : ByteBuffer
{
    public override long Length => pieceTable.Length;

    public override byte ReadByte(long index) => pieceTable.ReadByte(index);

    public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count) =>
        pieceTable.ReadBytes(offset, buffer, bufferOffset, count);
}
