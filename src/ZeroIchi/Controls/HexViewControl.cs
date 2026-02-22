using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace ZeroIchi.Controls;

public class HexViewControl : Control, ILogicalScrollable
{
    public static readonly StyledProperty<byte[]?> DataProperty =
        AvaloniaProperty.Register<HexViewControl, byte[]?>(nameof(Data));

    public static readonly StyledProperty<int> CursorPositionProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(CursorPosition), defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(SelectionStart), defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectionLengthProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(SelectionLength), defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<HashSet<int>?> ModifiedIndicesProperty =
        AvaloniaProperty.Register<HexViewControl, HashSet<int>?>(nameof(ModifiedIndices));

    public static readonly RoutedEvent<ByteModifiedEventArgs> ByteModifiedEvent =
        RoutedEvent.Register<HexViewControl, ByteModifiedEventArgs>(nameof(ByteModified), RoutingStrategies.Bubble);

    private const int BytesPerLine = 16;
    private const string MonoFontFamily = "Cascadia Mono, Consolas, Courier New, monospace";
    private const double FontSize = 13;

    // 列レイアウト（文字数）：Offset(8) Gap(2) Hex(49=16*3+1) Gap(2) ASCII(16)
    private const int OffsetChars = 8;
    private const int HexChars = 49;
    private const int AsciiChars = 16;
    private const int HexStartChar = OffsetChars + 2;
    private const int AsciiStartChar = HexStartChar + HexChars + 2;
    private const int TotalChars = AsciiStartChar + AsciiChars;

