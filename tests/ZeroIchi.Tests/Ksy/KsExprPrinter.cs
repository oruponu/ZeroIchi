using System.Globalization;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Tests.Ksy;

internal static class KsExprPrinter
{
    public static string Print(KsExpr e) => e switch
    {
        KsIntLiteral x => x.Value.ToString(CultureInfo.InvariantCulture),
        KsFloatLiteral x => x.Value.ToString("R", CultureInfo.InvariantCulture),
        KsStringLiteral x => $"\"{x.Value}\"",
        KsBoolLiteral x => x.Value ? "true" : "false",
        KsName x => x.Name,
        KsEnumRef x => $"{string.Join("::", x.EnumPath)}::{x.ValueName}",
        KsSizeOf x => $"({(x.InBits ? "bitsizeof" : "sizeof")} {x.Type.FullName})",
        KsUnary x => $"({UnaryText(x.Op)} {Print(x.Operand)})",
        KsBinary x => $"({BinaryText(x.Op)} {Print(x.Left)} {Print(x.Right)})",
        KsTernary x => $"(? {Print(x.Condition)} {Print(x.Then)} {Print(x.Else)})",
        KsMember x => $"(. {Print(x.Target)} {x.Name})",
        KsCall x => $"(call {Print(x.Target)} {x.Name}{string.Concat(x.Arguments.Select(a => " " + Print(a)))})",
        KsIndex x => $"([] {Print(x.Target)} {Print(x.Index)})",
        KsCast x => $"(as {Print(x.Target)} {x.Type.FullName})",
        KsArrayLiteral x => $"[{string.Join(" ", x.Elements.Select(Print))}]",
        KsFString x => $"(f {string.Join(" ", x.Parts.Select(Print))})",
        _ => throw new ArgumentException(e.GetType().Name),
    };

    private static string UnaryText(KsUnaryOp op) => op switch
    {
        KsUnaryOp.Plus => "+",
        KsUnaryOp.Minus => "-",
        KsUnaryOp.BitNot => "~",
        KsUnaryOp.Not => "not",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    private static string BinaryText(KsBinaryOp op) => op switch
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
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}
