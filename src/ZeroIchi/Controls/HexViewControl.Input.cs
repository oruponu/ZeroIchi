using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using System;

namespace ZeroIchi.Controls;

public partial class HexViewControl
{
    private int HitTestColumn(double x)
    {
        if (x >= _asciiStartX && x < AsciiCellX(BytesPerLine))
            return Math.Clamp((int)((x - _asciiStartX) / _asciiCellWidth), 0, BytesPerLine - 1);

        if (x >= _hexStartX && x < _hexEndX)
        {
            var relChar = (x - _hexStartX) / _charWidth;
            return Math.Clamp((int)((relChar - (relChar >= 24 ? 1 : 0)) / 3), 0, BytesPerLine - 1);
        }

        return -1;
    }

    // The address column stays in place over the horizontally scrolled content
    private double ToContentX(double x) => x < ContentLeft ? 0 : x + _scrollOffset.X;

    private long HitTestByte(Point point)
    {
        EnsureMetrics();

        var buffer = Buffer;
        if (buffer is null || point.Y < _dataTop) return -1;

        var line = (long)((point.Y - _dataTop + _scrollOffset.Y) / _rowHeight);
        if (line < 0 || line >= TotalLines) return -1;

        var byteInLine = HitTestColumn(ToContentX(point.X));
        if (byteInLine < 0) return -1;

        return Math.Min(line * BytesPerLine + byteInLine, buffer.Length);
    }

    private long HitTestByteDrag(Point point)
    {
        EnsureMetrics();

        var buffer = Buffer;
        if (buffer is null || buffer.Length == 0) return -1;

        var line = (long)((point.Y - _dataTop + _scrollOffset.Y) / _rowHeight);
        line = Math.Clamp(line, 0, TotalLines - 1);

        var x = ToContentX(point.X);
        var byteInLine = HitTestColumn(x);
        if (byteInLine < 0)
            byteInLine = x < _hexStartX ? 0 : BytesPerLine - 1;

        return Math.Min(line * BytesPerLine + byteInLine, buffer.Length);
    }

