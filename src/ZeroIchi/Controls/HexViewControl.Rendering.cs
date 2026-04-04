using Avalonia;
using Avalonia.Media;
using System;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Controls;

public partial class HexViewControl
{
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
        if (Buffer is not null)
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
    }

    private void DrawAddressHighlight(DrawingContext context, int byteOffset,
        double y, int selStart, int selEnd, int dataLength)
    {
        var cursor = CursorPosition;
        var hasSelection = SelectionLength > 0;
        var bytesInLine = Math.Max(0, Math.Min(BytesPerLine, dataLength - byteOffset));
        var cursorOnLine = cursor / BytesPerLine == byteOffset / BytesPerLine;
        var selectionOnLine = hasSelection && selStart < byteOffset + bytesInLine && selEnd > byteOffset;

        if (!cursorOnLine && !selectionOnLine) return;

        var addrAbove = IsAddrRowHighlighted(byteOffset - BytesPerLine);
        var addrBelow = IsAddrRowHighlighted(byteOffset + BytesPerLine);
        var r = HighlightCornerRadius;

        var offsetRect = new Rect(0, y, OffsetChars * _charWidth + 2 * CellPaddingX, _rowHeight);
        FillRoundedRect(context, cursorOnLine ? CursorBgBrush : SelectionBgBrush, offsetRect,
            addrAbove ? 0 : r, addrAbove ? 0 : r, addrBelow ? 0 : r, addrBelow ? 0 : r);
        return;

        bool IsAddrRowHighlighted(int rowOffset)
        {
            if (rowOffset < 0 || rowOffset > dataLength) return false;
            if (cursor / BytesPerLine == rowOffset / BytesPerLine) return true;
            var rowEnd = Math.Min(rowOffset + BytesPerLine, dataLength);
            return hasSelection && selStart < rowEnd && selEnd > rowOffset;
        }
    }

    private void DrawContentHighlights(DrawingContext context, int byteOffset, int bytesInLine,
        double y, int selStart, int selEnd)
    {
        var cursor = CursorPosition;
        var hasSelection = SelectionLength > 0;
        var doc = Document;
        var hovered = _hoveredByteIndex;
        var buffer = Buffer;
        var dataLength = (int)(buffer?.Length ?? 0);

        var matchOffsets = SearchMatchOffsets;
        var matchLength = SearchMatchLength;
        var currentMatchIndex = CurrentSearchMatchIndex;
        if (matchOffsets.Length > 0 && matchLength > 0)
        {
            var lineEnd = byteOffset + bytesInLine;
            var firstMatch = FindFirstMatchInRange(matchOffsets, byteOffset - matchLength + 1);
            for (var m = firstMatch; m < matchOffsets.Length; m++)
            {
                var matchStart = matchOffsets[m];
                if (matchStart >= lineEnd) break;
                var matchEnd = matchStart + matchLength;
                var drawStart = Math.Max(matchStart, byteOffset);
                var drawEnd = Math.Min(matchEnd, lineEnd);
                var brush = m == currentMatchIndex ? CurrentSearchMatchBrush : SearchMatchBrush;

                for (var byteIdx = drawStart; byteIdx < drawEnd; byteIdx++)
                {
                    var i = byteIdx - byteOffset;
                    var hexRect = HexCellRect(i, y);
                    var asciiRect = new Rect(AsciiCellX(i), y, _asciiCellWidth, _rowHeight);
                    context.DrawRectangle(brush, null, new RoundedRect(hexRect, HighlightCornerRadius));
                    context.DrawRectangle(brush, null, new RoundedRect(asciiRect, HighlightCornerRadius));
                }
            }
        }

        for (var i = 0; i < bytesInLine; i++)
        {
            var byteIndex = byteOffset + i;
            var isCursor = byteIndex == cursor;
            var isSelected = hasSelection && byteIndex >= selStart && byteIndex < selEnd;
            var isModified = doc?.IsByteModified(byteIndex) == true;
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
                    index >= 0 && index < dataLength && doc!.IsByteModified(index);

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

        if (buffer is not null && cursor == dataLength)
        {
            var appendInLine = dataLength - byteOffset;
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

    private static IBrush GetStructureBrush(StructureColorMap? map, int fileOffset, byte value)
    {
        if (map is not null)
        {
            var kind = map.GetValueKind(fileOffset);
            if (kind == ValueKind.Numeric) return NumericTextBrush;
            if (kind == ValueKind.Ascii) return AsciiTextBrush;
        }
        return value == 0 ? ZeroByteBrush : TextBrush;
    }

    private void DrawHexBytes(DrawingContext context, byte[] data, int bufferOffset, int fileOffset,
        int bytesInLine, double y, Typeface typeface)
    {
        var map = StructureColors;
        Span<char> colorBuf = stackalloc char[HexChars];
        var runStart = 0;
        var runBrush = GetStructureBrush(map, fileOffset, data[bufferOffset]);

        for (var i = 1; i <= bytesInLine; i++)
        {
            var brush = i < bytesInLine ? GetStructureBrush(map, fileOffset + i, data[bufferOffset + i]) : null;
            if (ReferenceEquals(brush, runBrush) && i < bytesInLine) continue;

            colorBuf.Fill(' ');
            for (var j = runStart; j < i; j++)
            {
                var charPos = j * 3 + (j >= 8 ? 1 : 0);
                var b = data[bufferOffset + j];
                colorBuf[charPos] = ToHexChar(b >> 4);
                colorBuf[charPos + 1] = ToHexChar(b & 0xF);
            }
            DrawText(context, new string(colorBuf), _hexStartX, y, typeface, runBrush);

            runStart = i;
            runBrush = brush!;
        }
    }

    private void DrawAscii(DrawingContext context, byte[] data, int bufferOffset, int fileOffset,
        int bytesInLine, double y, Typeface typeface)
    {
        var map = StructureColors;
        for (var i = 0; i < bytesInLine; i++)
        {
            var b = data[bufferOffset + i];
            var ch = b is >= 0x20 and <= 0x7E ? (char)b : '.';
            var brush = GetStructureBrush(map, fileOffset + i, b);
            var formatted = new FormattedText(ch.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, brush);
            var x = AsciiCellX(i) + CellPaddingX;
            context.DrawText(formatted, new Point(x, y));
        }
    }

    private static char ToHexChar(int nibble) =>
        (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);

    private static int FindFirstMatchInRange(int[] offsets, int rangeStart)
    {
        var lo = 0;
        var hi = offsets.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (offsets[mid] < rangeStart)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
