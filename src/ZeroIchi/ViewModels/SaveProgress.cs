using System;
using System.Globalization;
using System.Threading;
using ZeroIchi.Localization;

namespace ZeroIchi.ViewModels;

internal sealed class SaveProgress(long total) : IProgress<long>
{
    private long _written;

    public string Text => FormatText(Volatile.Read(ref _written), total);

    public void Report(long value) => Volatile.Write(ref _written, value);

    internal static string FormatText(long written, long total)
    {
        var percent = total <= 0 ? 100 : Math.Clamp(written * 100 / total, 0, 100);
        return string.Format(CultureInfo.CurrentCulture, Strings.SaveProgress, percent);
    }
}
