using System;
using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

internal sealed class KsyStaticSize
{
    private readonly HashSet<KsyType> _visiting = [];
    private readonly Dictionary<KsyType, long?> _computed = [];

    public long? Compute(KsyType type)
    {
        if (_computed.TryGetValue(type, out var known)) return known;
        if (!_visiting.Add(type)) return null;

        long? result;
        try
        {
            result = SeqBits(type);
        }
        finally
        {
            _visiting.Remove(type);
        }

        _computed[type] = result;
        return result;
    }

    private long? SeqBits(KsyType type)
    {
        try
        {
            long bits = 0;
            foreach (var attribute in type.Seq)
            {
                var count = attribute.Repeat switch
                {
                    KsyRepeatKind.None => 1,
                    KsyRepeatKind.Expr => Constant(attribute.RepeatExpr),
                    _ => null,
                };
                if (count is null) return null;

                if (attribute.DataType is KsyBitType bit)
                {
                    bits = checked(bits + bit.Width * count.Value);
                    continue;
                }

                var element = ElementBits(attribute);
                if (element is null) return null;
                bits = checked((bits + 7) / 8 * 8 + element.Value * count.Value);
            }
            return bits;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private long? ElementBits(KsyAttribute attribute)
    {
        if (attribute.Contents is { } contents) return contents.Length * 8L;
        if (attribute.Size is { } size) return Constant(size) is { } bytes ? checked(bytes * 8) : null;
        if (attribute.SizeEos || attribute.Terminator is not null) return null;

        return attribute.DataType switch
        {
            KsyIntType i => i.Size * 8L,
            KsyFloatType f => f.Size * 8L,
            KsyUserType user => Compute(user.Type),
            _ => null,
        };
    }

    private static long? Constant(KsExpr? expr)
    {
        if (expr is null) return null;
        try
        {
            return KsEvaluator.EvaluateNonNegativeLong(expr, EmptyScope.Instance);
        }
        catch (KsExpressionException)
        {
            return null;
        }
    }

    private sealed class EmptyScope : IKsEvalScope
    {
        public static EmptyScope Instance { get; } = new();

        public bool TryResolveName(string name, out KsValue value)
        {
            value = KsValue.Null;
            return false;
        }

        public IKsEnumType? ResolveEnum(IReadOnlyList<string> path) => null;

        public IKsTypeInfo? ResolveType(IReadOnlyList<string> path) => null;
    }
}
