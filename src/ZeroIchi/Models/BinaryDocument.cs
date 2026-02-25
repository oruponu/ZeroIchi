using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models;

public class BinaryDocument
{
    private static ReadOnlySpan<byte> JournalMagic => "ZIJF"u8;
    private readonly record struct WritePlanEntry(Piece Piece, long OutputOffset);

    private ByteBuffer _original;
    private PieceTable _pieceTable;
    private PieceTableByteBuffer _buffer;

    public string FilePath { get; private set; }
    public string FileName { get; private set; }
    public ByteBuffer Buffer => _buffer;
    public long FileSize => _pieceTable.Length;
    public bool IsModified => _pieceTable.Length != _original.Length || _pieceTable.HasAddPieces;
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

    public static Task<BinaryDocument> OpenAsync(string path)
    {
        var journalPath = path + ".journal";
        if (File.Exists(journalPath))
            RecoverFromJournal(path, journalPath);

        return Task.FromResult(new BinaryDocument(path, new MappedByteBuffer(path)));
    }

    public PieceTableEdit WriteByte(long index, byte value) => _pieceTable.WriteByte(index, value);

    public PieceTableEdit AppendByte(byte value) => _pieceTable.AppendByte(value);

    public PieceTableEdit InsertBytes(long index, byte[] bytes) => _pieceTable.InsertBytes(index, bytes);

    public PieceTableEdit DeleteBytes(long index, long count) => _pieceTable.DeleteBytes(index, count);

    public void UndoEdit(PieceTableEdit edit) => _pieceTable.UndoEdit(edit);

    public bool IsByteModified(long index) => _pieceTable.IsModified(index);

    public Task SaveAsync()
    {
        if (!IsModified || IsNew) return Task.CompletedTask;

        var (entries, finalSize, needsShift) = ComputeWritePlan();
        var workBuffer = new byte[81920];

        // シフトがなければ非破壊な上書きのみなのでジャーナル不要
        string? journalPath = null;
        if (needsShift)
        {
            journalPath = FilePath + ".journal";
            WriteJournal(journalPath, entries, finalSize, workBuffer);
        }

        // マッピング中のファイルは上書きできないため先に解放する
        if (_original is MappedByteBuffer mapped)
            mapped.ReleaseMapping();

        using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            WritePiecesInPlace(fs, entries, finalSize, workBuffer, _pieceTable.ReadAddBuffer);
        }

        if (journalPath is not null)
            File.Delete(journalPath);

        _original.Dispose();
        ResetPieceTable(new MappedByteBuffer(FilePath));
        return Task.CompletedTask;
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

    internal void ReleaseData()
    {
        _original.Dispose();
        ResetPieceTable(new ArrayByteBuffer([]));
    }

    private void ResetPieceTable(ByteBuffer newOriginal)
    {
        _original = newOriginal;
        _pieceTable = new PieceTable(newOriginal);
        _buffer = new PieceTableByteBuffer(_pieceTable);
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

    // 右シフトを後ろから、左シフトを前から処理することで
    // 移動元のデータが上書きされる前に必ず読み出される
    private static void WritePiecesInPlace(
        FileStream fs, WritePlanEntry[] entries, long finalSize, byte[] workBuffer,
        Action<long, byte[], int, int> readAddBuffer)
    {
        if (finalSize > fs.Length)
            fs.SetLength(finalSize);

        for (var i = entries.Length - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Piece.Source == PieceSource.Original && entry.OutputOffset > entry.Piece.Offset)
                CopyWithinFile(fs, entry.Piece.Offset, entry.OutputOffset, entry.Piece.Length, workBuffer, backward: true);
        }

        foreach (var entry in entries)
        {
            if (entry.Piece.Source == PieceSource.Original && entry.OutputOffset < entry.Piece.Offset)
                CopyWithinFile(fs, entry.Piece.Offset, entry.OutputOffset, entry.Piece.Length, workBuffer, backward: false);
        }

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

    private static void CopyWithinFile(FileStream fs, long src, long dst, long length, byte[] buffer, bool backward)
    {
        if (backward)
        {
            var remaining = length;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(remaining, buffer.Length);
                remaining -= chunk;
                fs.Position = src + remaining;
                fs.ReadExactly(buffer, 0, chunk);
                fs.Position = dst + remaining;
                fs.Write(buffer, 0, chunk);
            }
        }
        else
        {
            long offset = 0;
            while (offset < length)
            {
                var chunk = (int)Math.Min(length - offset, buffer.Length);
                fs.Position = src + offset;
                fs.ReadExactly(buffer, 0, chunk);
                fs.Position = dst + offset;
                fs.Write(buffer, 0, chunk);
                offset += chunk;
            }
        }
    }

