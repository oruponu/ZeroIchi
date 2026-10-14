using System;
using System.Collections.Generic;
using System.Globalization;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed partial class KsyRun(KsyFormat format, ByteBuffer buffer, KsyParseOptions options)
{
    private readonly Dictionary<KsyType, KsyTypeLayout> _layouts = [];
    private readonly List<KsObject> _objects = [];
    private int _nodeCount;
    private long _loaded;
    private Exception? _observed;
    private long? _failOffset;
    private bool _frozen;

    public FileStructureNode Run()
    {
        var stream = KsStream.FromBuffer(buffer);
        var root = new KsyNode(format.Id, null, stream, 0);
        Exception? failure = null;
        try
        {
            Guard(root, () => ReadObject(format.Root, null, null, stream, [], root));
            EvaluateForDisplay();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            failure = e;
        }

        _frozen = true;
        var children = BuildChildren(root);
        if (failure is not null) children.Add(Unparsed(failure));
        return new FileStructureNode
        {
            Name = format.Id,
            Offset = 0,
            Length = buffer.Length,
            Children = children,
            Error = root.Error,
            IsIncomplete = root.IsIncomplete,
        };
    }

    public KsyTypeLayout Layout(KsyType type)
    {
        if (!_layouts.TryGetValue(type, out var layout)) _layouts[type] = layout = new KsyTypeLayout(type);
        return layout;
    }

    private KsyNode NewNode(string name, string? fieldId, KsStream? stream, long start)
    {
        if (_nodeCount >= options.NodeBudget)
            throw new KsyParseException("Node budget exceeded") { IsBudget = true, IsLimit = true };
        _nodeCount++;
        return new KsyNode(name, fieldId, stream, start);
    }

    private void ChargeLoad(long bytes)
    {
        _loaded += bytes;
        if (_loaded > options.MaxTotalLoad)
        {
            throw new KsyParseException($"Data loaded into memory exceeds the limit ({options.MaxTotalLoad} bytes)")
            {
                IsLimit = true,
            };
        }
    }

    private T Guard<T>(KsyNode node, Func<T> read)
    {
        try
        {
            var result = read();
            node.End = node.Stream?.Position ?? node.Start;
            return result;
        }
        catch (Exception e) when (Observe(node, e))
        {
            throw;
        }
        finally
        {
            if (node.End is null)
            {
                node.End = node.Stream?.Position ?? node.Start;
                node.IsIncomplete = true;
            }
        }
    }

    private bool Observe(KsyNode node, Exception e)
    {
        if (!ReferenceEquals(_observed, e))
        {
            _observed = e;
            _failOffset = null;
            if (e is not KsyParseException { IsBudget: true }) node.Error = Message(e);
        }
        if (_failOffset is null && node.Stream is { IsFileBacked: true } stream)
            _failOffset = stream.FileOffset(stream.Position);
        return false;
    }

    private static bool IsLimit(Exception e) =>
        e is KsyParseException { IsLimit: true } or InsufficientExecutionStackException;

    private static string Message(Exception e) => e switch
    {
        KsyParseException or KsExpressionException => e.Message,
        InsufficientExecutionStackException => "Nesting is too deep for the available stack",
        _ => $"Internal error: {e.Message}",
    };

    private static KsyParseException ExpressionError(string path, string key, KsExpressionException e) =>
        new($"{path}/{key}: {e.Message}");

    private static KsValue Evaluate(KsExpr expr, IKsEvalScope scope, string path, string key)
    {
        try
        {
            return KsEvaluator.Evaluate(expr, scope);
        }
        catch (KsExpressionException e)
        {
            throw ExpressionError(path, key, e);
        }
    }

    private static bool EvaluateBool(KsExpr expr, IKsEvalScope scope, string path, string key)
    {
        try
        {
            return KsEvaluator.EvaluateBool(expr, scope);
        }
        catch (KsExpressionException e)
        {
            throw ExpressionError(path, key, e);
        }
    }

    private static long EvaluateLong(KsExpr expr, IKsEvalScope scope, string path, string key)
    {
        try
        {
            return KsEvaluator.EvaluateNonNegativeLong(expr, scope);
        }
        catch (KsExpressionException e)
        {
            throw ExpressionError(path, key, e);
        }
    }

    private static bool Equal(KsExpr node, KsValue left, KsValue right, string path, string key)
    {
        try
        {
            return KsEvaluator.ValuesEqual(node, left, right);
        }
        catch (KsExpressionException e)
        {
            throw ExpressionError(path, key, e);
        }
    }

    private static int Compare(KsExpr node, KsValue left, KsValue right, string path, string key)
    {
        try
        {
            return KsEvaluator.CompareValues(node, left, right);
        }
        catch (KsExpressionException e)
        {
            throw ExpressionError(path, key, e);
        }
    }

    private FileStructureNode Unparsed(Exception failure)
    {
        var offset = ReferenceEquals(_observed, failure) && _failOffset is { } observed
            ? Math.Clamp(observed, 0, buffer.Length)
            : buffer.Length;
        return new FileStructureNode
        {
            Name = "未解析",
            Offset = offset,
            Length = buffer.Length - offset,
            Description = failure is KsyParseException { IsBudget: true }
                ? string.Create(CultureInfo.InvariantCulture, $"ノード数の上限 ({options.NodeBudget:N0}) に到達")
                : Message(failure),
        };
    }

    private List<FileStructureNode> BuildChildren(KsyNode top)
    {
        var order = new List<KsyNode>();
        var pending = new Stack<KsyNode>();
        pending.Push(top);
        while (pending.TryPop(out var node))
        {
            order.Add(node);
            foreach (var child in node.Children) pending.Push(child);
            foreach (var instance in node.Instances)
            {
                if (instance is not null) pending.Push(instance);
            }
        }

        for (var i = order.Count - 1; i > 0; i--) order[i].Built = Build(order[i]);
        return Collect(top);
    }

    private static List<FileStructureNode> Collect(KsyNode node)
    {
        var children = new List<FileStructureNode>(node.Children.Count + node.Instances.Length);
        foreach (var child in node.Children) children.Add(child.Built!);
        foreach (var instance in node.Instances)
        {
            if (instance is not null) children.Add(instance.Built!);
        }
        return children;
    }

    private static (string Text, ValueKind Kind) Describe(KsyNode node)
    {
        if (node.IsContainer) return ("", ValueKind.None);
        return node.Value is { Kind: KsValueKind.Struct, AsStruct: KsObject obj }
            ? (KsySummary.Describe(obj), ValueKind.None)
            : KsyDescriptions.Describe(node.Value);
    }

    private FileStructureNode Build(KsyNode node)
    {
        var hasRange = node.Stream is { IsFileBacked: true };
        var (text, kind) = node.FixedDescription ?? Describe(node);
        if (node.IsIncomplete) text = text.Length == 0 ? "(未完了)" : $"{text} (未完了)";

        return new FileStructureNode
        {
            Name = node.Name,
            FieldId = node.FieldId,
            Offset = hasRange ? node.Stream!.FileOffset(node.Start) : 0,
            Length = hasRange ? (node.End ?? node.Start) - node.Start : 0,
            Description = text,
            ValueKind = hasRange ? kind : ValueKind.None,
            Children = Collect(node),
            HasRange = hasRange,
            Error = node.Error,
            IsIncomplete = node.IsIncomplete,
        };
    }
}
