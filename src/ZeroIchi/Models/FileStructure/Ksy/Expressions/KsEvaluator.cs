using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public static class KsEvaluator
{
    public static KsValue Evaluate(KsExpr expr, IKsEvalScope scope)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw Error(expr, "Expression is nested too deeply");

        try
        {
            return EvaluateCore(expr, scope);
        }
        catch (KsExpressionException e) when (e.Position < 0)
        {
            throw new KsExpressionException(e.Message, expr.Position, expr.Length);
        }
    }

    public static bool EvaluateBool(KsExpr expr, IKsEvalScope scope) => ExpectBool(expr, Evaluate(expr, scope));

    public static long EvaluateNonNegativeLong(KsExpr expr, IKsEvalScope scope)
    {
        var value = Evaluate(expr, scope);
        if (value.Kind != KsValueKind.Int)
            throw Error(expr, $"Integer expected (got {KindName(value)})");

        var number = value.AsInt;
        if (number < 0 || number > long.MaxValue)
            throw Error(expr, $"{number} cannot be used as a position or size");
        return (long)number;
    }

    internal static KsExpressionException Error(KsExpr node, string message) => new(message, node.Position, node.Length);

    internal static double ToDouble(KsValue value) =>
        value.Kind == KsValueKind.Int ? (double)value.AsInt : value.AsFloat;

    private static bool IsNumber(KsValue value) => value.Kind is KsValueKind.Int or KsValueKind.Float;

    private static string KindName(KsValue value) => KsValue.KindName(value.Kind);

    private static KsValue EvaluateCore(KsExpr expr, IKsEvalScope scope) => expr switch
    {
        KsIntLiteral e => KsValue.FromInt(e.Value),
        KsFloatLiteral e => KsValue.FromFloat(e.Value),
        KsStringLiteral e => KsValue.FromString(e.Value),
        KsBoolLiteral e => KsValue.FromBool(e.Value),
        KsFString e => EvaluateFString(e, scope),
        KsArrayLiteral e => EvaluateArray(e, scope),
        KsName e => scope.TryResolveName(e.Name, out var value)
            ? value
            : throw Error(e, $"Name '{e.Name}' not found"),
        KsEnumRef e => EvaluateEnumRef(e, scope),
        KsSizeOf e => EvaluateSizeOf(e, scope),
        KsUnary e => EvaluateUnary(e, scope),
        KsBinary e => EvaluateBinary(e, scope),
        KsTernary e => Evaluate(ExpectBool(e.Condition, Evaluate(e.Condition, scope)) ? e.Then : e.Else, scope),
        KsMember e => EvaluateMember(e, scope),
        KsCall e => KsMethods.Call(e, Evaluate(e.Target, scope), e.Name, EvaluateArguments(e.Arguments, scope)),
        KsIndex e => EvaluateIndex(e, scope),
        KsCast e => EvaluateCast(e, scope),
        _ => throw Error(expr, "Unsupported expression"),
    };

    private static KsValue[] EvaluateArguments(IReadOnlyList<KsExpr> arguments, IKsEvalScope scope)
    {
        var values = new KsValue[arguments.Count];
        for (var i = 0; i < values.Length; i++)
            values[i] = Evaluate(arguments[i], scope);
        return values;
    }

    private static bool ExpectBool(KsExpr node, KsValue value) => value.Kind switch
    {
        KsValueKind.Bool => value.AsBool,
        KsValueKind.Null => throw Error(node, "Value is null"),
        _ => throw Error(node, $"Boolean expected (got {KindName(value)})"),
    };

    private static KsValue EvaluateFString(KsFString e, IKsEvalScope scope)
    {
        var sb = new StringBuilder();
        foreach (var part in e.Parts)
        {
            var value = Evaluate(part, scope);
            sb.Append(value.Kind switch
            {
                KsValueKind.String => value.AsString,
                KsValueKind.Int => value.AsInt.ToString(CultureInfo.InvariantCulture),
                _ => throw Error(part, $"Only integers and strings can be embedded in f-strings (got {KindName(value)})"),
            });
        }
        return KsValue.FromString(sb.ToString());
    }

    private static KsValue EvaluateArray(KsArrayLiteral e, IKsEvalScope scope)
    {
        var values = EvaluateArguments(e.Elements, scope);
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i].Kind != values[0].Kind)
                throw Error(e.Elements[i], "Array elements have mixed types");
        }

        var isBytes = true;
        foreach (var value in values)
        {
            if (value.Kind != KsValueKind.Int || value.AsInt < 0 || value.AsInt > 255)
            {
                isBytes = false;
                break;
            }
        }

        if (!isBytes) return KsValue.FromArray(values);

        var bytes = new byte[values.Length];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)values[i].AsInt;
        return KsValue.FromBytes(bytes);
    }

    private static KsValue EvaluateEnumRef(KsEnumRef e, IKsEvalScope scope)
    {
        var type = scope.ResolveEnum(e.EnumPath)
            ?? throw Error(e, $"Enum '{string.Join("::", e.EnumPath)}' not found");
        if (!type.TryGetValue(e.ValueName, out var value))
            throw Error(e, $"Enum '{type.Name}' has no value '{e.ValueName}'");
        return KsValue.FromEnum(type, value);
    }

    private static KsValue EvaluateSizeOf(KsSizeOf e, IKsEvalScope scope)
    {
        var bits = KsPrimitiveTypes.SizeInBits(e.Type);
        if (bits is null)
        {
            var type = scope.ResolveType(e.Type.Path)
                ?? throw Error(e, $"Type '{e.Type.FullName}' not found");
            bits = type.SizeInBits ?? throw Error(e, $"Type '{e.Type.FullName}' is not fixed-size");
        }

        if (e.InBits) return KsValue.FromInt(bits.Value);
        if (bits.Value % 8 != 0)
            throw Error(e, $"Size of type '{e.Type.FullName}' is not a whole number of bytes ({bits} bits)");
        return KsValue.FromInt(bits.Value / 8);
    }

    private static KsValue EvaluateUnary(KsUnary e, IKsEvalScope scope)
    {
        var value = Evaluate(e.Operand, scope);
        if (value.Kind == KsValueKind.Null)
            throw Error(e.Operand, "Value is null");

        try
        {
            return (e.Op, value.Kind) switch
            {
                (KsUnaryOp.Not, _) => KsValue.FromBool(!ExpectBool(e.Operand, value)),
                (KsUnaryOp.Plus, KsValueKind.Int or KsValueKind.Float) => value,
                (KsUnaryOp.Minus, KsValueKind.Int) => KsValue.FromInt(checked(-value.AsInt)),
                (KsUnaryOp.Minus, KsValueKind.Float) => KsValue.FromFloat(-value.AsFloat),
                (KsUnaryOp.BitNot, KsValueKind.Int) => KsValue.FromInt(~value.AsInt),
                _ => throw Error(e, $"Unary operator cannot be applied to {KindName(value)}"),
            };
        }
        catch (OverflowException)
        {
            throw Error(e, "Integer overflow");
        }
    }

    private static KsValue EvaluateBinary(KsBinary e, IKsEvalScope scope)
    {
        if (e.Op is KsBinaryOp.And or KsBinaryOp.Or)
        {
            var left = ExpectBool(e.Left, Evaluate(e.Left, scope));
            if (e.Op == KsBinaryOp.And ? !left : left) return KsValue.FromBool(left);
            return KsValue.FromBool(ExpectBool(e.Right, Evaluate(e.Right, scope)));
        }

        var l = Evaluate(e.Left, scope);
        var r = Evaluate(e.Right, scope);
        if (l.Kind == KsValueKind.Null) throw Error(e.Left, "Value is null");
        if (r.Kind == KsValueKind.Null) throw Error(e.Right, "Value is null");

        try
        {
            return e.Op switch
            {
                KsBinaryOp.Add when l.Kind == KsValueKind.String && r.Kind == KsValueKind.String =>
                    KsValue.FromString(l.AsString + r.AsString),
                KsBinaryOp.Add or KsBinaryOp.Sub or KsBinaryOp.Mul or KsBinaryOp.Div or KsBinaryOp.Mod =>
                    Arithmetic(e, l, r),
                KsBinaryOp.Shl or KsBinaryOp.Shr or KsBinaryOp.BitAnd or KsBinaryOp.BitOr or KsBinaryOp.BitXor =>
                    IntegerOnly(e, l, r),
                KsBinaryOp.Eq => KsValue.FromBool(AreEqual(e, l, r)),
                KsBinaryOp.Ne => KsValue.FromBool(!AreEqual(e, l, r)),
                KsBinaryOp.Lt => KsValue.FromBool(CompareOrdered(e, l, r) < 0),
                KsBinaryOp.Le => KsValue.FromBool(CompareOrdered(e, l, r) <= 0),
                KsBinaryOp.Gt => KsValue.FromBool(CompareOrdered(e, l, r) > 0),
                KsBinaryOp.Ge => KsValue.FromBool(CompareOrdered(e, l, r) >= 0),
                _ => throw Error(e, "Unsupported operator"),
            };
        }
        catch (OverflowException)
        {
            throw Error(e, "Integer overflow");
        }
    }

    private static KsExpressionException OperandsError(KsBinary e, KsValue l, KsValue r) =>
        Error(e, $"Operator {OperatorText(e.Op)} cannot be applied to {KindName(l)} and {KindName(r)}");

    private static KsValue Arithmetic(KsBinary e, KsValue l, KsValue r)
    {
        if (l.Kind == KsValueKind.Int && r.Kind == KsValueKind.Int)
        {
            var a = l.AsInt;
            var b = r.AsInt;
            if (e.Op is KsBinaryOp.Div or KsBinaryOp.Mod && b == 0)
                throw Error(e, "Division by zero");

            return KsValue.FromInt(e.Op switch
            {
                KsBinaryOp.Add => checked(a + b),
                KsBinaryOp.Sub => checked(a - b),
                KsBinaryOp.Mul => checked(a * b),
                KsBinaryOp.Div => KsIntMath.FloorDiv(a, b),
                _ => KsIntMath.FloorMod(a, b),
            });
        }

        if (!IsNumber(l) || !IsNumber(r)) throw OperandsError(e, l, r);

        var x = ToDouble(l);
        var y = ToDouble(r);
        if (e.Op is KsBinaryOp.Div or KsBinaryOp.Mod && y == 0)
            throw Error(e, "Division by zero");

        return KsValue.FromFloat(e.Op switch
        {
            KsBinaryOp.Add => x + y,
            KsBinaryOp.Sub => x - y,
            KsBinaryOp.Mul => x * y,
            KsBinaryOp.Div => x / y,
            _ => x - y * Math.Floor(x / y),
        });
    }

    private static KsValue IntegerOnly(KsBinary e, KsValue l, KsValue r)
    {
        if (l.Kind != KsValueKind.Int || r.Kind != KsValueKind.Int) throw OperandsError(e, l, r);

        var a = l.AsInt;
        var b = r.AsInt;
        if (e.Op is KsBinaryOp.Shl or KsBinaryOp.Shr && b < 0)
            throw Error(e, "Negative shift count");

        return KsValue.FromInt(e.Op switch
        {
            KsBinaryOp.Shl => KsIntMath.ShiftLeft(a, b),
            KsBinaryOp.Shr => KsIntMath.ShiftRight(a, b),
            KsBinaryOp.BitAnd => a & b,
            KsBinaryOp.BitOr => a | b,
            _ => a ^ b,
        });
    }

    private static int CompareNumbers(KsValue l, KsValue r) =>
        l.Kind == KsValueKind.Int && r.Kind == KsValueKind.Int
            ? l.AsInt.CompareTo(r.AsInt)
            : ToDouble(l).CompareTo(ToDouble(r));

    private static bool AreEqual(KsBinary e, KsValue l, KsValue r)
    {
        if (IsNumber(l) && IsNumber(r)) return CompareNumbers(l, r) == 0;
        if (l.Kind != r.Kind) throw OperandsError(e, l, r);

        return l.Kind switch
        {
            KsValueKind.String => string.Equals(l.AsString, r.AsString, StringComparison.Ordinal),
            KsValueKind.Bool => l.AsBool == r.AsBool,
            KsValueKind.Bytes => l.AsBytes.ToArray().AsSpan().SequenceEqual(r.AsBytes.ToArray()),
            KsValueKind.Enum => ReferenceEquals(l.EnumType, r.EnumType)
                ? l.EnumValue == r.EnumValue
                : throw Error(e, $"Cannot compare different enums ({l.EnumType.Name} and {r.EnumType.Name})"),
            _ => throw OperandsError(e, l, r),
        };
    }

    private static int CompareOrdered(KsBinary e, KsValue l, KsValue r)
    {
        if (IsNumber(l) && IsNumber(r)) return CompareNumbers(l, r);
        if (l.Kind == KsValueKind.String && r.Kind == KsValueKind.String)
            return Math.Sign(string.CompareOrdinal(l.AsString, r.AsString));
        if (l.Kind == KsValueKind.Bytes && r.Kind == KsValueKind.Bytes)
            return Math.Sign(l.AsBytes.ToArray().AsSpan().SequenceCompareTo(r.AsBytes.ToArray()));
        throw OperandsError(e, l, r);
    }

    private static KsValue EvaluateMember(KsMember e, IKsEvalScope scope)
    {
        var target = Evaluate(e.Target, scope);
        if (target.Kind == KsValueKind.Null)
            throw Error(e, "Value is null");

        if (target.Kind != KsValueKind.Struct)
            return KsMethods.Call(e, target, e.Name, []);

        var obj = target.AsStruct;
        switch (e.Name)
        {
            case "_root":
                return KsValue.FromStruct(obj.Root);
            case "_parent":
                return obj.Parent is { } parent ? KsValue.FromStruct(parent) : KsValue.Null;
            case "_io":
                return KsValue.FromStream(obj.Io);
            case "_sizeof":
                return obj.StaticSize is { } size
                    ? KsValue.FromInt(size)
                    : throw Error(e, $"Type '{obj.TypeName}' is not fixed-size");
        }

        if (obj.TryGetMember(e.Name, out var value)) return value;
        throw Error(e, $"'{obj.TypeName}' has no member '{e.Name}'");
    }

    private static KsValue EvaluateIndex(KsIndex e, IKsEvalScope scope)
    {
        var target = Evaluate(e.Target, scope);
        var index = Evaluate(e.Index, scope);
        if (target.Kind == KsValueKind.Null) throw Error(e.Target, "Value is null");
        if (index.Kind != KsValueKind.Int) throw Error(e.Index, $"Index must be an integer (got {KindName(index)})");

        var i = index.AsInt;
        switch (target.Kind)
        {
            case KsValueKind.Array:
            {
                var items = target.AsArray;
                if (i < 0 || i >= items.Count)
                    throw Error(e, $"Index {i} is out of range (count {items.Count})");
                return items[(int)i];
            }
            case KsValueKind.Bytes:
            {
                var bytes = target.AsBytes.ToArray();
                if (i < 0 || i >= bytes.Length)
                    throw Error(e, $"Index {i} is out of range (length {bytes.Length})");
                return KsValue.FromInt(bytes[(int)i]);
            }
            default:
                throw Error(e, $"Indexing is not supported for {KindName(target)}");
        }
    }

    private static KsValue EvaluateCast(KsCast e, IKsEvalScope scope)
    {
        var value = Evaluate(e.Target, scope);
        if (value.Kind == KsValueKind.Null) throw Error(e.Target, "Value is null");

        if (KsPrimitiveTypes.KindOf(e.Type) is { } kind)
        {
            return value.Kind == kind
                ? value
                : throw Error(e, $"Cannot treat {KindName(value)} as {e.Type.FullName}");
        }

        var type = scope.ResolveType(e.Type.Path)
            ?? throw Error(e, $"Type '{e.Type.FullName}' not found");
        return type.IsInstance(value)
            ? value
            : throw Error(e, $"Cannot treat {KindName(value)} as {e.Type.FullName}");
    }

    private static string OperatorText(KsBinaryOp op) => op switch
    {
        KsBinaryOp.Add => "+",
        KsBinaryOp.Sub => "-",
        KsBinaryOp.Mul => "*",
        KsBinaryOp.Div => "/",
        KsBinaryOp.Mod => "%",
        KsBinaryOp.Shl => "<<",
        KsBinaryOp.Shr => ">>",
        KsBinaryOp.BitAnd => "&",
        KsBinaryOp.BitOr => "|",
        KsBinaryOp.BitXor => "^",
        KsBinaryOp.Eq => "==",
        KsBinaryOp.Ne => "!=",
        KsBinaryOp.Lt => "<",
        KsBinaryOp.Le => "<=",
        KsBinaryOp.Gt => ">",
        KsBinaryOp.Ge => ">=",
        KsBinaryOp.And => "and",
        KsBinaryOp.Or => "or",
        _ => op.ToString(),
    };
}