    private void WriteJournal(string journalPath, WritePlanEntry[] entries, long finalSize, byte[] workBuffer)
    {
        using var fs = new FileStream(journalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        Span<byte> buf = stackalloc byte[17];

        fs.Write(JournalMagic);

        BinaryPrimitives.WriteInt64LittleEndian(buf, finalSize);
        fs.Write(buf[..8]);

        BinaryPrimitives.WriteInt32LittleEndian(buf, entries.Length);
        fs.Write(buf[..4]);

        foreach (var entry in entries)
        {
            buf[0] = (byte)entry.Piece.Source;
            BinaryPrimitives.WriteInt64LittleEndian(buf[1..], entry.Piece.Offset);
            BinaryPrimitives.WriteInt64LittleEndian(buf[9..], entry.Piece.Length);
            fs.Write(buf);
        }

        var addLen = _pieceTable.AddBufferLength;
        BinaryPrimitives.WriteInt32LittleEndian(buf, addLen);
        fs.Write(buf[..4]);

        if (addLen > 0)
        {
            var remaining = addLen;
            var offset = 0;
            while (remaining > 0)
            {
                var chunk = Math.Min(remaining, workBuffer.Length);
                _pieceTable.ReadAddBuffer(offset, workBuffer, 0, chunk);
                fs.Write(workBuffer, 0, chunk);
                offset += chunk;
                remaining -= chunk;
            }
        }
    }

    private static void RecoverFromJournal(string filePath, string journalPath)
    {
        Piece[] pieces;
        long finalSize;
        byte[] addBuffer;

        try
        {
            using var jfs = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.None);
            Span<byte> buf = stackalloc byte[17];

            jfs.ReadExactly(buf[..4]);
            if (!buf[..4].SequenceEqual(JournalMagic))
                throw new InvalidDataException();

            jfs.ReadExactly(buf[..8]);
            finalSize = BinaryPrimitives.ReadInt64LittleEndian(buf);

            jfs.ReadExactly(buf[..4]);
            var pieceCount = BinaryPrimitives.ReadInt32LittleEndian(buf);

            pieces = new Piece[pieceCount];
            for (var i = 0; i < pieceCount; i++)
            {
                jfs.ReadExactly(buf);
                pieces[i] = new Piece(
                    (PieceSource)buf[0],
                    BinaryPrimitives.ReadInt64LittleEndian(buf[1..]),
                    BinaryPrimitives.ReadInt64LittleEndian(buf[9..]));
            }

            jfs.ReadExactly(buf[..4]);
            var addLen = BinaryPrimitives.ReadInt32LittleEndian(buf);

            addBuffer = new byte[addLen];
            if (addLen > 0)
                jfs.ReadExactly(addBuffer);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException)
        {
            // ジャーナル自体が壊れている場合はリカバリ不可
            File.Delete(journalPath);
            return;
        }

        var entries = new WritePlanEntry[pieces.Length];
        long outputOffset = 0;
        for (var i = 0; i < pieces.Length; i++)
        {
            entries[i] = new WritePlanEntry(pieces[i], outputOffset);
            outputOffset += pieces[i].Length;
        }

        using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var buffer = new byte[81920];
            WritePiecesInPlace(fs, entries, finalSize, buffer, ReadAdd);
        }

        File.Delete(journalPath);
        return;

        void ReadAdd(long offset, byte[] buf, int bufOffset, int count)
            => Array.Copy(addBuffer, (int)offset, buf, bufOffset, count);
    }
}
