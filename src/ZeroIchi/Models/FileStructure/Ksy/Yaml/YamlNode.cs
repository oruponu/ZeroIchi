using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure.Ksy.Yaml;

public readonly record struct YamlPosition(int Line, int Column);

public abstract class YamlNode(YamlPosition start)
{
    public YamlPosition Start { get; } = start;
}

public sealed class YamlScalar(string text, YamlScalarStyle style, YamlPosition start) : YamlNode(start)
{
    public string Text { get; } = text;
    public YamlScalarStyle Style { get; } = style;
    public YamlScalarValue Value { get; } = YamlScalarValue.Resolve(text, style);
}

public sealed class YamlSequence(IReadOnlyList<YamlNode> items, YamlPosition start) : YamlNode(start)
{
    public IReadOnlyList<YamlNode> Items { get; } = items;
}

public sealed class YamlMapping(IReadOnlyList<KeyValuePair<YamlScalar, YamlNode>> entries, YamlPosition start)
    : YamlNode(start)
{
    public IReadOnlyList<KeyValuePair<YamlScalar, YamlNode>> Entries { get; } = entries;

    public YamlNode? Get(string key)
    {
        foreach (var (k, v) in Entries)
        {
            if (k.Value.Kind == YamlScalarKind.String && k.Text == key) return v;
        }
        return null;
    }
}

public sealed record YamlError(string Message, YamlPosition Position);

public sealed record YamlLoadResult(YamlNode? Root, IReadOnlyList<YamlError> Errors);
