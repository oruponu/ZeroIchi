using System;
using System.Threading;

namespace ZeroIchi.ViewModels;

internal sealed class SaveProgress(long total) : IProgress<long>
{
    private long _written;

    public string Text => FormatText(Volatile.Read(ref _written), total);

    public void Report(long value) => Volatile.Write(ref _written, value);

    internal static string FormatText(long written, long total)
    {
        var percent = total <= 0 ? 100 : Math.Clamp(written * 100 / total, 0, 100);
        return $"保存中 {percent}%";
    }
}
