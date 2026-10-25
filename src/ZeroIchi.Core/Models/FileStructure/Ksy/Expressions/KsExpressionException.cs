using System;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public sealed class KsExpressionException(string message, int position, int length) : Exception(message)
{
    public int Position { get; } = position;
    public int Length { get; } = length;
}
