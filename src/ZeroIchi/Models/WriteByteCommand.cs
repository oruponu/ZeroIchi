using System.Collections.Generic;

namespace ZeroIchi.Models;

public class WriteByteCommand(BinaryDocument document, int index, byte newValue, int cursorPosition)
    : IEditCommand
{
    private readonly byte _oldValue = document.Data[index];
    private readonly HashSet<int> _modifiedIndicesBefore = [.. document.ModifiedIndices];

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute()
    {
        document.Data[index] = newValue;
        document.ModifiedIndices.Add(index);
    }

    public void Undo()
    {
        document.Data[index] = _oldValue;
        document.ModifiedIndices.Clear();
        document.ModifiedIndices.UnionWith(_modifiedIndicesBefore);
    }
}
