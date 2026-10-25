using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileTypes;

internal sealed class MagicEvaluator(ByteBuffer buffer)
{
    public const int MagicReadLimit = 64 * 1024;
    public const int RegexWindow = 8 * 1024;

    private const int ReadChunk = 4 * 1024;

    private static readonly long Budget = Stopwatch.Frequency * 5 / 1000;
    private static readonly ConditionalWeakTable<MagicMatch, Regex> Regexes = new();

    private readonly int _available = (int)Math.Min(buffer.Length, MagicReadLimit);
    private byte[] _data = [];
    private int _loaded;
    private string _text = "";
    private long _elapsed;

    public int RegexAttempts { get; private set; }

    public bool Matches(MagicMatch match)
    {
        switch (match.Kind)
        {
            case MagicMatchKind.Unsupported:
                return false;
            case MagicMatchKind.MinShouldMatch:
            {
                var count = 0;
                foreach (var child in match.Children)
                {
                    if (Matches(child) && ++count >= match.MinShouldMatch) return true;
                }
                return false;
            }
        }

        if (!MatchesSelf(match)) return false;
        if (match.Children.Length == 0) return true;
        foreach (var child in match.Children)
        {
            if (Matches(child)) return true;
        }
        return false;
    }

    private bool MatchesSelf(MagicMatch match) => match.Kind switch
    {
        MagicMatchKind.Bytes => MatchesBytes(match, ignoreCase: false),
        MagicMatchKind.BytesIgnoreCase => MatchesBytes(match, ignoreCase: true),
        MagicMatchKind.Regex => MatchesRegex(match),
        _ => false,
    };

    private bool MatchesBytes(MagicMatch match, bool ignoreCase)
    {
        var length = match.Pattern.Length;
        if ((long)match.OffsetStart + length > _available) return false;
        var last = (int)Math.Min(match.OffsetEnd, _available - length);
        Load(last + length);

        var pattern = match.Pattern;
        var mask = match.Mask;
        for (var start = match.OffsetStart; start <= last; start++)
        {
            var found = true;
            for (var i = 0; i < length; i++)
            {
                var value = _data[start + i];
                if (ignoreCase && value is >= (byte)'A' and <= (byte)'Z') value += 'a' - 'A';
                if ((value & mask[i]) != pattern[i])
                {
                    found = false;
                    break;
                }
            }
            if (found) return true;
        }
        return false;
    }

    private bool MatchesRegex(MagicMatch match)
    {
        if (match.OffsetStart >= _available) return false;
        var last = (int)Math.Min(match.OffsetEnd, _available - 1);
        Load((int)Math.Min(_available, (long)last + RegexWindow));
        if (_text.Length < _loaded) _text = Encoding.Latin1.GetString(_data, 0, _loaded);

        var regex = Regexes.GetValue(match, Compile);
        for (var start = match.OffsetStart; start <= last; start++)
        {
            if (_elapsed >= Budget) return false;
            RegexAttempts++;
            var begin = Stopwatch.GetTimestamp();
            try
            {
                if (regex.Match(_text, start, Math.Min(RegexWindow, _loaded - start)).Success) return true;
            }
            catch (RegexMatchTimeoutException)
            {
            }
            finally
            {
                _elapsed += Stopwatch.GetTimestamp() - begin;
            }
        }
        return false;
    }

    private static Regex Compile(MagicMatch match) => MagicRegex.Create(match.Regex!);

    private void Load(int end)
    {
        if (end <= _loaded) return;
        var target = Math.Min(_available, Math.Max(end, Math.Max(_loaded * 2, ReadChunk)));
        if (target > _data.Length) Array.Resize(ref _data, target);
        buffer.ReadBytes(_loaded, _data, _loaded, target - _loaded);
        _loaded = target;
    }
}
