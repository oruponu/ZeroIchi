using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.Commands;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models;

public sealed class BinaryDocument : IDisposable
{
    private readonly record struct WritePlanEntry(Piece Piece, long OutputOffset);
    private readonly record struct OverwrittenRange(long Offset, byte[] Bytes);
    private readonly record struct HistorySavePlan(IEditCommand? DiscardThrough, Dictionary<IEditCommand, byte[]> RetainedBytes);

    private ByteBuffer _original;
    private PieceTable _pieceTable;
    private PieceTableByteBuffer _buffer;
    private readonly UndoRedoManager _undoRedoManager = new();

    public event EventHandler? Changed;

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
    internal Func<Stream, Stream>? InPlaceStreamWrapper { get; set; }
    internal long UndoRetentionLimit { get; set; } = 64L * 1024 * 1024;
    internal PieceTable CurrentPieceTable => _pieceTable;

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
        if (File.Exists(path))
            path = File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;

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

    internal byte[] ReadPieces(IReadOnlyList<Piece> pieces) => _pieceTable.ReadPieces(pieces);

    public void ExecuteCommand(IEditCommand command)
    {
        if (!ReferenceEquals(command.Document, this))
            throw new InvalidOperationException();

        _undoRedoManager.ExecuteCommand(command);
        OnChanged();
    }

    public IEditCommand? Undo() => RaiseChangedIfApplied(_undoRedoManager.Undo());

    public IEditCommand? Redo() => RaiseChangedIfApplied(_undoRedoManager.Redo());

    private IEditCommand? RaiseChangedIfApplied(IEditCommand? command)
    {
        if (command is not null)
            OnChanged();
        return command;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public bool IsByteModified(long index) => _pieceTable.IsModified(index);

    public void Save()
    {
        if (!IsModified || IsNew) return;

        var historyPlan = PlanHistoryForSave();
        var (entries, finalSize, needsShift) = ComputeWritePlan();
        if (needsShift)
            SaveByReplace();
        else
            SaveInPlace(entries, finalSize);

        var newOriginal = new MappedByteBuffer(FilePath);
        _original.Dispose();
        ResetPieceTable(newOriginal);
        CommitHistoryAfterSave(historyPlan);
        OnChanged();
    }

    private void SaveInPlace(WritePlanEntry[] entries, long finalSize)
    {
        var originalLength = _original.Length;
        var overwrittenRanges = ReadOverwrittenRanges(entries);
        var workBuffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            // A mapped file cannot be overwritten, so release the mapping first
            if (_original is MappedByteBuffer mapped)
                mapped.ReleaseMapping();

            var historyInvalidated = false;
            try
            {
                using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize: 0);
                var stream = InPlaceStreamWrapper?.Invoke(fs) ?? fs;
                try
                {
                    WritePiecesInPlace(stream, entries, finalSize, workBuffer, _pieceTable.ReadAddBuffer);
                    fs.Flush(flushToDisk: true);
                }
                catch
                {
                    historyInvalidated = !TryRestoreOverwrittenRanges(stream, fs, overwrittenRanges, originalLength);
                    throw;
                }
            }
            catch
            {
                (_original as MappedByteBuffer)?.RestoreMapping();
                if (historyInvalidated)
                {
                    _undoRedoManager.Clear();
                    if (IsUsable)
                        OnChanged();
                }
                throw;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(workBuffer);
        }
    }

    private List<OverwrittenRange> ReadOverwrittenRanges(WritePlanEntry[] entries)
    {
        var ranges = new List<OverwrittenRange>();
        foreach (var entry in entries)
        {
            if (entry.Piece.Source != PieceSource.Add || entry.OutputOffset >= _original.Length) continue;

            var length = (int)Math.Min(entry.Piece.Length, _original.Length - entry.OutputOffset);
            ranges.Add(new OverwrittenRange(entry.OutputOffset, _original.SliceToArray(entry.OutputOffset, length)));
        }
        return ranges;
    }

    private static bool TryRestoreOverwrittenRanges(
        Stream stream, FileStream file, List<OverwrittenRange> ranges, long originalLength)
    {
        try
        {
            // Bytes cut off by truncation are not backed up, so they cannot be restored
            if (stream.Length < originalLength) return false;

            foreach (var range in ranges)
            {
                stream.Position = range.Offset;
                stream.Write(range.Bytes);
            }
            stream.SetLength(originalLength);
            file.Flush(flushToDisk: true);
            return true;
        }
        catch
        {
            return false;
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
        if (!IsNew && string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
        {
            Save();
            return;
        }

        var historyPlan = PlanHistoryForSave();
        var tempPath = path + ".zitmp";
        try
        {
            await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            _pieceTable.WriteTo(fs);
            fs.Flush(flushToDisk: true);
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }

        try
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }

        var newOriginal = new MappedByteBuffer(path);
        _original.Dispose();
        ResetPieceTable(newOriginal);
        CommitHistoryAfterSave(historyPlan);

        FilePath = path;
        FileName = Path.GetFileName(path);
        OnChanged();
    }

    private HistorySavePlan PlanHistoryForSave()
    {
        var retainedBytes = new Dictionary<IEditCommand, byte[]>();
        long total = 0;
        foreach (var command in _undoRedoManager.UndoCommandsNewestFirst)
        {
            total += command.RetainedLength;
            if (total > UndoRetentionLimit)
                return new HistorySavePlan(command, retainedBytes);

            if (command.CaptureRetainedBytes() is { } bytes)
                retainedBytes.Add(command, bytes);
        }
        return new HistorySavePlan(null, retainedBytes);
    }

    private void CommitHistoryAfterSave(HistorySavePlan plan)
    {
        if (plan.DiscardThrough is not null)
            _undoRedoManager.DiscardUndoThrough(plan.DiscardThrough);

        foreach (var command in _undoRedoManager.AllCommands)
            command.CommitSave(plan.RetainedBytes.GetValueOrDefault(command));
    }

    public void Dispose()
    {
        _original.Dispose();
        _undoRedoManager.Clear();
        ResetPieceTable(new ArrayByteBuffer([]));
    }

    private void ResetPieceTable(ByteBuffer newOriginal)
    {
        var newTable = new PieceTable(newOriginal);
        var newBuffer = new PieceTableByteBuffer(newTable);

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
        Stream stream, WritePlanEntry[] entries, long finalSize, byte[] workBuffer,
        Action<long, byte[], int, int> readAddBuffer)
    {
        if (finalSize > stream.Length)
            stream.SetLength(finalSize);

        foreach (var entry in entries)
        {
            if (entry.Piece.Source != PieceSource.Add) continue;

            stream.Position = entry.OutputOffset;
            var remaining = entry.Piece.Length;
            var srcOffset = entry.Piece.Offset;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(remaining, workBuffer.Length);
                readAddBuffer(srcOffset, workBuffer, 0, chunk);
                stream.Write(workBuffer, 0, chunk);
                srcOffset += chunk;
                remaining -= chunk;
            }
        }

        if (finalSize < stream.Length)
            stream.SetLength(finalSize);
    }

}
