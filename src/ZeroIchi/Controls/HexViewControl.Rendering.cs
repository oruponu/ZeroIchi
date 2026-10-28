using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
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
            var charPos = HexCharIndex(i);
            buf[charPos] = ToHexChar(i >> 4);
            buf[charPos + 1] = ToHexChar(i & 0xF);
        }

        return new string(buf);
    }

    private void DrawHeader(DrawingContext context)
    {
        if (Buffer is not null)
        {
            var cursorCol = (int)(CursorPosition % BytesPerLine);
            var hasSelection = SelectionLength > 0;
            var selFirstCol = 0;
            var selLastCol = 0;
            long selLineSpan = 0;

            if (hasSelection)
            {
                var selEnd = SelectionStart + SelectionLength - 1;
                selFirstCol = (int)(SelectionStart % BytesPerLine);
                selLastCol = (int)(selEnd % BytesPerLine);
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

    private void DrawAddressHighlight(DrawingContext context, long byteOffset,
        double y, long selStart, long selEnd, long dataLength)
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

        var offsetRect = new Rect(0, y, _offsetChars * _charWidth + 2 * CellPaddingX, _rowHeight);
        FillRoundedRect(context, cursorOnLine ? CursorBgBrush : SelectionBgBrush, offsetRect,
            addrAbove ? 0 : r, addrAbove ? 0 : r, addrBelow ? 0 : r, addrBelow ? 0 : r);
        return;

        bool IsAddrRowHighlighted(long rowOffset)
        {
            if (rowOffset < 0 || rowOffset > dataLength) return false;
            if (cursor / BytesPerLine == rowOffset / BytesPerLine) return true;
            var rowEnd = Math.Min(rowOffset + BytesPerLine, dataLength);
            return hasSelection && selStart < rowEnd && selEnd > rowOffset;
        }
    }

    private void DrawContentHighlights(DrawingContext context, long byteOffset, int bytesInLine,
        double y, long selStart, long selEnd)
    {
        var cursor = CursorPosition;
        var doc = Document;
        var hovered = _hoveredByteIndex;
        var dataLength = Buffer?.Length ?? 0;

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
                    var i = (int)(byteIdx - byteOffset);
                    var hexRect = HexCellRect(i, y);
                    var asciiRect = new Rect(AsciiCellX(i), y, _asciiCellWidth, _rowHeight);
                    context.DrawRectangle(brush, null, new RoundedRect(hexRect, HighlightCornerRadius));
                    context.DrawRectangle(brush, null, new RoundedRect(asciiRect, HighlightCornerRadius));
                }
            }
        }

        var highlighted = HighlightMask(byteOffset, cursor, selStart, selEnd, dataLength);

        for (var i = 0; i < bytesInLine; i++)
        {
            var byteIndex = byteOffset + i;
            var isModified = doc?.IsByteModified(byteIndex) == true;
            var isHovered = byteIndex == hovered;
            var isHighlighted = IsSet(highlighted, i);

            if (!isHighlighted && !isModified && !isHovered) continue;

            var hexRect = HexCellRect(i, y);
            var asciiRect = new Rect(AsciiCellX(i), y, _asciiCellWidth, _rowHeight);

            if (isHovered && !isHighlighted)
            {
                context.DrawRectangle(HoverBgBrush, null, new RoundedRect(hexRect, HighlightCornerRadius));
                context.DrawRectangle(HoverBgBrush, null, new RoundedRect(asciiRect, HighlightCornerRadius));
            }

            if (isModified)
            {
                bool IsModifiedNeighbor(long index) =>
                    index >= 0 && index < dataLength && doc!.IsByteModified(index);

                DrawMergedCells(context, ModifiedBgBrush, hexRect, asciiRect, i,
                    byteIndex >= BytesPerLine && IsModifiedNeighbor(byteIndex - BytesPerLine),
                    byteIndex + BytesPerLine < dataLength && IsModifiedNeighbor(byteIndex + BytesPerLine),
                    i > 0 && IsModifiedNeighbor(byteIndex - 1),
                    i < BytesPerLine - 1 && IsModifiedNeighbor(byteIndex + 1));
            }
        }

        if (highlighted == 0) return;

        var above = HighlightMask(byteOffset - BytesPerLine, cursor, selStart, selEnd, dataLength);
        var below = HighlightMask(byteOffset + BytesPerLine, cursor, selStart, selEnd, dataLength);
        var cursorMask = cursor - byteOffset is >= 0 and < BytesPerLine ? 1 << (int)(cursor - byteOffset) : 0;
        FillCellRuns(context, SelectionBgBrush, y, highlighted & ~cursorMask, highlighted, above, below);
        FillCellRuns(context, CursorBgBrush, y, highlighted & cursorMask, highlighted, above, below);
    }

    // Bit i is set when byte i of the row is the cursor (including the cell past the end) or selected
    private static int HighlightMask(long rowOffset, long cursor, long selStart, long selEnd, long dataLength)
    {
        if (rowOffset < 0 || rowOffset > dataLength) return 0;

        var mask = 0;
        var from = Math.Max(selStart, rowOffset);
        var to = Math.Min(selEnd, Math.Min(rowOffset + BytesPerLine, dataLength));
        if (from < to)
            mask = ((1 << (int)(to - from)) - 1) << (int)(from - rowOffset);

        if (cursor <= dataLength && cursor - rowOffset is >= 0 and < BytesPerLine)
            mask |= 1 << (int)(cursor - rowOffset);
        return mask;
    }

    private static bool IsSet(int mask, int column) => (mask & (1 << column)) != 0;

    // Hex cells are not joined across the boundary between bytes 7 and 8
    private void FillCellRuns(DrawingContext context, IBrush brush, double y,
        int cells, int joined, int above, int below)
    {
        for (var first = 0; first < BytesPerLine; first++)
        {
            if (!IsSet(cells, first)) continue;

            var last = first;
            while (last < BytesPerLine - 1 && IsSet(cells, last + 1)) last++;

            var left = first > 0 && IsSet(joined, first - 1);
            var right = last < BytesPerLine - 1 && IsSet(joined, last + 1);

            if (first < 8 && last >= 8)
            {
                FillHexRun(first, 7, left, false);
                FillHexRun(8, last, false, right);
            }
            else
            {
                FillHexRun(first, last, left && first != 8, right && last != 7);
            }

            FillRun(new Rect(AsciiCellX(first), y, (last - first + 1) * _asciiCellWidth, _rowHeight),
                first, last, left, right);

            first = last;
        }

        return;

        void FillHexRun(int from, int to, bool hasLeft, bool hasRight) =>
            FillRun(new Rect(HexCellRect(from, y).TopLeft, HexCellRect(to, y).BottomRight), from, to, hasLeft, hasRight);

        void FillRun(Rect rect, int from, int to, bool hasLeft, bool hasRight)
        {
            var r = HighlightCornerRadius;
            FillRoundedRect(context, brush, rect,
                IsSet(above, from) || hasLeft ? 0 : r, IsSet(above, to) || hasRight ? 0 : r,
                IsSet(below, to) || hasRight ? 0 : r, IsSet(below, from) || hasLeft ? 0 : r);
        }
    }

    // Cells are not merged across the boundary between bytes 7 and 8
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

    private static IBrush GetStructureBrush(StructureColorMap? map, long fileOffset, byte value)
    {
        if (map is not null)
        {
            var kind = map.GetValueKind(fileOffset);
            if (kind == ValueKind.Numeric) return NumericTextBrush;
            if (kind == ValueKind.Ascii) return AsciiTextBrush;
        }
        return value == 0 ? ZeroByteBrush : TextBrush;
    }

    private void FillRowBrushes(byte[] data, int bufferOffset, long fileOffset, int bytesInLine)
    {
        var map = StructureColors;
        for (var i = 0; i < bytesInLine; i++)
            _rowBrushes[i] = GetStructureBrush(map, fileOffset + i, data[bufferOffset + i]);
    }

    private bool IsFirstOfBrush(int column)
    {
        for (var i = 0; i < column; i++)
        {
            if (ReferenceEquals(_rowBrushes[i], _rowBrushes[column])) return false;
        }
        return true;
    }

    private void DrawHexBytes(DrawingContext context, byte[] data, int bufferOffset, int bytesInLine, double y)
    {
        Span<char> chars = stackalloc char[BytesPerLine * 2];
        Span<double> xs = stackalloc double[BytesPerLine * 2];
        for (var first = 0; first < bytesInLine; first++)
        {
            if (!IsFirstOfBrush(first)) continue;

            var count = 0;
            for (var i = first; i < bytesInLine; i++)
            {
                if (!ReferenceEquals(_rowBrushes[i], _rowBrushes[first])) continue;
                var b = data[bufferOffset + i];
                var x = _hexStartX + HexCharIndex(i) * _charWidth;
                chars[count] = ToHexChar(b >> 4);
                xs[count++] = x;
                chars[count] = ToHexChar(b & 0xF);
                xs[count++] = x + _charWidth;
            }
            DrawGlyphs(context, _rowBrushes[first], chars[..count], xs[..count], y);
        }
    }

    private void DrawAscii(DrawingContext context, byte[] data, int bufferOffset, int bytesInLine, double y)
    {
        Span<char> chars = stackalloc char[BytesPerLine];
        Span<double> xs = stackalloc double[BytesPerLine];
        for (var first = 0; first < bytesInLine; first++)
        {
            if (!IsFirstOfBrush(first)) continue;

            var count = 0;
            for (var i = first; i < bytesInLine; i++)
            {
                if (!ReferenceEquals(_rowBrushes[i], _rowBrushes[first])) continue;
                chars[count] = ToAsciiChar(data[bufferOffset + i]);
                xs[count++] = AsciiCellX(i) + CellPaddingX;
            }
            DrawGlyphs(context, _rowBrushes[first], chars[..count], xs[..count], y);
        }
    }

    // Glyph runs with precomputed glyph indices skip text shaping, which is too slow to run for every row each frame
    private void DrawGlyphs(DrawingContext context, IBrush brush, ReadOnlySpan<char> chars, ReadOnlySpan<double> xs, double y)
    {
        var glyphs = new GlyphInfo[chars.Length];
        for (var k = 0; k < chars.Length; k++)
        {
            var advance = k + 1 < chars.Length ? xs[k + 1] - xs[k] : _charWidth;
            glyphs[k] = new GlyphInfo(_glyphIndices[chars[k]], k, advance);
        }

        using var run = new GlyphRun(MonospaceTypeface.GlyphTypeface, FontSize, chars.ToArray(), glyphs,
            new Point(xs[0], y + _baseline));
        context.DrawGlyphRun(brush, run);
    }

    private static char ToAsciiChar(byte b) => b is >= 0x20 and <= 0x7E ? (char)b : '.';

    private static ushort[] BuildGlyphIndices()
    {
        var map = MonospaceTypeface.GlyphTypeface.CharacterToGlyphMap;
        var glyphs = new ushort[0x7F];
        for (var c = 0x20; c < glyphs.Length; c++)
            glyphs[c] = map.GetGlyph(c);
        return glyphs;
    }

    private static char ToHexChar(int nibble) =>
        (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);

    private static int FindFirstMatchInRange(long[] offsets, long rangeStart)
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
