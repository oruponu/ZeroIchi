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

    private int HitTestByte(Point point)
    {
        EnsureMetrics();

        var buffer = Buffer;
        if (buffer is null || point.Y < _dataTop) return -1;

        var line = (int)((point.Y - _dataTop + _scrollOffset.Y) / _rowHeight);
        if (line < 0 || line >= TotalLines) return -1;

        var byteInLine = HitTestColumn(point.X);
        if (byteInLine < 0) return -1;

        return Math.Min(line * BytesPerLine + byteInLine, (int)buffer.Length);
    }

    private int HitTestByteDrag(Point point)
    {
        EnsureMetrics();

        var buffer = Buffer;
        if (buffer is null || buffer.Length == 0) return -1;

        var line = (int)((point.Y - _dataTop + _scrollOffset.Y) / _rowHeight);
        line = Math.Clamp(line, 0, TotalLines - 1);

        var byteInLine = HitTestColumn(point.X);
        if (byteInLine < 0)
            byteInLine = point.X < _hexStartX ? 0 : BytesPerLine - 1;

        return Math.Min(line * BytesPerLine + byteInLine, (int)buffer.Length);
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
        UpdateSelection(_selectionAnchor, byteIndex);
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
            UpdateSelection(_selectionAnchor, byteIndex);
        }
        else
        {
            _selectionAnchor = byteIndex;
            CursorPosition = byteIndex;
            SelectionStart = byteIndex;
            SelectionLength = Buffer is { } b && byteIndex < (int)b.Length ? 1 : 0;
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

        StopAutoScroll();
        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void UpdateSelection(int anchor, int current)
    {
        CursorPosition = current;
        SelectionStart = Math.Min(anchor, current);
        SelectionLength = Math.Abs(current - anchor) + 1;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        EnsureMetrics();

        var buffer = Buffer;
        if (buffer is null) return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var dataLength = (int)buffer.Length;
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
                if (!ctrl && TryParseHexKey(e.Key) is { } nibble)
                {
                    HandleHexInput(nibble, maxIndex);
                    e.Handled = true;
                }
                return;
        }

        _editingHighNibble = false;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (SelectionLength == 0)
                _selectionAnchor = CursorPosition;
            UpdateSelection(_selectionAnchor, newPos);
        }
        else
        {
            CursorPosition = newPos;
            SelectionStart = newPos;
            SelectionLength = newPos < maxIndex ? 1 : 0;
            _selectionAnchor = newPos;
        }

        EnsureCursorVisible();
        e.Handled = true;
    }

    private void HandleHexInput(int nibble, int maxIndex)
    {
        var buffer = Buffer;
        if (buffer is null) return;

        var pos = CursorPosition;
        var dataLength = (int)buffer.Length;

        if (pos == dataLength)
        {
            var newValue = (byte)(nibble << 4);
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue));
            _editingHighNibble = true;
            EnsureCursorVisible();
            return;
        }

        var currentByte = buffer.ReadByte(pos);

        if (!_editingHighNibble)
        {
            var newValue = (byte)((nibble << 4) | (currentByte & 0x0F));
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue));
            _editingHighNibble = true;
        }
        else
        {
            var newValue = (byte)((currentByte & 0xF0) | nibble);
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue));
            _editingHighNibble = false;

            var newPos = Math.Min(maxIndex, pos + 1);
            CursorPosition = newPos;
            SelectionStart = newPos;
            SelectionLength = newPos < maxIndex ? 1 : 0;
            _selectionAnchor = newPos;
            EnsureCursorVisible();
        }
    }

    private void HandleDelete(Key key, int dataLength)
    {
        int deleteIndex;
        int deleteCount;

        if (SelectionLength > 0)
        {
            deleteIndex = SelectionStart;
            deleteCount = SelectionLength;
        }
        else if (key == Key.Delete)
        {
            if (CursorPosition >= dataLength) return;
            deleteIndex = CursorPosition;
            deleteCount = 1;
        }
        else if (key == Key.Back)
        {
            if (CursorPosition <= 0) return;
            deleteIndex = CursorPosition - 1;
            deleteCount = 1;
        }
        else
        {
            return;
        }

        var newPos = Math.Clamp(deleteIndex, 0, dataLength - deleteCount);

        RaiseEvent(new BytesDeletedEventArgs(BytesDeletedEvent, this, deleteIndex, deleteCount));

        _editingHighNibble = false;
        CursorPosition = newPos;
        SelectionStart = newPos;
        SelectionLength = newPos < dataLength - deleteCount ? 1 : 0;
        _selectionAnchor = newPos;
        EnsureCursorVisible();
    }

    private static int? TryParseHexKey(Key key) => key switch
    {
        Key.D0 or Key.NumPad0 => 0,
        Key.D1 or Key.NumPad1 => 1,
        Key.D2 or Key.NumPad2 => 2,
        Key.D3 or Key.NumPad3 => 3,
        Key.D4 or Key.NumPad4 => 4,
        Key.D5 or Key.NumPad5 => 5,
        Key.D6 or Key.NumPad6 => 6,
        Key.D7 or Key.NumPad7 => 7,
        Key.D8 or Key.NumPad8 => 8,
        Key.D9 or Key.NumPad9 => 9,
        Key.A => 0xA,
        Key.B => 0xB,
        Key.C => 0xC,
        Key.D => 0xD,
        Key.E => 0xE,
        Key.F => 0xF,
        _ => null,
    };
}
