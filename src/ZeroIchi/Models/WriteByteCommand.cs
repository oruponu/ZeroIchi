using System.Collections.Generic;

namespace ZeroIchi.Models;

public class WriteByteCommand(BinaryDocument document, int index, byte newValue, int cursorPosition)
    : IEditCommand
{
    private readonly byte _oldValue = document.Buffer.ReadByte(index);
    private readonly HashSet<int> _modifiedIndicesBefore = [.. document.ModifiedIndices];

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute()
    {
        document.Buffer.WriteByte(index, newValue);
        document.ModifiedIndices.Add(index);
    }

    public void Undo()
    {
        document.Buffer.WriteByte(index, _oldValue);
        document.ModifiedIndices.Clear();
        document.ModifiedIndices.UnionWith(_modifiedIndicesBefore);
    }
}
