using System.Collections.Generic;

namespace ZeroIchi.Models.Commands;

public class UndoRedoManager
{
    private readonly Stack<IEditCommand> _undoStack = new();
    private readonly Stack<IEditCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void ExecuteCommand(IEditCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public IEditCommand? Undo()
    {
        if (_undoStack.Count == 0) return null;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        return command;
    }

    public IEditCommand? Redo()
    {
        if (_redoStack.Count == 0) return null;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        return command;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
