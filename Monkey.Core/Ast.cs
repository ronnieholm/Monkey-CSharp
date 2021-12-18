using System.Collections.Generic;
using System.Linq;

namespace Monkey.Core;

public record Node(Token Token);

public record Statement(Token Token) : Node(Token)
{
    public string TokenLiteral => Token.Literal;
    public virtual string String => Token.Literal;
}

public record Expression(Token Token) : Node(Token)
{
    public string TokenLiteral => Token.Literal;
    public virtual string String => Token.Literal;
}

public record Program(List<Statement> Statements) : Node(/* placeholder token */ new Token(TokenType.Illegal, ""))
{
    public string TokenLiteral => Statements.Count > 0 ? Statements[0].TokenLiteral : "";
    public string String => string.Join("", Statements.Select(s => s.String));
}

public record LetStatement(Token Token, Identifier Name, Expression Value) : Statement(Token)
{
    // LetStatement, ReturnStatement, and ExpressionStatement may contain a
    // null member. This happens when attempting to call Program.String on a
    // program with parse errors. Program.String isn't called by tests nor
    // CLI, but may be enabled during debugging.
    public override string String => $"{TokenLiteral} {Name.String} = {Value.String};";
}

public record ReturnStatement(Token Token, Expression ReturnValue) : Statement(Token)
{
    public override string String => $"{TokenLiteral} {(ReturnValue.String)};";
}

public record ExpressionStatement(Token Token, Expression Expression) : Statement(Token)
{
    public override string String => Expression.String;
}

public record Identifier(Token Token, string Value) : Expression(Token);

public record IntegerLiteral(Token Token, long Value) : Expression(Token);

public record PrefixExpression(Token Token, string Operator, Expression Right) : Expression(Token)
{
    public override string String => $"({Operator}{Right.String})";
}

public record InfixExpression(Token Token, string Operator, Expression Left, Expression Right) : Expression(Token)
{
    public override string String => $"({Left.String} {Operator} {Right.String})";
}

public record Boolean(Token Token, bool Value) : Expression(Token)
{
    public override string String => Token.Literal.ToLower();
}

public record BlockStatement(Token Token, List<Statement> Statements) : Statement(Token)
{
    public override string String => string.Join("", Statements.Select(s => s.String));
}

public record IfExpression(Token Token, Expression Condition, BlockStatement Consequence, BlockStatement? Alternative) : Expression(Token)
{
    public override string String
    {
        get
        {
            var s = $"if {Condition.String} {{{Consequence.String}}}";
            if (Alternative != null)
                s += $" else {{{Alternative.String}}}";
            return s;
        }
    }
}

public record FunctionLiteral(Token Token, List<Identifier> Parameters, BlockStatement Body) : Expression(Token)
{
    public override string String => $"{TokenLiteral}({string.Join(", ", Parameters.Select(p => p.String))}) {Body.String}";
}

public record CallExpression(Token Token, Expression Function, List<Expression> Arguments) : Expression(Token)
{
    public override string String => $"{Function.String}({string.Join(", ", Arguments.Select(a => a.String))})";
}

public record StringLiteral(Token Token, string Value) : Expression(Token)
{
    public override string String => Token.Literal;
}

public record ArrayLiteral(Token Token, List<Expression> Elements) : Expression(Token)
{
    public override string String => $"[{string.Join(", ", Elements.Select(e => e.String))}]";
}

public record IndexExpression(
    Token Token,
        
    // Object being accessed is an expression as it can be an identifier, an
    // array literal, or a function call.
    Expression Left,
    Expression Index) : Expression(Token)
{
    public override string String => $"({Left.String}[{Index.String}])";
}

public record HashLiteral(Token Token, Dictionary<Expression, Expression> Pairs) : Expression(Token)
{
    public override string String => $"{{{string.Join(", ", Pairs.Select(kv => $"{kv.Key.String}:{kv.Value.String}"))}}}";
}