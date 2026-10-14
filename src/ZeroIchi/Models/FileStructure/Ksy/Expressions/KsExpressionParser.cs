using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZeroIchi.Models.FileStructure.Ksy.Expressions;

public sealed class KsExpressionParser
{
    public const int MaxDepth = 128;
    public const int MaxAstDepth = 256;

    private readonly List<KsToken> _tokens;
    private int _index;
    private int _depth;

    private KsExpressionParser(List<KsToken> tokens, int depth)
    {
        _tokens = tokens;
        _depth = depth;
    }

    public static KsExpr Parse(string source)
    {
        var expr = Parse(source, 0, 0);
        CheckAstDepth(expr);
        return expr;
    }

    private static KsExpr Parse(string source, int offset, int depth)
    {
        var parser = new KsExpressionParser(KsLexer.Tokenize(source, offset), depth);
        var expr = parser.ParseTernary();
        if (parser.Current.Kind != KsTokenKind.End)
            throw parser.Error("Unexpected token after expression", parser.Current);
        return expr;
    }

    private static void CheckAstDepth(KsExpr root)
    {
        var pending = new Stack<(KsExpr Node, int Depth)>();
        pending.Push((root, 1));
        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();
            if (depth > MaxAstDepth)
                throw new KsExpressionException("Expression is nested too deeply", node.Position, Math.Max(node.Length, 1));

            foreach (var child in Children(node))
                pending.Push((child, depth + 1));
        }
    }

    private static IEnumerable<KsExpr> Children(KsExpr node)
    {
        switch (node)
        {
            case KsFString e:
                foreach (var part in e.Parts) yield return part;
                break;
            case KsArrayLiteral e:
                foreach (var element in e.Elements) yield return element;
                break;
            case KsUnary e:
                yield return e.Operand;
                break;
            case KsBinary e:
                yield return e.Left;
                yield return e.Right;
                break;
            case KsTernary e:
                yield return e.Condition;
                yield return e.Then;
                yield return e.Else;
                break;
            case KsMember e:
                yield return e.Target;
                break;
            case KsCall e:
                yield return e.Target;
                foreach (var argument in e.Arguments) yield return argument;
                break;
            case KsIndex e:
                yield return e.Target;
                yield return e.Index;
                break;
            case KsCast e:
                yield return e.Target;
                break;
        }
    }

    private KsToken Current => _tokens[_index];

    private KsToken Advance()
    {
        var token = _tokens[_index];
        if (token.Kind != KsTokenKind.End) _index++;
        return token;
    }

    private bool IsKeyword(string word) => Current.Kind == KsTokenKind.Identifier && Current.Text == word;

    private KsToken Expect(KsTokenKind kind, string what)
    {
        if (Current.Kind == kind) return Advance();
        throw Current.Kind == KsTokenKind.End
            ? Error($"Unexpected end of expression ({what} expected)", Current)
            : Error($"{what} expected", Current);
    }

    private KsExpressionException Error(string message, KsToken token) =>
        new(message, token.Position, Math.Max(token.Length, 1));

    private void EnterDepth()
    {
        if (++_depth > MaxDepth || !RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw Error("Expression is nested too deeply", Current);
    }

    private static int End(KsExpr e) => e.Position + e.Length;

    private static int End(KsToken t) => t.Position + t.Length;

    private static KsBinary Binary(KsBinaryOp op, KsExpr left, KsExpr right) =>
        new(op, left, right, left.Position, End(right) - left.Position);

    private KsExpr ParseTernary()
    {
        EnterDepth();
        var condition = ParseOr();
        if (Current.Kind == KsTokenKind.Question)
        {
            Advance();
            var then = ParseTernary();
            Expect(KsTokenKind.Colon, "':'");
            var otherwise = ParseTernary();
            condition = new KsTernary(condition, then, otherwise, condition.Position, End(otherwise) - condition.Position);
        }
        _depth--;
        return condition;
    }

    private KsExpr ParseOr() => ParseKeywordChain("or", KsBinaryOp.Or, ParseAnd);

    private KsExpr ParseAnd() => ParseKeywordChain("and", KsBinaryOp.And, ParseNot);

    private KsExpr ParseKeywordChain(string keyword, KsBinaryOp op, Func<KsExpr> next)
    {
        var left = next();
        var added = 0;
        while (IsKeyword(keyword))
        {
            Advance();
            EnterDepth();
            added++;
            left = Binary(op, left, next());
        }
        _depth -= added;
        return left;
    }

    private KsExpr ParseNot()
    {
        if (!IsKeyword("not")) return ParseComparison();

        var token = Advance();
        EnterDepth();
        var operand = ParseNot();
        _depth--;
        return new KsUnary(KsUnaryOp.Not, operand, token.Position, End(operand) - token.Position);
    }

    private KsExpr ParseComparison()
    {
        var left = ParseBitOr();
        if (ComparisonOp(Current.Kind) is not { } op) return left;

        Advance();
        var right = ParseBitOr();
        if (ComparisonOp(Current.Kind) is not null)
            throw Error("Comparison operators cannot be chained", Current);
        return Binary(op, left, right);
    }

    private static KsBinaryOp? ComparisonOp(KsTokenKind kind) => kind switch
    {
        KsTokenKind.Eq => KsBinaryOp.Eq,
        KsTokenKind.Ne => KsBinaryOp.Ne,
        KsTokenKind.Lt => KsBinaryOp.Lt,
        KsTokenKind.Le => KsBinaryOp.Le,
        KsTokenKind.Gt => KsBinaryOp.Gt,
        KsTokenKind.Ge => KsBinaryOp.Ge,
        _ => null,
    };

    private KsExpr ParseBitOr() =>
        ParseLeftAssoc(ParseBitXor, k => k == KsTokenKind.Pipe ? KsBinaryOp.BitOr : null);

    private KsExpr ParseBitXor() =>
        ParseLeftAssoc(ParseBitAnd, k => k == KsTokenKind.Caret ? KsBinaryOp.BitXor : null);

    private KsExpr ParseBitAnd() =>
        ParseLeftAssoc(ParseShift, k => k == KsTokenKind.Amp ? KsBinaryOp.BitAnd : null);

    private KsExpr ParseShift() => ParseLeftAssoc(ParseAdditive, k => k switch
    {
        KsTokenKind.Shl => KsBinaryOp.Shl,
        KsTokenKind.Shr => KsBinaryOp.Shr,
        _ => null,
    });

    private KsExpr ParseAdditive() => ParseLeftAssoc(ParseMultiplicative, k => k switch
    {
        KsTokenKind.Plus => KsBinaryOp.Add,
        KsTokenKind.Minus => KsBinaryOp.Sub,
        _ => null,
    });

    private KsExpr ParseMultiplicative() => ParseLeftAssoc(ParseUnary, k => k switch
    {
        KsTokenKind.Star => KsBinaryOp.Mul,
        KsTokenKind.Slash => KsBinaryOp.Div,
        KsTokenKind.Percent => KsBinaryOp.Mod,
        _ => null,
    });

    private KsExpr ParseLeftAssoc(Func<KsExpr> next, Func<KsTokenKind, KsBinaryOp?> opOf)
    {
        var left = next();
        var added = 0;
        while (opOf(Current.Kind) is { } op)
        {
            Advance();
            EnterDepth();
            added++;
            left = Binary(op, left, next());
        }
        _depth -= added;
        return left;
    }

    private KsExpr ParseUnary()
    {
        KsUnaryOp? op = Current.Kind switch
        {
            KsTokenKind.Plus => KsUnaryOp.Plus,
            KsTokenKind.Minus => KsUnaryOp.Minus,
            KsTokenKind.Tilde => KsUnaryOp.BitNot,
            _ => null,
        };
        if (op is null) return ParsePostfix();

        var token = Advance();
        EnterDepth();
        var operand = ParseUnary();
        _depth--;
        return new KsUnary(op.Value, operand, token.Position, End(operand) - token.Position);
    }

    private KsExpr ParsePostfix()
    {
        var expr = ParseAtom();
        var added = 0;
        while (true)
        {
            if (Current.Kind == KsTokenKind.Dot)
            {
                Advance();
                EnterDepth();
                added++;
                var name = Expect(KsTokenKind.Identifier, "name");
                if (name.Text == "as" && Current.Kind == KsTokenKind.Lt)
                {
                    Advance();
                    var type = ParseTypeRef();
                    var close = ExpectTypeClose();
                    expr = new KsCast(expr, type, expr.Position, End(close) - expr.Position);
                }
                else if (Current.Kind == KsTokenKind.LParen)
                {
                    Advance();
                    var arguments = ParseList(KsTokenKind.RParen);
                    var close = Expect(KsTokenKind.RParen, "')'");
                    expr = new KsCall(expr, name.Text, arguments, expr.Position, End(close) - expr.Position);
                }
                else
                {
                    expr = new KsMember(expr, name.Text, expr.Position, End(name) - expr.Position);
                }
            }
            else if (Current.Kind == KsTokenKind.LBracket)
            {
                Advance();
                EnterDepth();
                added++;
                var index = ParseTernary();
                var close = Expect(KsTokenKind.RBracket, "']'");
                expr = new KsIndex(expr, index, expr.Position, End(close) - expr.Position);
            }
            else
            {
                _depth -= added;
                return expr;
            }
        }
    }

    private List<KsExpr> ParseList(KsTokenKind end)
    {
        var items = new List<KsExpr>();
        if (Current.Kind == end) return items;

        items.Add(ParseTernary());
        while (Current.Kind == KsTokenKind.Comma)
        {
            Advance();
            items.Add(ParseTernary());
        }
        return items;
    }

    private KsTypeRef ParseTypeRef()
    {
        var path = new List<string> { Expect(KsTokenKind.Identifier, "name").Text };
        while (Current.Kind == KsTokenKind.DoubleColon)
        {
            Advance();
            path.Add(Expect(KsTokenKind.Identifier, "name").Text);
        }
        return new KsTypeRef(path);
    }

    private KsToken ExpectTypeClose()
    {
        if (Current.Kind != KsTokenKind.Shr) return Expect(KsTokenKind.Gt, "'>'");

        var shr = Current;
        _tokens[_index] = new KsToken(KsTokenKind.Gt, shr.Position + 1, 1);
        return new KsToken(KsTokenKind.Gt, shr.Position, 1);
    }

    private KsExpr ParseAtom()
    {
        var token = Current;
        switch (token.Kind)
        {
            case KsTokenKind.Integer:
                Advance();
                return new KsIntLiteral(token.IntValue, token.Position, token.Length);
            case KsTokenKind.Float:
                Advance();
                return new KsFloatLiteral(token.FloatValue, token.Position, token.Length);
            case KsTokenKind.String:
            {
                Advance();
                var text = token.Text;
                var end = End(token);
                while (Current.Kind == KsTokenKind.String)
                {
                    text += Current.Text;
                    end = End(Current);
                    Advance();
                }
                return new KsStringLiteral(text, token.Position, end - token.Position);
            }
            case KsTokenKind.FString:
                Advance();
                return ParseFString(token);
            case KsTokenKind.LParen:
            {
                Advance();
                var inner = ParseTernary();
                Expect(KsTokenKind.RParen, "')'");
                return inner;
            }
            case KsTokenKind.LBracket:
            {
                Advance();
                var elements = ParseList(KsTokenKind.RBracket);
                var close = Expect(KsTokenKind.RBracket, "']'");
                return new KsArrayLiteral(elements, token.Position, End(close) - token.Position);
            }
            case KsTokenKind.Identifier:
                return ParseIdentifierAtom();
            case KsTokenKind.End:
                throw Error("Unexpected end of expression", token);
            default:
                throw Error("Value expected", token);
        }
    }

    private KsExpr ParseIdentifierAtom()
    {
        var token = Advance();
        switch (token.Text)
        {
            case "true":
                return new KsBoolLiteral(true, token.Position, token.Length);
            case "false":
                return new KsBoolLiteral(false, token.Position, token.Length);
            case "sizeof" or "bitsizeof" when Current.Kind == KsTokenKind.Lt:
            {
                Advance();
                var type = ParseTypeRef();
                var close = ExpectTypeClose();
                return new KsSizeOf(type, token.Text == "bitsizeof", token.Position, End(close) - token.Position);
            }
        }

        if (Current.Kind != KsTokenKind.DoubleColon)
            return new KsName(token.Text, token.Position, token.Length);

        var path = new List<string> { token.Text };
        var last = token;
        while (Current.Kind == KsTokenKind.DoubleColon)
        {
            Advance();
            last = Expect(KsTokenKind.Identifier, "name");
            path.Add(last.Text);
        }
        return new KsEnumRef(path.GetRange(0, path.Count - 1), path[^1], token.Position, End(last) - token.Position);
    }

    private KsFString ParseFString(KsToken token)
    {
        var parts = new List<KsExpr>();
        foreach (var part in token.FStringParts)
        {
            parts.Add(part.IsExpression
                ? Parse(part.Text, part.Position, _depth)
                : new KsStringLiteral(part.Text, part.Position, part.Text.Length));
        }
        return new KsFString(parts, token.Position, token.Length);
    }
}
