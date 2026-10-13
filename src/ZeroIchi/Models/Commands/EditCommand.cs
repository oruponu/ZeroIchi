using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public abstract class EditCommand(BinaryDocument document, long cursorPositionBefore, long cursorPositionAfter)
    : IEditCommand
{
    private PieceTableEdit? _edit;
    private bool _editIsInverse;

    public BinaryDocument Document => document;

    public long CursorPositionBefore { get; } = cursorPositionBefore;
    public long CursorPositionAfter { get; } = cursorPositionAfter;

    public virtual long RetainedLength => 0;

    protected PieceTableEdit? ForwardEdit => _editIsInverse ? null : _edit;

    public void Execute()
    {
        if (_edit is not null && _editIsInverse)
        {
            document.UndoEdit(_edit);
            _edit = null;
            return;
        }

        _edit = ApplyForward();
        _editIsInverse = false;
    }

    public void Undo()
    {
        if (_edit is not null && !_editIsInverse)
        {
            document.UndoEdit(_edit);
            _edit = null;
            return;
        }

        _edit = ApplyInverse();
        _editIsInverse = true;
    }

    public virtual byte[]? CaptureRetainedBytes() => null;

    public void CommitSave(byte[]? retainedBytes)
    {
        if (retainedBytes is not null)
            StoreRetainedBytes(retainedBytes);
        _edit = null;
    }

    protected virtual void StoreRetainedBytes(byte[] bytes)
    {
    }

    protected abstract PieceTableEdit ApplyForward();
    protected abstract PieceTableEdit ApplyInverse();
}
