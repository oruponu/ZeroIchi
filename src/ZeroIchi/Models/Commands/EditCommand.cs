using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Models.Commands;

public abstract class EditCommand(BinaryDocument document, long cursorPositionBefore, long cursorPositionAfter)
    : IEditCommand
{
    private PieceTableEdit? _edit;
    private bool _editIsInverse;
    private bool _editIsApplied;

    public BinaryDocument Document => document;

    public long CursorPositionBefore { get; } = cursorPositionBefore;
    public long CursorPositionAfter { get; } = cursorPositionAfter;

    public virtual long RetainedLength => 0;

    protected PieceTableEdit? ForwardEdit => _editIsInverse ? null : _edit;

    public void Execute() => MoveTo(inverse: false);

    public void Undo() => MoveTo(inverse: true);

    private void MoveTo(bool inverse)
    {
        if (_edit is not null)
        {
            if (_editIsInverse != inverse && _editIsApplied)
            {
                document.UndoEdit(_edit);
                _editIsApplied = false;
                return;
            }

            if (_editIsInverse == inverse && !_editIsApplied)
            {
                document.RedoEdit(_edit);
                _editIsApplied = true;
                return;
            }
        }

        _edit = inverse ? ApplyInverse() : ApplyForward();
        _editIsInverse = inverse;
        _editIsApplied = true;
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
