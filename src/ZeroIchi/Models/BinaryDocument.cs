using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.Commands;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models;

public sealed class BinaryDocument : IDisposable
{
    private readonly record struct WritePlanEntry(Piece Piece, long OutputOffset);

    private ByteBuffer _original;
    private PieceTable _pieceTable;
    private PieceTableByteBuffer _buffer;
    private readonly UndoRedoManager _undoRedoManager = new();

    public string FilePath { get; private set; }
    public string FileName { get; private set; }
    public ByteBuffer Buffer => _buffer;
    public long FileSize => _pieceTable.Length;
    public bool IsModified => _pieceTable.Length != _original.Length || _pieceTable.HasAddPieces;
    public bool IsNew => FilePath == "";
    public bool IsUsable => _original is not MappedByteBuffer { IsMapped: false };
    public bool CanUndo => _undoRedoManager.CanUndo;
    public bool CanRedo => _undoRedoManager.CanRedo;
    internal ByteBuffer Original => _original;

    private BinaryDocument(string filePath, ByteBuffer original)
    {
        FilePath = filePath;
        FileName = filePath == "" ? "Untitled" : Path.GetFileName(filePath);
        _original = original;
        _pieceTable = new PieceTable(original);
        _buffer = new PieceTableByteBuffer(_pieceTable);
    }

    public static BinaryDocument CreateNew() => new("", new ArrayByteBuffer([]));

    public static BinaryDocument Open(string path)
    {
        var tempPath = path + ".zitmp";
        if (File.Exists(path))
        {
            TryDeleteTempFile(tempPath);
        }
        else if (File.Exists(tempPath))
        {
            File.Move(tempPath, path);
        }

        return new BinaryDocument(path, new MappedByteBuffer(path));
    }

    public PieceTableEdit WriteByte(long index, byte value) => _pieceTable.WriteByte(index, value);

    public PieceTableEdit AppendByte(byte value) => _pieceTable.AppendByte(value);

    public PieceTableEdit InsertBytes(long index, byte[] bytes) => _pieceTable.InsertBytes(index, bytes);

    public PieceTableEdit DeleteBytes(long index, long count) => _pieceTable.DeleteBytes(index, count);

    public void UndoEdit(PieceTableEdit edit) => _pieceTable.UndoEdit(edit);

    public void ExecuteCommand(IEditCommand command)
    {
        if (!ReferenceEquals(command.Document, this))
            throw new InvalidOperationException();

        _undoRedoManager.ExecuteCommand(command);
    }

    public IEditCommand? Undo() => _undoRedoManager.Undo();

    public IEditCommand? Redo() => _undoRedoManager.Redo();

    public bool IsByteModified(long index) => _pieceTable.IsModified(index);

    public void Save()
    {
        if (!IsModified || IsNew) return;

        var (entries, finalSize, needsShift) = ComputeWritePlan();
        if (needsShift)
            SaveByReplace();
        else
            SaveInPlace(entries, finalSize);

        var newOriginal = new MappedByteBuffer(FilePath);
        _original.Dispose();
        ResetPieceTable(newOriginal);
    }

    private void SaveInPlace(WritePlanEntry[] entries, long finalSize)
    {
        var workBuffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            // マッピング中のファイルは上書きできないため先に解放する
            if (_original is MappedByteBuffer mapped)
                mapped.ReleaseMapping();

            try
            {
                using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                WritePiecesInPlace(fs, entries, finalSize, workBuffer, _pieceTable.ReadAddBuffer);
            }
            catch
            {
                (_original as MappedByteBuffer)?.RestoreMapping();
                throw;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(workBuffer);
        }
    }

    private void SaveByReplace()
    {
        var tempPath = FilePath + ".zitmp";
        try
        {
            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            _pieceTable.WriteTo(fs);
            fs.Flush(flushToDisk: true);
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }

        if (_original is MappedByteBuffer mapped)
            mapped.ReleaseMapping();

        try
        {
            File.Replace(tempPath, FilePath, destinationBackupFileName: null);
        }
        catch
        {
            if (!File.Exists(FilePath))
            {
                File.Move(tempPath, FilePath);
            }
            else
            {
                (_original as MappedByteBuffer)?.RestoreMapping();
                TryDeleteTempFile(tempPath);
                throw;
            }
        }
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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

    public void Dispose()
    {
        _original.Dispose();
        ResetPieceTable(new ArrayByteBuffer([]));
    }

    private void ResetPieceTable(ByteBuffer newOriginal)
    {
        var newTable = new PieceTable(newOriginal);
        var newBuffer = new PieceTableByteBuffer(newTable);

        _undoRedoManager.Clear();
        _original = newOriginal;
        _pieceTable = newTable;
        _buffer = newBuffer;
    }

    private (WritePlanEntry[] Entries, long FinalSize, bool NeedsShift) ComputeWritePlan()
    {
        var pieces = _pieceTable.Pieces;
        var entries = new WritePlanEntry[pieces.Count];
        long outputOffset = 0;
        var needsShift = false;

        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            entries[i] = new WritePlanEntry(piece, outputOffset);
            if (piece.Source == PieceSource.Original && outputOffset != piece.Offset)
                needsShift = true;
            outputOffset += piece.Length;
        }

        return (entries, outputOffset, needsShift);
    }

    private static void WritePiecesInPlace(
        FileStream fs, WritePlanEntry[] entries, long finalSize, byte[] workBuffer,
        Action<long, byte[], int, int> readAddBuffer)
    {
        if (finalSize > fs.Length)
            fs.SetLength(finalSize);

        foreach (var entry in entries)
        {
            if (entry.Piece.Source != PieceSource.Add) continue;

            fs.Position = entry.OutputOffset;
            var remaining = entry.Piece.Length;
            var srcOffset = entry.Piece.Offset;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(remaining, workBuffer.Length);
                readAddBuffer(srcOffset, workBuffer, 0, chunk);
                fs.Write(workBuffer, 0, chunk);
                srcOffset += chunk;
                remaining -= chunk;
            }
        }

        if (finalSize < fs.Length)
            fs.SetLength(finalSize);
    }

}