    private void StartAutoScroll()
    {
        if (_autoScrollTimer != null) return;
        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _autoScrollTimer.Tick += OnAutoScrollTick;
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer == null) return;
        _autoScrollTimer.Stop();
        _autoScrollTimer.Tick -= OnAutoScrollTick;
        _autoScrollTimer = null;
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (!_isDragging) { StopAutoScroll(); return; }
        UpdateDragSelection();
    }

    private void UpdateDragSelection()
    {
        var byteIndex = HitTestByteDrag(_lastPointerPosition);
        if (byteIndex < 0) return;
        UpdateSelection(SelectionAnchor, byteIndex);
        EnsureCursorVisible();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetPosition(this);
        var byteIndex = HitTestByte(point);
        if (byteIndex < 0) return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _editingHighNibble = false;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            UpdateSelection(SelectionAnchor, byteIndex);
        }
        else
        {
            CursorPosition = byteIndex;
            SelectionStart = byteIndex;
            SelectionLength = Buffer is { } b && byteIndex < b.Length ? 1 : 0;
        }

        _isDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pos = e.GetPosition(this);

        if (_isDragging)
        {
            _lastPointerPosition = pos;
            UpdateDragSelection();

            if (pos.Y < _dataTop || pos.Y > Bounds.Height)
                StartAutoScroll();
            else
                StopAutoScroll();

            e.Handled = true;
            return;
        }

        var byteIndex = HitTestByte(pos);
        var newHover = byteIndex >= 0 ? byteIndex : -1;
        if (newHover != _hoveredByteIndex)
        {
            _hoveredByteIndex = newHover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_hoveredByteIndex >= 0)
        {
            _hoveredByteIndex = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging) return;

        EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag();
    }

    private void EndDrag()
    {
        StopAutoScroll();
        _isDragging = false;
    }

    private long SelectionAnchor => GetSelectionAnchor(CursorPosition, SelectionStart, SelectionLength);

    internal static long GetSelectionAnchor(long cursorPosition, long selectionStart, long selectionLength)
    {
        if (selectionLength <= 0) return cursorPosition;
        var selectionEnd = selectionStart + selectionLength - 1;
        return cursorPosition == selectionStart ? selectionEnd : selectionStart;
    }

    private void UpdateSelection(long anchor, long current)
    {
        CursorPosition = current;
        (SelectionStart, SelectionLength) = GetSelectionRange(anchor, current, Buffer?.Length ?? 0);
    }

    internal static (long Start, long Length) GetSelectionRange(long anchor, long current, long dataLength)
    {
        var start = Math.Min(anchor, current);
        var end = Math.Min(Math.Max(anchor, current), dataLength - 1);
        return (start, Math.Max(0, end - start + 1));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        EnsureMetrics();

        var buffer = Buffer;
        if (buffer is null) return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var dataLength = buffer.Length;
        var maxIndex = dataLength;
        var newPos = CursorPosition;
        var visibleLines = (int)((Bounds.Height - _dataTop) / _rowHeight);

        switch (e.Key)
        {
            case Key.Left:
                newPos = Math.Max(0, newPos - 1);
                break;
            case Key.Right:
                newPos = Math.Min(maxIndex, newPos + 1);
                break;
            case Key.Up:
                newPos = Math.Max(0, newPos - BytesPerLine);
                break;
            case Key.Down:
                newPos = Math.Min(maxIndex, newPos + BytesPerLine);
                break;
            case Key.Home:
                newPos = ctrl ? 0 : newPos - newPos % BytesPerLine;
                break;
            case Key.End:
                newPos = ctrl ? maxIndex : Math.Min(maxIndex, newPos - newPos % BytesPerLine + BytesPerLine - 1);
                break;
            case Key.PageUp:
                newPos = Math.Max(0, newPos - visibleLines * BytesPerLine);
                break;
            case Key.PageDown:
                newPos = Math.Min(maxIndex, newPos + visibleLines * BytesPerLine);
                break;
            case Key.Delete:
            case Key.Back:
                HandleDelete(e.Key, dataLength);
                e.Handled = true;
                return;
            default:
                return;
        }

        _editingHighNibble = false;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            UpdateSelection(SelectionAnchor, newPos);
        }
        else
        {
            CursorPosition = newPos;
            SelectionStart = newPos;
            SelectionLength = newPos < maxIndex ? 1 : 0;
        }

        EnsureCursorVisible();
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        var buffer = Buffer;
        if (buffer is null || e.Text is not { } text) return;

        foreach (var c in text)
        {
            if (ParseHexDigit(c) is { } nibble)
            {
                HandleHexInput(nibble, buffer.Length);
                e.Handled = true;
            }
        }
    }

    private void HandleHexInput(int nibble, long maxIndex)
    {
        var buffer = Buffer;
        if (buffer is null) return;

        var pos = CursorPosition;
        var dataLength = buffer.Length;

        if (pos == dataLength)
        {
            var newValue = (byte)(nibble << 4);
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue, pos));
            _editingHighNibble = true;
            EnsureCursorVisible();
            return;
        }

        var currentByte = buffer.ReadByte(pos);

        if (!_editingHighNibble)
        {
            var newValue = (byte)((nibble << 4) | (currentByte & 0x0F));
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue, pos));
            _editingHighNibble = true;
        }
        else
        {
            var newValue = (byte)((currentByte & 0xF0) | nibble);
            var newPos = Math.Min(maxIndex, pos + 1);
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue, newPos));
            _editingHighNibble = false;

            CursorPosition = newPos;
            SelectionStart = newPos;
            SelectionLength = newPos < maxIndex ? 1 : 0;
            EnsureCursorVisible();
        }
    }

    private void HandleDelete(Key key, long dataLength)
    {
        var range = GetDeleteRange(key, SelectionStart, SelectionLength, CursorPosition, dataLength);
        if (range is null) return;

        var (deleteIndex, deleteCount) = range.Value;
        var newPos = Math.Clamp(deleteIndex, 0, dataLength - deleteCount);

        RaiseEvent(new BytesDeletedEventArgs(BytesDeletedEvent, this, deleteIndex, deleteCount, newPos));

        _editingHighNibble = false;
        CursorPosition = newPos;
        SelectionStart = newPos;
        SelectionLength = newPos < dataLength - deleteCount ? 1 : 0;
        EnsureCursorVisible();
    }

    internal static (long Index, long Count)? GetDeleteRange(
        Key key, long selectionStart, long selectionLength, long cursorPosition, long dataLength)
    {
        long deleteIndex;
        long deleteCount;

        if (selectionLength > 0)
        {
            deleteIndex = selectionStart;
            // The selection may include the virtual cell just past EOF, so clamp it to the data length
            deleteCount = Math.Min(selectionLength, dataLength - selectionStart);
        }
        else if (key == Key.Delete)
        {
            if (cursorPosition >= dataLength) return null;
            deleteIndex = cursorPosition;
            deleteCount = 1;
        }
        else if (key == Key.Back)
        {
            if (cursorPosition <= 0) return null;
            deleteIndex = cursorPosition - 1;
            deleteCount = 1;
        }
        else
        {
            return null;
        }

        return deleteCount <= 0 ? null : (deleteIndex, deleteCount);
    }

    internal static int? ParseHexDigit(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 0xA,
        >= 'A' and <= 'F' => c - 'A' + 0xA,
        _ => null,
    };
}
