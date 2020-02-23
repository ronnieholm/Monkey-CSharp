using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Monkey.Core
{
    public interface INode
    {
        // For debugging and testing.
        string TokenLiteral { get; }

        // We don't override Object.ToString() to make String calls explicit.
        string String { get; }
    }

    public class Statement : INode
    {
        public Token Token { get; protected set; }
        public string TokenLiteral => Token.Literal;
        public virtual string String => Token.Literal;
    }

    public class Expression : INode
    {
        public Token Token { get; protected set; }
        public string TokenLiteral => Token.Literal;
        public virtual string String => Token.Literal;
    }

    public class Program : INode
    {
        public List<Statement> Statements { get; }
        public string TokenLiteral => Statements.Count > 0 ? Statements[0].TokenLiteral : "";
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var s in Statements)
                    sb.Append(s.String);
                return sb.ToString();
            }
        }

        public Program(List<Statement> statements)
        {
            Statements = statements;
        }
    }

    public class LetStatement : Statement
    {
        public Identifier Name { get; }
        public Expression Value { get; }
        public override string String => $"{TokenLiteral} {Name.String} = {Value.String};";

        public LetStatement(Token token, Identifier name, Expression value)
        {
            Token = token;
            Name = name;
            Value = value;
        }
    }

    public class ReturnStatement : Statement
    {
        public Expression ReturnValue { get; }
        public override string String => $"{TokenLiteral} {ReturnValue.String};";

        public ReturnStatement(Token token, Expression returnValue)
        {
            Token = token;
            ReturnValue = returnValue;
        }
    }

    public class ExpressionStatement : Statement
    {
        public Expression Expression { get; }
        public override string String => Expression != null ? Expression.String : "";

        public ExpressionStatement(Token token, Expression expression)
        {
            Token = token;
            Expression = expression;
        }
    }

    public class Identifier : Expression
    {
        public string Value { get; }

        public Identifier(Token token, string value)
        {
            Token = token;
            Value = value;
        }
    }

    public class IntegerLiteral : Expression
    {
        public long Value { get; }

        public IntegerLiteral(Token token, long value)
        {
            Token = token;
            Value = value;
        }
    }

    public class PrefixExpression : Expression
    {
        public string Operator { get; }
        public Expression Right;
        public override string String => $"({Operator}{Right.String})";

        public PrefixExpression(Token token, string @operator)
        {
            Token = token;
            Operator = @operator;
        }
    }

    public class InfixExpression : Expression
    {
        public Expression Left { get; }
        public string Operator { get; }
        public Expression Right { get; }
        public override string String => $"({Left.String} {Operator} {Right.String})";

        public InfixExpression(Token token, string @operator, Expression left, Expression right)
        {
            Token = token;
            Operator = @operator;
            Left = left;
            Right = right;
        }
    }

    public class Boolean_ : Expression
    {
        public bool Value { get; }
        public override string String => Token.Literal.ToLower();

        public Boolean_(Token token, bool value)
        {
            Token = token;
            Value = value;
        }
    }

    public class BlockStatement : Statement
    {
        public List<Statement> Statements { get; }
        public override string String        
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var stmt in Statements)
                    sb.Append(stmt.String);
                return sb.ToString();
            }
        }

        public BlockStatement(Token token, List<Statement> statements)
        {
            Token = token;
            Statements = statements;
        }
    }

    public class IfExpression : Expression
    {
        public Expression Condition { get; }
        public BlockStatement Consequence { get; }
        public BlockStatement Alternative { get; }
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

        public IfExpression(Token token, Expression condition, BlockStatement consequence, BlockStatement alternative)
        {
            Token = token;
            Condition = condition;
            Consequence = consequence;
            Alternative = alternative;
        }
    }

    public class FunctionLiteral : Expression
    {
        public List<Identifier> Parameters { get; }
        public BlockStatement Body { get; }
        public override string String        
        {
            get
            {
                var params_ = Parameters.Select(p => p.String).ToArray();
                return $"{TokenLiteral}({string.Join(", ", params_)}) {Body.String}";
            }
        }

        public FunctionLiteral(Token token, List<Identifier> parameters, BlockStatement body)
        {
            Token = token;
            Parameters = parameters;
            Body = body;
        }
    }

    public class CallExpression : Expression
    {
        public Expression Function { get; }
        public List<Expression> Arguments { get;  }
        public override string String        
        {
            get
            {
                var args = Arguments.Select(a => a.String).ToArray();
                return $"{Function.String}({string.Join(", ", args)})";
            }
        }

        public CallExpression(Token token, Expression function, List<Expression> arguments)
        {
            Token = token;
            Function = function;
            Arguments = arguments;
        }
    }

    public class StringLiteral : Expression
    {
        public string Value { get; }
        public override string String => Token.Literal;

        public StringLiteral(Token token, string value)
        {
            Token = token;
            Value = value;
        }
    }

    public class ArrayLiteral : Expression
    {
        public List<Expression> Elements { get; }
        public override string String        
        {
            get
            {
                var elements = Elements.Select(e => e.String).ToArray();
                return $"[{string.Join(", ", elements)}]";
            }
        }

        public ArrayLiteral(Token token, List<Expression> elements)
        {
            Token = token;
            Elements = elements;
        }
    }

    public class IndexExpression : Expression
    {
        // Object being accessed is an expression as it can be an identifier, an
        // array literal, or a function call.
        public Expression Left { get; }
        public Expression Index { get; }
        public override string String => $"({Left.String}[{Index.String}])";

        public IndexExpression(Token token, Expression left, Expression index)
        {
            Token = token;
            Left = left;
            Index = index;
        }
    }

    public class HashLiteral : Expression
    {
        public Dictionary<Expression, Expression> Pairs { get; }
        public override string String        
        {
            get
            {
                var pairs = Pairs.Select(kv => $"{kv.Key.String}:{kv.Value.String}");
                return $"{{{string.Join(", ", pairs)}}}";
            }
        }

        public HashLiteral(Token token, Dictionary<Expression, Expression> pairs)
        {
            Token = token;
            Pairs = pairs;
        }
    }
}
