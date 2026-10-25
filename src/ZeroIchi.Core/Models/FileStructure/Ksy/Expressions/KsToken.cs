using System;
using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public sealed record KsFStringPart(bool IsExpression, string Text, int Position);

public sealed record KsToken(KsTokenKind Kind, int Position, int Length)
{
    public Int128 IntValue { get; init; }
    public double FloatValue { get; init; }
    public string Text { get; init; } = "";
    public IReadOnlyList<KsFStringPart> FStringParts { get; init; } = [];
}
