using System;
using System.Collections.Generic;

namespace ZeroIchi.Models;

public class DeleteBytesCommand(BinaryDocument document, int index, int count, int cursorPosition)
    : IEditCommand
{
    private readonly byte[] _deletedBytes = document.Buffer.SliceToArray(index, count);
    private readonly HashSet<int> _modifiedIndicesBefore = [.. document.ModifiedIndices];
    private readonly bool _structurallyModifiedBefore = document.StructurallyModified;

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => document.DeleteBytes(index, _deletedBytes.Length);

    public void Undo()
    {
        document.EnsureMaterialized();
        var data = ((ArrayByteBuffer)document.Buffer).Array;
        var newData = new byte[data.Length + _deletedBytes.Length];
        Array.Copy(data, 0, newData, 0, index);
        Array.Copy(_deletedBytes, 0, newData, index, _deletedBytes.Length);
        Array.Copy(data, index, newData, index + _deletedBytes.Length, data.Length - index);

        ((ArrayByteBuffer)document.Buffer).Array = newData;
        document.ModifiedIndices.Clear();
        document.ModifiedIndices.UnionWith(_modifiedIndicesBefore);
        document.StructurallyModified = _structurallyModifiedBefore;
    }
}
