using System.Collections.Generic;

namespace ZeroIchi.Models;

public class AppendByteCommand(BinaryDocument document, byte value, int cursorPosition) : IEditCommand
{
    private readonly HashSet<int> _modifiedIndicesBefore = [.. document.ModifiedIndices];

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute() => document.AppendByte(value);

    public void Undo()
    {
        document.EnsureMaterialized();
        var arr = ((ArrayByteBuffer)document.Buffer).Array;
        ((ArrayByteBuffer)document.Buffer).Array = arr[..^1];
        document.ModifiedIndices.Clear();
        document.ModifiedIndices.UnionWith(_modifiedIndicesBefore);
    }
}
