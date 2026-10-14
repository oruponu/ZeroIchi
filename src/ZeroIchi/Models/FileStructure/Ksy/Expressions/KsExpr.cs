using System;
using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public abstract record KsExpr(int Position, int Length);

public sealed record KsIntLiteral(Int128 Value, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsFloatLiteral(double Value, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsStringLiteral(string Value, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsBoolLiteral(bool Value, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsFString(IReadOnlyList<KsExpr> Parts, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsArrayLiteral(IReadOnlyList<KsExpr> Elements, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsName(string Name, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsEnumRef(IReadOnlyList<string> EnumPath, string ValueName, int Position, int Length)
    : KsExpr(Position, Length);

public sealed record KsSizeOf(KsTypeRef Type, bool InBits, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsUnary(KsUnaryOp Op, KsExpr Operand, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsBinary(KsBinaryOp Op, KsExpr Left, KsExpr Right, int Position, int Length)
    : KsExpr(Position, Length);

public sealed record KsTernary(KsExpr Condition, KsExpr Then, KsExpr Else, int Position, int Length)
    : KsExpr(Position, Length);

public sealed record KsMember(KsExpr Target, string Name, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsCall(KsExpr Target, string Name, IReadOnlyList<KsExpr> Arguments, int Position, int Length)
    : KsExpr(Position, Length);

public sealed record KsIndex(KsExpr Target, KsExpr Index, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsCast(KsExpr Target, KsTypeRef Type, int Position, int Length) : KsExpr(Position, Length);

public sealed record KsTypeRef(IReadOnlyList<string> Path)
{
    public string FullName => string.Join("::", Path);
}

public enum KsUnaryOp
{
    Plus,
    Minus,
    BitNot,
    Not,
}

public enum KsBinaryOp
{
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    Shl,
    Shr,
    BitAnd,
    BitOr,
    BitXor,
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
    And,
    Or,
}
