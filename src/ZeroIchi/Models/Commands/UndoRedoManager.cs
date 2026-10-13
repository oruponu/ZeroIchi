using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroIchi.Models.Commands;

public sealed class UndoRedoManager
{
    private readonly List<IEditCommand> _undoList = [];
    private readonly Stack<IEditCommand> _redoStack = new();

    public bool CanUndo => _undoList.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public IEnumerable<IEditCommand> UndoCommandsNewestFirst
    {
        get
        {
            for (var i = _undoList.Count - 1; i >= 0; i--)
                yield return _undoList[i];
        }
    }

    public IEnumerable<IEditCommand> AllCommands => _undoList.Concat(_redoStack);

    public void ExecuteCommand(IEditCommand command)
    {
        command.Execute();
        _undoList.Add(command);
        _redoStack.Clear();
    }

    public IEditCommand? Undo()
    {
        if (_undoList.Count == 0) return null;

        var command = _undoList[^1];
        _undoList.RemoveAt(_undoList.Count - 1);
        command.Undo();
        _redoStack.Push(command);
        return command;
    }

    public IEditCommand? Redo()
    {
        if (_redoStack.Count == 0) return null;

        var command = _redoStack.Pop();
        command.Execute();
        _undoList.Add(command);
        return command;
    }

    public void DiscardUndoThrough(IEditCommand command)
    {
        var index = _undoList.IndexOf(command);
        if (index < 0)
            throw new ArgumentException(null, nameof(command));
        _undoList.RemoveRange(0, index + 1);
    }

    public void Clear()
    {
        _undoList.Clear();
        _redoStack.Clear();
    }
}
