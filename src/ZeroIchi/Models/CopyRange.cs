using System;

namespace ZeroIchi.Models;

public static class CopyRange
{
    public static long EffectiveLength(long selectionLength, long selectionStart, long bufferLength) =>
        Math.Clamp(selectionLength, 0, Math.Max(0, bufferLength - selectionStart));

    public static bool CanCopy(long selectionLength, long selectionStart, long bufferLength) =>
        EffectiveLength(selectionLength, selectionStart, bufferLength) <= Array.MaxLength;
}
