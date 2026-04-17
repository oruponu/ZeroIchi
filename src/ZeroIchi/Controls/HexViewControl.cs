using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Buffers;
using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Controls;

public partial class HexViewControl : Control, ILogicalScrollable
{
    public static readonly StyledProperty<BinaryDocument?> DocumentProperty =
        AvaloniaProperty.Register<HexViewControl, BinaryDocument?>(nameof(Document));

    public static readonly StyledProperty<int> DataVersionProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(DataVersion));

    public static readonly StyledProperty<int> CursorPositionProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(CursorPosition), defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(SelectionStart), defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectionLengthProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(SelectionLength), defaultValue: 0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int[]> SearchMatchOffsetsProperty =
        AvaloniaProperty.Register<HexViewControl, int[]>(nameof(SearchMatchOffsets), defaultValue: []);

    public static readonly StyledProperty<int> SearchMatchLengthProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(SearchMatchLength), defaultValue: 0);

    public static readonly StyledProperty<int> CurrentSearchMatchIndexProperty =
        AvaloniaProperty.Register<HexViewControl, int>(nameof(CurrentSearchMatchIndex), defaultValue: -1);

    public static readonly StyledProperty<StructureColorMap?> StructureColorsProperty =
        AvaloniaProperty.Register<HexViewControl, StructureColorMap?>(nameof(StructureColors));

    public static readonly RoutedEvent<ByteModifiedEventArgs> ByteModifiedEvent =
        RoutedEvent.Register<HexViewControl, ByteModifiedEventArgs>(nameof(ByteModified), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<BytesDeletedEventArgs> BytesDeletedEvent =
        RoutedEvent.Register<HexViewControl, BytesDeletedEventArgs>(nameof(BytesDeleted), RoutingStrategies.Bubble);

    private const int BytesPerLine = 16;
    private const string MonoFontFamily = "fonts:App#Roboto Mono";
    private const double FontSize = 13;
    private const double CellPaddingX = 4;
    private const double CellPaddingY = 2;
    private const double HighlightCornerRadius = 4;

    private const int OffsetChars = 8;
    private const int HexChars = 49;
    private const double ColumnGap = 16;

    private static readonly Typeface MonospaceTypeface = new(MonoFontFamily);
    private static readonly IBrush OffsetBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)).ToImmutable();
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)).ToImmutable();
    private static readonly IBrush CursorBgBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x4F, 0x78)).ToImmutable();
    private static readonly IBrush SelectionBgBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x26, 0x4F, 0x78)).ToImmutable();
    private static readonly IBrush ModifiedBgBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0x8C, 0x00)).ToImmutable();
    private static readonly IBrush HoverBgBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)).ToImmutable();
    private static readonly IBrush SearchMatchBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xE8, 0xA8, 0x00)).ToImmutable();
    private static readonly IBrush CurrentSearchMatchBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xE8, 0xA8, 0x00)).ToImmutable();
    private static readonly IBrush NumericTextBrush = new SolidColorBrush(Color.Parse("#B5CEA8")).ToImmutable();
    private static readonly IBrush AsciiTextBrush = new SolidColorBrush(Color.Parse("#CE9178")).ToImmutable();
    private static readonly IBrush ZeroByteBrush = new SolidColorBrush(Color.Parse("#606060")).ToImmutable();
    private static readonly Pen HeaderSeparatorPen = new(new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)).ToImmutable());
    private static readonly string HeaderHexText = BuildHeaderHexText();

    static HexViewControl()
    {
        AffectsRender<HexViewControl>(DocumentProperty, DataVersionProperty, CursorPositionProperty,
            SelectionStartProperty, SelectionLengthProperty,
            SearchMatchOffsetsProperty, SearchMatchLengthProperty, CurrentSearchMatchIndexProperty,
            StructureColorsProperty);
        AffectsMeasure<HexViewControl>(DocumentProperty, DataVersionProperty);
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
    private Avalonia.Threading.DispatcherTimer? _autoScrollTimer;
    private Point _lastPointerPosition;

    public HexViewControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public BinaryDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public int DataVersion
    {
        get => GetValue(DataVersionProperty);
        set => SetValue(DataVersionProperty, value);
    }

    private ByteBuffer? Buffer => Document?.Buffer;

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

    public int[] SearchMatchOffsets
    {
        get => GetValue(SearchMatchOffsetsProperty);
        set => SetValue(SearchMatchOffsetsProperty, value);
    }

    public int SearchMatchLength
    {
        get => GetValue(SearchMatchLengthProperty);
        set => SetValue(SearchMatchLengthProperty, value);
    }

    public int CurrentSearchMatchIndex
    {
        get => GetValue(CurrentSearchMatchIndexProperty);
        set => SetValue(CurrentSearchMatchIndexProperty, value);
    }

    public StructureColorMap? StructureColors
    {
        get => GetValue(StructureColorsProperty);
        set => SetValue(StructureColorsProperty, value);
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

        if (change.Property == DocumentProperty)
        {
            CursorPosition = 0;
            SelectionStart = 0;
            SelectionLength = 0;
            _selectionAnchor = 0;
            _editingHighNibble = false;
            _scrollOffset = default;
            InvalidateScrollable();
        }
        else if (change.Property == DataVersionProperty)
        {
            InvalidateScrollable();
        }
        else if (change.Property == CursorPositionProperty)
        {
            EnsureCursorVisible();
        }
        else if (change.Property == CurrentSearchMatchIndexProperty)
        {
            EnsureCursorVisible();
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

    private int TotalLines => Buffer is { } b ? (int)(b.Length / BytesPerLine) + 1 : 0;

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

        var separatorY = Math.Floor((_rowHeight + _dataTop) / 2) - 0.5;
        context.DrawLine(HeaderSeparatorPen, new Point(0, separatorY), new Point(Bounds.Width, separatorY));

        var buffer = Buffer;
        var dataLength = buffer is null ? 0 : (int)buffer.Length;
        var dataTop = _dataTop;
        var firstLine = Math.Max(0, (int)(_scrollOffset.Y / _rowHeight));
        var visibleLineCount = (int)((Bounds.Height - dataTop) / _rowHeight) + 2;
        var lastLine = buffer is null ? 0 : Math.Min(dataLength / BytesPerLine + 1, firstLine + visibleLineCount);

        var selStart = SelectionStart;
        var selEnd = selStart + SelectionLength;

        var contentLeft = _hexStartX - CellPaddingX;
        using (context.PushClip(new Rect(contentLeft, 0, Bounds.Width - contentLeft, Bounds.Height)))
        using (context.PushTransform(Matrix.CreateTranslation(-_scrollOffset.X, 0)))
        {
            DrawHeader(context);

            if (buffer is not null)
            {
                var visibleBytes = (lastLine - firstLine) * BytesPerLine;
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(visibleBytes);
                try
                {
                    var startOffset = firstLine * BytesPerLine;
                    var bytesToRead = Math.Min(visibleBytes, dataLength - startOffset);
                    if (bytesToRead > 0)
                        buffer.ReadBytes(startOffset, rentedBuffer, 0, bytesToRead);

                    using (context.PushClip(new Rect(_scrollOffset.X + contentLeft, dataTop,
                        Bounds.Width - contentLeft, Bounds.Height - dataTop)))
                    {
                        for (var line = firstLine; line < lastLine; line++)
                        {
                            var y = dataTop + line * _rowHeight - _scrollOffset.Y;
                            var byteOffset = line * BytesPerLine;
                            var bytesInLine = Math.Max(0, Math.Min(BytesPerLine, dataLength - byteOffset));

                            DrawContentHighlights(context, byteOffset, bytesInLine, y, selStart, selEnd);
                            var textY = y + CellPaddingY;
                            if (bytesInLine > 0)
                            {
                                var bufferOffset = byteOffset - startOffset;
                                DrawHexBytes(context, rentedBuffer, bufferOffset, byteOffset, bytesInLine, textY, MonospaceTypeface);
                                DrawAscii(context, rentedBuffer, bufferOffset, byteOffset, bytesInLine, textY, MonospaceTypeface);
                            }
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
        }

        using (context.PushClip(new Rect(0, dataTop, contentLeft, Bounds.Height - dataTop)))
        {
            for (var line = firstLine; line < lastLine; line++)
            {
                var y = dataTop + line * _rowHeight - _scrollOffset.Y;
                var byteOffset = line * BytesPerLine;

                DrawAddressHighlight(context, byteOffset, y, selStart, selEnd, dataLength);
                DrawText(context, byteOffset.ToString("X8"), CellPaddingX, y + CellPaddingY, MonospaceTypeface, OffsetBrush);
            }
        }
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

    public event EventHandler? ScrollInvalidated;

    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll;

    bool IScrollable.CanHorizontallyScroll => _canHorizontallyScroll;
    bool IScrollable.CanVerticallyScroll => _canVerticallyScroll;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set => _canHorizontallyScroll = value;
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set => _canVerticallyScroll = value;
    }

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
            InvalidateScrollable();
        }
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;
    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;
    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    private void InvalidateScrollable() => ScrollInvalidated?.Invoke(this, EventArgs.Empty);
}
