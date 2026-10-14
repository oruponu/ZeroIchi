using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace ZeroIchi.Models.FileStructure.Ksy.Yaml;

public static class YamlLoader
{
    public const int MaxDepth = 128;

    public static YamlLoadResult Load(string text)
    {
        var errors = new List<YamlError>();
        var position = new YamlPosition(1, 1);
        try
        {
            var root = Build(new Parser(new StringReader(text)), errors, ref position);
            if (root is null) errors.Add(new YamlError("Document is empty", new YamlPosition(1, 1)));
            return new YamlLoadResult(root, errors);
        }
        catch (FatalYamlError e)
        {
            errors.Add(e.Error);
        }
        catch (YamlException e)
        {
            errors.Add(new YamlError(e.Message, ToPosition(e.Start)));
        }
        catch (InvalidOperationException e)
        {
            errors.Add(new YamlError($"Invalid YAML ({e.Message})", position));
        }
        return new YamlLoadResult(null, errors);
    }

    private sealed class FatalYamlError(YamlError error) : Exception(error.Message)
    {
        public YamlError Error { get; } = error;
    }

    private sealed class Frame(bool isMapping, YamlPosition start)
    {
        public bool IsMapping { get; } = isMapping;
        public YamlPosition Start { get; } = start;
        public List<YamlNode> Items { get; } = [];
        public List<KeyValuePair<YamlScalar, YamlNode>> Entries { get; } = [];
        public HashSet<(YamlScalarKind, string)> Keys { get; } = [];
        public YamlScalar? PendingKey { get; set; }
        public bool SkipValue { get; set; }
    }

    private static YamlNode? Build(Parser parser, List<YamlError> errors, ref YamlPosition position)
    {
        var stack = new Stack<Frame>();
        YamlNode? root = null;
        var documents = 0;

        void Add(YamlNode node)
        {
            if (stack.Count == 0)
            {
                root = node;
                return;
            }

            var frame = stack.Peek();
            if (!frame.IsMapping)
            {
                frame.Items.Add(node);
                return;
            }

            if (frame.PendingKey is not { } key)
            {
                if (node is not YamlScalar scalar)
                    throw new FatalYamlError(new YamlError("Mapping keys must be scalars", node.Start));

                frame.PendingKey = scalar;
                frame.SkipValue = !frame.Keys.Add(KeyIdentity(scalar.Value));
                if (frame.SkipValue)
                    errors.Add(new YamlError($"Duplicate key '{scalar.Text}'", scalar.Start));
                return;
            }

            if (!frame.SkipValue) frame.Entries.Add(new KeyValuePair<YamlScalar, YamlNode>(key, node));
            frame.PendingKey = null;
        }

        while (parser.MoveNext())
        {
            var current = parser.Current!;
            var start = ToPosition(current.Start);
            position = start;
            switch (current)
            {
                case DocumentStart:
                    if (++documents > 1)
                        throw new FatalYamlError(new YamlError("Only one YAML document is allowed", start));
                    break;
                case AnchorAlias:
                    throw new FatalYamlError(new YamlError("Aliases are not supported", start));
                case NodeEvent { Tag.IsEmpty: false, Tag.IsNonSpecific: false }:
                    throw new FatalYamlError(new YamlError("Tags are not supported", start));
                case Scalar scalar:
                    Add(new YamlScalar(scalar.Value, ToStyle(scalar.Style), start));
                    break;
                case MappingStart or SequenceStart:
                    if (stack.Count >= MaxDepth)
                        throw new FatalYamlError(new YamlError($"Nesting is deeper than {MaxDepth} levels", start));
                    stack.Push(new Frame(current is MappingStart, start));
                    break;
                case MappingEnd or SequenceEnd:
                {
                    var frame = stack.Pop();
                    Add(frame.IsMapping
                        ? new YamlMapping(frame.Entries, frame.Start)
                        : new YamlSequence(frame.Items, frame.Start));
                    break;
                }
            }
        }

        return root;
    }

    private static (YamlScalarKind, string) KeyIdentity(YamlScalarValue value) =>
        (value.Kind, value.Kind == YamlScalarKind.Null ? "" : value.ToExpressionSource());

    private static YamlScalarStyle ToStyle(ScalarStyle style) => style switch
    {
        ScalarStyle.SingleQuoted => YamlScalarStyle.SingleQuoted,
        ScalarStyle.DoubleQuoted => YamlScalarStyle.DoubleQuoted,
        ScalarStyle.Literal => YamlScalarStyle.Literal,
        ScalarStyle.Folded => YamlScalarStyle.Folded,
        _ => YamlScalarStyle.Plain,
    };

    private static YamlPosition ToPosition(Mark mark) => new((int)mark.Line, (int)mark.Column);
}