    private static readonly Typeface MonospaceTypeface = new(MonoFontFamily);
    private static readonly IBrush OffsetBrush = Brushes.Gray;
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)).ToImmutable();
    private static readonly IBrush CursorBgBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x4F, 0x78)).ToImmutable();
    private static readonly IBrush SelectionBgBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x26, 0x4F, 0x78)).ToImmutable();
    private static readonly IBrush ModifiedBgBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0x8C, 0x00)).ToImmutable();
    private static readonly Pen HeaderSeparatorPen = new(new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)).ToImmutable());
    private static readonly string HeaderHexText = BuildHeaderHexText();

    static HexViewControl()
    {
        AffectsRender<HexViewControl>(DataProperty, CursorPositionProperty,
            SelectionStartProperty, SelectionLengthProperty, ModifiedIndicesProperty);
        AffectsMeasure<HexViewControl>(DataProperty);
    }

    private double _charWidth;
    private double _lineHeight;
    private bool _metricsValid;

    private Vector _scrollOffset;
    private Size _scrollExtent;
    private Size _scrollViewport;
    private bool _isDragging;
    private int _selectionAnchor;
    private bool _editingHighNibble;

    public HexViewControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public byte[]? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public int CursorPosition
    {
        get => GetValue(CursorPositionProperty);
        set => SetValue(CursorPositionProperty, value);
    }

    public int SelectionStart
    {
        get => GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public int SelectionLength
    {
        get => GetValue(SelectionLengthProperty);
        set => SetValue(SelectionLengthProperty, value);
    }

    public HashSet<int>? ModifiedIndices
    {
        get => GetValue(ModifiedIndicesProperty);
        set => SetValue(ModifiedIndicesProperty, value);
    }

    public event EventHandler<ByteModifiedEventArgs>? ByteModified
    {
        add => AddHandler(ByteModifiedEvent, value);
        remove => RemoveHandler(ByteModifiedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataProperty)
        {
            var oldData = change.GetOldValue<byte[]?>();
            var newData = change.GetNewValue<byte[]?>();

            // バイト追加時はカーソル位置と編集状態を維持
            if (oldData is not null && newData is not null && newData.Length == oldData.Length + 1)
            {
                InvalidateScrollable();
                return;
            }

            CursorPosition = 0;
            SelectionStart = 0;
            SelectionLength = 0;
            _selectionAnchor = 0;
            _editingHighNibble = false;
            _scrollOffset = default;
            InvalidateScrollable();
        }
    }

    private void EnsureMetrics()
    {
        if (_metricsValid) return;

        var formatted = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, MonospaceTypeface, FontSize, Brushes.White);
        _charWidth = formatted.Width;
        _lineHeight = Math.Ceiling(formatted.Height);
        _metricsValid = true;
    }

    private int TotalLines => Data is { } d ? d.Length / BytesPerLine + 1 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();

        var desiredWidth = TotalChars * _charWidth;
        _scrollExtent = new Size(desiredWidth, TotalLines * _lineHeight + _lineHeight);
        _scrollViewport = new Size(availableSize.Width, availableSize.Height);
        InvalidateScrollable();

        return new Size(desiredWidth, 0);
    }

    public override void Render(DrawingContext context)
    {
        EnsureMetrics();

        // ポインターイベントのために透明背景を描画
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        DrawHeader(context);

        var data = Data;
        if (data is null) return;

        var dataTop = _lineHeight;
        var totalLines = data.Length / BytesPerLine + 1;
        var firstLine = Math.Max(0, (int)(_scrollOffset.Y / _lineHeight));
        var visibleLineCount = (int)((Bounds.Height - dataTop) / _lineHeight) + 2;
        var lastLine = Math.Min(totalLines, firstLine + visibleLineCount);

        var selStart = SelectionStart;
        var selEnd = selStart + SelectionLength;

        using (context.PushClip(new Rect(0, dataTop, Bounds.Width, Bounds.Height - dataTop)))
        {
            for (var line = firstLine; line < lastLine; line++)
            {
                var y = dataTop + line * _lineHeight - _scrollOffset.Y;
                var byteOffset = line * BytesPerLine;
                var bytesInLine = Math.Max(0, Math.Min(BytesPerLine, data.Length - byteOffset));

                DrawHighlights(context, byteOffset, bytesInLine, y, selStart, selEnd);
                DrawText(context, byteOffset.ToString("X8"), 0, y, MonospaceTypeface, OffsetBrush);
                if (bytesInLine > 0)
                {
                    DrawHexBytes(context, data, byteOffset, bytesInLine, y, MonospaceTypeface);
                    DrawAscii(context, data, byteOffset, bytesInLine, y, MonospaceTypeface);
                }
            }
        }
    }

    private static string BuildHeaderHexText()
    {
        Span<char> buf = stackalloc char[HexChars];
        buf.Fill(' ');

        for (var i = 0; i < BytesPerLine; i++)
        {
            var charPos = i * 3 + (i >= 8 ? 1 : 0);
            buf[charPos] = ToHexChar(i >> 4);
            buf[charPos + 1] = ToHexChar(i & 0xF);
        }

        return new string(buf);
    }

    private void DrawHeader(DrawingContext context)
    {
        DrawText(context, HeaderHexText, HexStartChar, 0, MonospaceTypeface, OffsetBrush);
        var separatorY = _lineHeight - 0.5;
        context.DrawLine(HeaderSeparatorPen, new Point(0, separatorY), new Point(Bounds.Width, separatorY));
    }

    private void DrawHighlights(DrawingContext context, int byteOffset, int bytesInLine,
        double y, int selStart, int selEnd)
    {
        var cursor = CursorPosition;
        var hasSelection = SelectionLength > 0;
        var modified = ModifiedIndices;

        for (var i = 0; i < bytesInLine; i++)
        {
            var byteIndex = byteOffset + i;
            var isCursor = byteIndex == cursor;
            var isSelected = hasSelection && byteIndex >= selStart && byteIndex < selEnd;
            var isModified = modified is not null && modified.Contains(byteIndex);

            if (!isCursor && !isSelected && !isModified) continue;

            var hexCharPos = HexStartChar + i * 3 + (i >= 8 ? 1 : 0);
            var hexRect = new Rect(hexCharPos * _charWidth, y, 2 * _charWidth, _lineHeight);
            var asciiRect = new Rect((AsciiStartChar + i) * _charWidth, y, _charWidth, _lineHeight);

            if (isModified)
            {
                context.FillRectangle(ModifiedBgBrush, hexRect);
                context.FillRectangle(ModifiedBgBrush, asciiRect);
            }

            if (isCursor || isSelected)
            {
                var brush = isCursor ? CursorBgBrush : SelectionBgBrush;
                context.FillRectangle(brush, hexRect);
                context.FillRectangle(brush, asciiRect);
            }
        }

        var data = Data;
        if (data is not null && cursor == data.Length)
        {
            var appendInLine = data.Length - byteOffset;
            if (appendInLine is >= 0 and < BytesPerLine)
            {
                var hexCharPos = HexStartChar + appendInLine * 3 + (appendInLine >= 8 ? 1 : 0);
                var hexRect = new Rect(hexCharPos * _charWidth, y, 2 * _charWidth, _lineHeight);
                var asciiRect = new Rect((AsciiStartChar + appendInLine) * _charWidth, y, _charWidth, _lineHeight);
                context.FillRectangle(CursorBgBrush, hexRect);
                context.FillRectangle(CursorBgBrush, asciiRect);
            }
        }
    }

    private void DrawText(DrawingContext context, string text, int charColumn, double y,
        Typeface typeface, IBrush brush)
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, FontSize, brush);
        context.DrawText(formatted, new Point(charColumn * _charWidth, y));
    }

    private void DrawHexBytes(DrawingContext context, byte[] data, int byteOffset, int bytesInLine,
        double y, Typeface typeface)
    {
        Span<char> buf = stackalloc char[HexChars];
        buf.Fill(' ');

        for (var i = 0; i < bytesInLine; i++)
        {
            var charPos = i * 3 + (i >= 8 ? 1 : 0);
            var b = data[byteOffset + i];
            buf[charPos] = ToHexChar(b >> 4);
            buf[charPos + 1] = ToHexChar(b & 0xF);
        }

        DrawText(context, new string(buf), HexStartChar, y, typeface, TextBrush);
    }

    private void DrawAscii(DrawingContext context, byte[] data, int byteOffset, int bytesInLine,
        double y, Typeface typeface)
    {
        Span<char> buf = stackalloc char[bytesInLine];

        for (var i = 0; i < bytesInLine; i++)
        {
            var b = data[byteOffset + i];
            buf[i] = b is >= 0x20 and <= 0x7E ? (char)b : '.';
        }

        DrawText(context, new string(buf), AsciiStartChar, y, typeface, TextBrush);
    }

    private static char ToHexChar(int nibble) =>
        (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);

    private int HitTestByte(Point point)
    {
        EnsureMetrics();

        var data = Data;
        if (data is null || point.Y < _lineHeight) return -1;

        var line = (int)((point.Y - _lineHeight + _scrollOffset.Y) / _lineHeight);
        if (line < 0 || line >= TotalLines) return -1;

        var charCol = point.X / _charWidth;
        int byteInLine;

        if (charCol is >= AsciiStartChar and < AsciiStartChar + AsciiChars)
        {
            byteInLine = (int)(charCol - AsciiStartChar);
        }
        else if (charCol is >= HexStartChar and < HexStartChar + HexChars)
        {
            var relChar = charCol - HexStartChar;
            byteInLine = (int)((relChar - (relChar >= 24 ? 1 : 0)) / 3);
        }
        else
        {
            return -1;
        }

        byteInLine = Math.Clamp(byteInLine, 0, BytesPerLine - 1);
        var byteIndex = line * BytesPerLine + byteInLine;
        return Math.Min(byteIndex, data.Length);
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
            SelectionLength = 0;
        }

        _isDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging) return;

        var byteIndex = HitTestByte(e.GetPosition(this));
        if (byteIndex < 0) return;

        UpdateSelection(_selectionAnchor, byteIndex);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging) return;

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

        var data = Data;
        if (data is null) return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var maxIndex = data.Length;
        var newPos = CursorPosition;
        var visibleLines = (int)((Bounds.Height - _lineHeight) / _lineHeight);

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
            SelectionLength = 0;
            _selectionAnchor = newPos;
        }

        EnsureCursorVisible();
        e.Handled = true;
    }

    private void EnsureCursorVisible()
    {
        EnsureMetrics();

        var cursorLine = CursorPosition / BytesPerLine;
        var cursorY = cursorLine * _lineHeight;
        var viewTop = _scrollOffset.Y;
        var viewHeight = _scrollViewport.Height - _lineHeight;
        var viewBottom = viewTop + viewHeight;

        if (cursorY < viewTop)
            ((IScrollable)this).Offset = new Vector(_scrollOffset.X, cursorY);
        else if (cursorY + _lineHeight > viewBottom)
            ((IScrollable)this).Offset = new Vector(_scrollOffset.X, cursorY + _lineHeight - viewHeight);
    }

    private void HandleHexInput(int nibble, int maxIndex)
    {
        var data = Data;
        if (data is null) return;

        var pos = CursorPosition;

        if (pos == data.Length)
        {
            var newValue = (byte)(nibble << 4);
            RaiseEvent(new ByteModifiedEventArgs(ByteModifiedEvent, this, pos, newValue));
            _editingHighNibble = true;
            EnsureCursorVisible();
            return;
        }

        var currentByte = data[pos];

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
            SelectionLength = 0;
            _selectionAnchor = newPos;
            EnsureCursorVisible();
        }
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

    public event EventHandler? ScrollInvalidated;

    bool ILogicalScrollable.CanHorizontallyScroll { get; set; }
    bool ILogicalScrollable.CanVerticallyScroll { get; set; }
    bool ILogicalScrollable.IsLogicalScrollEnabled => true;
    Size ILogicalScrollable.ScrollSize => new(_charWidth * 4, _lineHeight);
    Size ILogicalScrollable.PageScrollSize => new(_scrollViewport.Width, Math.Max(_lineHeight, _scrollViewport.Height - _lineHeight * 2));
    Size IScrollable.Extent => _scrollExtent;
    Size IScrollable.Viewport => _scrollViewport;

    Vector IScrollable.Offset
    {
        get => _scrollOffset;
        set
        {
            var maxX = Math.Max(0, _scrollExtent.Width - _scrollViewport.Width);
            var maxY = Math.Max(0, _scrollExtent.Height - _scrollViewport.Height);
            var clamped = new Vector(
                Math.Clamp(value.X, 0, maxX),
                Math.Clamp(value.Y, 0, maxY));

            if (_scrollOffset == clamped) return;
            _scrollOffset = clamped;
            InvalidateVisual();
        }
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;
    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;
    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    private void InvalidateScrollable() => ScrollInvalidated?.Invoke(this, EventArgs.Empty);
}

public class ByteModifiedEventArgs(RoutedEvent routedEvent, object source, int index, byte value)
    : RoutedEventArgs(routedEvent, source)
{
    public int Index { get; } = index;
    public byte Value { get; } = value;
}
