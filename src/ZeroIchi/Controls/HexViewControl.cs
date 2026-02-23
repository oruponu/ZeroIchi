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

    public static readonly RoutedEvent<BytesDeletedEventArgs> BytesDeletedEvent =
        RoutedEvent.Register<HexViewControl, BytesDeletedEventArgs>(nameof(BytesDeleted), RoutingStrategies.Bubble);

    private const int BytesPerLine = 16;
    private const string MonoFontFamily = "Cascadia Mono, Consolas, Courier New, monospace";
    private const double FontSize = 13;
    private const double CellPaddingX = 4;
    private const double CellPaddingY = 2;
    private const double HighlightCornerRadius = 4;

    private const int OffsetChars = 8;
    private const int HexChars = 49;
    private const double ColumnGap = 16;

    private static readonly Typeface MonospaceTypeface = new(MonoFontFamily);
    private static readonly IBrush OffsetBrush = Brushes.Gray;
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)).ToImmutable();
    private static readonly IBrush CursorBgBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x4F, 0x78)).ToImmutable();
    private static readonly IBrush SelectionBgBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x26, 0x4F, 0x78)).ToImmutable();
    private static readonly IBrush ModifiedBgBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0x8C, 0x00)).ToImmutable();
    private static readonly IBrush HoverBgBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)).ToImmutable();
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
    private double _rowHeight;
    private double _dataTop;
    private double _hexStartX;
    private double _hexEndX;
    private double _asciiStartX;
    private double _asciiCellWidth;
    private bool _metricsValid;

    private Vector _scrollOffset;
    private Size _scrollExtent;
    private Size _scrollViewport;
    private bool _isDragging;
    private int _selectionAnchor;
    private bool _editingHighNibble;
    private int _hoveredByteIndex = -1;

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

    public event EventHandler<BytesDeletedEventArgs>? BytesDeleted
    {
        add => AddHandler(BytesDeletedEvent, value);
        remove => RemoveHandler(BytesDeletedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataProperty)
        {
            var oldData = change.GetOldValue<byte[]?>();
            var newData = change.GetNewValue<byte[]?>();

            // 編集操作時はカーソル位置と編集状態を維持
            if (oldData is not null && newData is not null)
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
        _rowHeight = _lineHeight + 2 * CellPaddingY;
        _dataTop = _rowHeight + ColumnGap - 2 * CellPaddingX;
        _hexStartX = CellPaddingX + OffsetChars * _charWidth + ColumnGap;
        _hexEndX = _hexStartX + HexChars * _charWidth;
        _asciiStartX = _hexEndX + ColumnGap;
        _asciiCellWidth = _charWidth + 2 * CellPaddingX;
        _metricsValid = true;
    }

    private int TotalLines => Data is { } d ? d.Length / BytesPerLine + 1 : 0;

    private double AsciiCellX(int i) => _asciiStartX + i * _asciiCellWidth;
    private double TotalWidth => AsciiCellX(BytesPerLine);

    private Rect HexCellRect(int byteInLine, double y)
    {
        var charInHex = byteInLine * 3 + (byteInLine >= 8 ? 1 : 0);
        return new Rect(_hexStartX + charInHex * _charWidth - CellPaddingX, y, 2 * _charWidth + 2 * CellPaddingX, _rowHeight);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();

        var desiredWidth = TotalWidth;
        _scrollExtent = new Size(desiredWidth, TotalLines * _rowHeight + _dataTop);
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

        var dataTop = _dataTop;
        var totalLines = data.Length / BytesPerLine + 1;
        var firstLine = Math.Max(0, (int)(_scrollOffset.Y / _rowHeight));
        var visibleLineCount = (int)((Bounds.Height - dataTop) / _rowHeight) + 2;
        var lastLine = Math.Min(totalLines, firstLine + visibleLineCount);

        var selStart = SelectionStart;
        var selEnd = selStart + SelectionLength;

        using (context.PushClip(new Rect(0, dataTop, Bounds.Width, Bounds.Height - dataTop)))
        {
            for (var line = firstLine; line < lastLine; line++)
            {
                var y = dataTop + line * _rowHeight - _scrollOffset.Y;
                var byteOffset = line * BytesPerLine;
                var bytesInLine = Math.Max(0, Math.Min(BytesPerLine, data.Length - byteOffset));

                DrawHighlights(context, byteOffset, bytesInLine, y, selStart, selEnd);
                var textY = y + CellPaddingY;
                DrawText(context, byteOffset.ToString("X8"), CellPaddingX, textY, MonospaceTypeface, OffsetBrush);
                if (bytesInLine > 0)
                {
                    DrawHexBytes(context, data, byteOffset, bytesInLine, textY, MonospaceTypeface);
                    DrawAscii(context, data, byteOffset, bytesInLine, textY, MonospaceTypeface);
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
        if (Data is not null)
        {
            var cursorCol = CursorPosition % BytesPerLine;
            var hasSelection = SelectionLength > 0;
            var selFirstCol = 0;
            var selLastCol = 0;
            var selLineSpan = 0;

            if (hasSelection)
            {
                var selEnd = SelectionStart + SelectionLength - 1;
                selFirstCol = SelectionStart % BytesPerLine;
                selLastCol = selEnd % BytesPerLine;
                selLineSpan = selEnd / BytesPerLine - SelectionStart / BytesPerLine;
            }

            bool IsHeaderHighlighted(int col) =>
                col == cursorCol || (hasSelection && (selLineSpan >= 2
                    || (selLineSpan == 0 ? col >= selFirstCol && col <= selLastCol
                                         : col >= selFirstCol || col <= selLastCol)));

            for (var i = 0; i < BytesPerLine; i++)
            {
                if (!IsHeaderHighlighted(i)) continue;

                var hasLeft = i > 0 && i != 8 && IsHeaderHighlighted(i - 1);
                var hasRight = i < BytesPerLine - 1 && i != 7 && IsHeaderHighlighted(i + 1);
                var r = HighlightCornerRadius;

                FillRoundedRect(context, i == cursorCol ? CursorBgBrush : SelectionBgBrush, HexCellRect(i, 0),
                    hasLeft ? 0 : r, hasRight ? 0 : r, hasRight ? 0 : r, hasLeft ? 0 : r);
            }
        }

        DrawText(context, HeaderHexText, _hexStartX, CellPaddingY, MonospaceTypeface, OffsetBrush);
        var separatorY = Math.Floor((_rowHeight + _dataTop) / 2) - 0.5;
        context.DrawLine(HeaderSeparatorPen, new Point(0, separatorY), new Point(Bounds.Width, separatorY));
    }

    private void DrawHighlights(DrawingContext context, int byteOffset, int bytesInLine,
        double y, int selStart, int selEnd)
    {
        var cursor = CursorPosition;
        var hasSelection = SelectionLength > 0;
        var modified = ModifiedIndices;
        var hovered = _hoveredByteIndex;
        var data = Data;
        var dataLength = data?.Length ?? 0;

        var cursorOnLine = cursor / BytesPerLine == byteOffset / BytesPerLine;
        var selectionOnLine = hasSelection && selStart < byteOffset + bytesInLine && selEnd > byteOffset;

        if (cursorOnLine || selectionOnLine)
        {
            bool IsAddrRowHighlighted(int rowOffset)
            {
                if (rowOffset < 0 || rowOffset > dataLength) return false;
                if (cursor / BytesPerLine == rowOffset / BytesPerLine) return true;
                var rowEnd = Math.Min(rowOffset + BytesPerLine, dataLength);
                return hasSelection && selStart < rowEnd && selEnd > rowOffset;
            }

            var addrAbove = IsAddrRowHighlighted(byteOffset - BytesPerLine);
            var addrBelow = IsAddrRowHighlighted(byteOffset + BytesPerLine);
            var r = HighlightCornerRadius;

            var offsetRect = new Rect(0, y, OffsetChars * _charWidth + 2 * CellPaddingX, _rowHeight);
            FillRoundedRect(context, cursorOnLine ? CursorBgBrush : SelectionBgBrush, offsetRect,
                addrAbove ? 0 : r, addrAbove ? 0 : r, addrBelow ? 0 : r, addrBelow ? 0 : r);
        }

        for (var i = 0; i < bytesInLine; i++)
        {
            var byteIndex = byteOffset + i;
            var isCursor = byteIndex == cursor;
            var isSelected = hasSelection && byteIndex >= selStart && byteIndex < selEnd;
            var isModified = modified is not null && modified.Contains(byteIndex);
            var isHovered = byteIndex == hovered;
            var highlighted = isCursor || isSelected;

            if (!highlighted && !isModified && !isHovered) continue;

            var hexRect = HexCellRect(i, y);
            var asciiRect = new Rect(AsciiCellX(i), y, _asciiCellWidth, _rowHeight);

            if (isHovered && !highlighted)
            {
                context.DrawRectangle(HoverBgBrush, null, new RoundedRect(hexRect, HighlightCornerRadius));
                context.DrawRectangle(HoverBgBrush, null, new RoundedRect(asciiRect, HighlightCornerRadius));
            }

            if (isModified)
            {
                bool IsModifiedNeighbor(int index) =>
                    index >= 0 && index < dataLength && modified!.Contains(index);

                DrawMergedCells(context, ModifiedBgBrush, hexRect, asciiRect, i,
                    byteIndex >= BytesPerLine && IsModifiedNeighbor(byteIndex - BytesPerLine),
                    byteIndex + BytesPerLine < dataLength && IsModifiedNeighbor(byteIndex + BytesPerLine),
                    i > 0 && IsModifiedNeighbor(byteIndex - 1),
                    i < BytesPerLine - 1 && IsModifiedNeighbor(byteIndex + 1));
            }

            if (highlighted)
            {
                DrawMergedCells(context, isCursor ? CursorBgBrush : SelectionBgBrush, hexRect, asciiRect, i,
                    byteIndex >= BytesPerLine && IsHighlighted(byteIndex - BytesPerLine),
                    byteIndex + BytesPerLine <= dataLength && IsHighlighted(byteIndex + BytesPerLine),
                    i > 0 && IsHighlighted(byteIndex - 1),
                    i < BytesPerLine - 1 && IsHighlighted(byteIndex + 1));
            }
        }

        if (data is not null && cursor == data.Length)
        {
            var appendInLine = data.Length - byteOffset;
            if (appendInLine is >= 0 and < BytesPerLine)
            {
                DrawMergedCells(context, CursorBgBrush,
                    HexCellRect(appendInLine, y),
                    new Rect(AsciiCellX(appendInLine), y, _asciiCellWidth, _rowHeight),
                    appendInLine,
                    cursor >= BytesPerLine && IsHighlighted(cursor - BytesPerLine),
                    false,
                    appendInLine > 0 && IsHighlighted(cursor - 1),
                    false);
            }
        }

        return;

        bool IsHighlighted(int index) =>
            index == cursor || (hasSelection && index >= selStart && index < selEnd);
    }

    // バイト 7-8 の間は結合しない
    private static void DrawMergedCells(DrawingContext context, IBrush brush,
        Rect hexRect, Rect asciiRect, int col,
        bool above, bool below, bool left, bool right)
    {
        var r = HighlightCornerRadius;
        var hexLeft = left && col != 8;
        var hexRight = right && col != 7;

        FillRoundedRect(context, brush, hexRect,
            above || hexLeft ? 0 : r, above || hexRight ? 0 : r,
            below || hexRight ? 0 : r, below || hexLeft ? 0 : r);
        FillRoundedRect(context, brush, asciiRect,
            above || left ? 0 : r, above || right ? 0 : r,
            below || right ? 0 : r, below || left ? 0 : r);
    }

    private static void FillRoundedRect(DrawingContext context, IBrush brush, Rect rect,
        double tl, double tr, double br, double bl)
    {
        if (tl <= 0 && tr <= 0 && br <= 0 && bl <= 0)
        {
            context.FillRectangle(brush, rect);
            return;
        }

        if (tl > 0 && tr > 0 && br > 0 && bl > 0)
        {
            context.DrawRectangle(brush, null, new RoundedRect(rect, tl));
            return;
        }

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            double x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
            ctx.BeginFigure(new Point(x + tl, y), true);
            ctx.LineTo(new Point(x + w - tr, y));
            if (tr > 0) ctx.ArcTo(new Point(x + w, y + tr), new Size(tr, tr), 0, false, SweepDirection.Clockwise);
            else ctx.LineTo(new Point(x + w, y));
            ctx.LineTo(new Point(x + w, y + h - br));
            if (br > 0) ctx.ArcTo(new Point(x + w - br, y + h), new Size(br, br), 0, false, SweepDirection.Clockwise);
            else ctx.LineTo(new Point(x + w, y + h));
            ctx.LineTo(new Point(x + bl, y + h));
            if (bl > 0) ctx.ArcTo(new Point(x, y + h - bl), new Size(bl, bl), 0, false, SweepDirection.Clockwise);
            else ctx.LineTo(new Point(x, y + h));
            ctx.LineTo(new Point(x, y + tl));
            if (tl > 0) ctx.ArcTo(new Point(x + tl, y), new Size(tl, tl), 0, false, SweepDirection.Clockwise);
            else ctx.LineTo(new Point(x, y));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geo);
    }

    private static void DrawText(DrawingContext context, string text, double x, double y,
        Typeface typeface, IBrush brush)
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, FontSize, brush);
        context.DrawText(formatted, new Point(x, y));
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

        DrawText(context, new string(buf), _hexStartX, y, typeface, TextBrush);
    }

    private void DrawAscii(DrawingContext context, byte[] data, int byteOffset, int bytesInLine,
        double y, Typeface typeface)
    {
        for (var i = 0; i < bytesInLine; i++)
        {
            var b = data[byteOffset + i];
            var ch = b is >= 0x20 and <= 0x7E ? (char)b : '.';
            var formatted = new FormattedText(ch.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, TextBrush);
            var x = AsciiCellX(i) + CellPaddingX;
            context.DrawText(formatted, new Point(x, y));
        }
    }

    private static char ToHexChar(int nibble) =>
        (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);

    private int HitTestByte(Point point)
    {
        EnsureMetrics();

        var data = Data;
        if (data is null || point.Y < _dataTop) return -1;

        var line = (int)((point.Y - _dataTop + _scrollOffset.Y) / _rowHeight);
        if (line < 0 || line >= TotalLines) return -1;

        int byteInLine;

        var asciiEndX = AsciiCellX(BytesPerLine);
        if (point.X >= _asciiStartX && point.X < asciiEndX)
        {
            byteInLine = (int)((point.X - _asciiStartX) / _asciiCellWidth);
        }
        else if (point.X >= _hexStartX && point.X < _hexEndX)
        {
            var relChar = (point.X - _hexStartX) / _charWidth;
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
            SelectionLength = Data is { } d && byteIndex < d.Length ? 1 : 0;
        }

        _isDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var byteIndex = HitTestByte(e.GetPosition(this));

        if (_isDragging)
        {
            if (byteIndex < 0) return;
            UpdateSelection(_selectionAnchor, byteIndex);
            e.Handled = true;
            return;
        }

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
                HandleDelete(e.Key, data);
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

    private void EnsureCursorVisible()
    {
        EnsureMetrics();

        var cursorLine = CursorPosition / BytesPerLine;
        var cursorY = cursorLine * _rowHeight;
        var viewTop = _scrollOffset.Y;
        var viewHeight = _scrollViewport.Height - _dataTop;
        var viewBottom = viewTop + viewHeight;

        if (cursorY < viewTop)
            ((IScrollable)this).Offset = new Vector(_scrollOffset.X, cursorY);
        else if (cursorY + _rowHeight > viewBottom)
            ((IScrollable)this).Offset = new Vector(_scrollOffset.X, cursorY + _rowHeight - viewHeight);
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
            SelectionLength = newPos < maxIndex ? 1 : 0;
            _selectionAnchor = newPos;
            EnsureCursorVisible();
        }
    }

    private void HandleDelete(Key key, byte[] data)
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
            if (CursorPosition >= data.Length) return;
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

        var newPos = Math.Clamp(deleteIndex, 0, data.Length - deleteCount);

        RaiseEvent(new BytesDeletedEventArgs(BytesDeletedEvent, this, deleteIndex, deleteCount));

        _editingHighNibble = false;
        CursorPosition = newPos;
        SelectionStart = newPos;
        SelectionLength = newPos < data.Length - deleteCount ? 1 : 0;
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

    public event EventHandler? ScrollInvalidated;

    bool ILogicalScrollable.CanHorizontallyScroll { get; set; }
    bool ILogicalScrollable.CanVerticallyScroll { get; set; }
    bool ILogicalScrollable.IsLogicalScrollEnabled => true;
    Size ILogicalScrollable.ScrollSize => new(_charWidth * 4, _rowHeight);
    Size ILogicalScrollable.PageScrollSize => new(_scrollViewport.Width, Math.Max(_rowHeight, _scrollViewport.Height - _rowHeight * 2));
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

public class BytesDeletedEventArgs(RoutedEvent routedEvent, object source, int index, int count)
    : RoutedEventArgs(routedEvent, source)
{
    public int Index { get; } = index;
    public int Count { get; } = count;
}
