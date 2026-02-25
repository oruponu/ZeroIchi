using System.IO;
using System.Threading.Tasks;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models;

public class BinaryDocument
{
    private ByteBuffer _original;
    private PieceTable _pieceTable;
    private PieceTableByteBuffer _buffer;

    public string FilePath { get; private set; }
    public string FileName { get; private set; }
    public ByteBuffer Buffer => _buffer;
    public long FileSize => _pieceTable.Length;
    public bool IsModified => _pieceTable.HasAddPieces;
    public bool IsNew => FilePath == "";

    private BinaryDocument(string filePath, ByteBuffer original)
    {
        FilePath = filePath;
        FileName = filePath == "" ? "Untitled" : Path.GetFileName(filePath);
        _original = original;
        _pieceTable = new PieceTable(original);
        _buffer = new PieceTableByteBuffer(_pieceTable);
    }

    public static BinaryDocument CreateNew() => new("", new ArrayByteBuffer([]));

    public static Task<BinaryDocument> OpenAsync(string path) =>
        Task.FromResult(new BinaryDocument(path, new MappedByteBuffer(path)));

    public PieceTableEdit WriteByte(long index, byte value) => _pieceTable.WriteByte(index, value);

    public PieceTableEdit AppendByte(byte value) => _pieceTable.AppendByte(value);

    public PieceTableEdit InsertBytes(long index, byte[] bytes) => _pieceTable.InsertBytes(index, bytes);

    public PieceTableEdit DeleteBytes(long index, long count) => _pieceTable.DeleteBytes(index, count);

    public void UndoEdit(PieceTableEdit edit) => _pieceTable.UndoEdit(edit);

    public bool IsByteModified(long index) => _pieceTable.IsModified(index);

    internal void ReleaseData()
    {
        _original.Dispose();
        ResetPieceTable(new ArrayByteBuffer([]));
    }

    public async Task SaveAsync()
    {
        if (!IsModified || IsNew) return;

        var tempPath = FilePath + ".tmp";
        await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _pieceTable.WriteTo(fs);
        }

        // マッピング中のファイルは上書きできないため先に解放する
        if (_original is MappedByteBuffer mapped)
            mapped.ReleaseMapping();

        File.Move(tempPath, FilePath, overwrite: true);

        _original.Dispose();
        ResetPieceTable(new MappedByteBuffer(FilePath));
    }

    public async Task SaveAsAsync(string path)
    {
        await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _pieceTable.WriteTo(fs);
        }

        _original.Dispose();
        ResetPieceTable(new MappedByteBuffer(path));

        FilePath = path;
        FileName = Path.GetFileName(path);
    }

    private void ResetPieceTable(ByteBuffer newOriginal)
    {
        _original = newOriginal;
        _pieceTable = new PieceTable(newOriginal);
        _buffer = new PieceTableByteBuffer(_pieceTable);
    }
}
