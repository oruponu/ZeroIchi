using System;
using System.Collections.Generic;
using System.Linq;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileTypes;

internal sealed record DetectedFileType(string MimeType, string Extension);

internal sealed class FileTypeDetector
{
    private static readonly Lazy<FileTypeDetector> LazyDefault =
        new(() => new FileTypeDetector([.. TikaMimeRules.Create(), .. ContainerDetector.ExtraTypes]));

    private readonly Dictionary<string, MimeTypeEntry> _types;
    private readonly (string Name, MimeMagic Magic)[] _magics;

    public FileTypeDetector(IReadOnlyList<MimeTypeEntry> types)
    {
        _types = types.ToDictionary(type => type.Name, StringComparer.Ordinal);
        _magics = types
            .SelectMany(type => type.Magics.Select(magic => (type.Name, Magic: magic)))
            .OrderByDescending(pair => pair.Magic.Priority)
            .ThenByDescending(pair => pair.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static FileTypeDetector Default => LazyDefault.Value;

    public DetectedFileType? Detect(ByteBuffer buffer) => DetectWithStatistics(buffer).Result;

    internal (DetectedFileType? Result, int RegexAttempts) DetectWithStatistics(ByteBuffer buffer)
    {
        if (buffer.Length == 0) return (null, 0);

        var container = ContainerDetector.Detect(buffer, _types.ContainsKey);
        if (container is not null && _types.ContainsKey(container))
        {
            return (new DetectedFileType(container, Extension(container)), 0);
        }

        var evaluator = new MagicEvaluator(buffer);
        var candidates = new List<string>();
        int? tier = null;
        foreach (var (name, magic) in _magics)
        {
            if (magic.Priority < tier) break;
            if (candidates.Contains(name) || !evaluator.Matches(magic.Clause)) continue;
            candidates.Add(name);
            tier = magic.Priority;
        }

        string? best = null;
        foreach (var candidate in candidates)
        {
            if (candidates.Any(other => other != candidate && IsA(other, candidate))) continue;
            if (best is null || string.CompareOrdinal(candidate, best) > 0) best = candidate;
        }
        return (best is null ? null : new DetectedFileType(best, Extension(best)), evaluator.RegexAttempts);
    }

    public string Extension(string mimeType)
    {
        foreach (var type in SelfAndAncestors(mimeType))
        {
            if (type.Extensions.Length > 0) return type.Extensions[0];
        }
        return "";
    }

    public bool IsA(string mimeType, string ancestorOrSelf) =>
        mimeType == ancestorOrSelf || SelfAndAncestors(mimeType).Any(type => type.Name == ancestorOrSelf);

    private IEnumerable<MimeTypeEntry> SelfAndAncestors(string mimeType)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var name = mimeType; name is not null && visited.Add(name) && _types.TryGetValue(name, out var type);
             name = type.Parent)
        {
            yield return type;
        }
    }
}
