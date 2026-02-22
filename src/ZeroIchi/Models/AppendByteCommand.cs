using System.Collections.Generic;

namespace ZeroIchi.Models;

public class AppendByteCommand(BinaryDocument document, byte value, int cursorPosition) : IEditCommand
{
    private readonly HashSet<int> _modifiedIndicesBefore = [.. document.ModifiedIndices];

    public int CursorPositionBefore { get; } = cursorPosition;
    public int CursorPositionAfter { get; set; }

    public void Execute()
    {
        document.Data = [.. document.Data, value];
        document.ModifiedIndices.Add(document.Data.Length - 1);
    }

    public void Undo()
    {
        document.Data = document.Data[..^1];
        document.ModifiedIndices.Clear();
        document.ModifiedIndices.UnionWith(_modifiedIndicesBefore);
    }
}
