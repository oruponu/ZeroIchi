using System.Collections.Generic;

namespace ZeroIchi.Models;

public class InsertBytesCommand(BinaryDocument document, int index, byte[] bytes, int cursorPosition)
    : IEditCommand
{
    private readonly HashSet<int> _modifiedIndicesBefore = [.. document.ModifiedIndices];
    private readonly bool _structurallyModifiedBefore = document.StructurallyModified;

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => document.InsertBytes(index, bytes);

    public void Undo()
    {
        document.DeleteBytes(index, bytes.Length);
        document.ModifiedIndices.Clear();
        document.ModifiedIndices.UnionWith(_modifiedIndicesBefore);
        document.StructurallyModified = _structurallyModifiedBefore;
    }
}
