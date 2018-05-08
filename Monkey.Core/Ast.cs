using System.Collections.Generic;
using System.Text;

namespace Monkey.Core
{
    public interface INode
    {
        // For debugging and testing.
        string TokenLiteral { get; }

        // We don't override Object.ToString() because we want to make the
        // String call explicit.
        string String { get; }
    }

    // Marker interface.
    public interface IStatement : INode
    {
    }

    // Marker interface.
    public interface IExpression : INode
    {
    }

    public class Program : INode
    {
        public List<IStatement> Statements { get; set; }
        public string TokenLiteral { get => Statements.Count > 0 ? Statements[0].TokenLiteral : ""; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var s in Statements)
                {
                    sb.Append(s.String);
                }
                return sb.ToString();
            }
        }
    }

    public class LetStatement : IStatement
    {
        public Token Token { get; set; }
        public Identifier Name { get; set; }
        public IExpression Value { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append($"{TokenLiteral} {Name.String} = ");
                if (Value != null)
                {
                    sb.Append(Value.String);
                }
                sb.Append(";");
                return sb.ToString();
            }
        }
    }

    public class ReturnStatement : IStatement
    {
        public Token Token { get; set; }
        public IExpression ReturnValue { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append($"{TokenLiteral} ");
                if (ReturnValue != null)
                {
                    sb.Append(ReturnValue.String);
                }
                sb.Append(";");
                return sb.ToString();
            }
        }
    }

    public class ExpressionStatement : IStatement
    {
        public Token Token { get; set; }
        public IExpression Expression { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => Expression != null ? Expression.String : ""; }
    }

    public class Identifier : IExpression
    {
        public Token Token { get; set; }
        public string Value { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => Value; }
    }

    public class IntegerLiteral : IExpression
    {
        public Token Token { get; set; }
        public long Value { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => Token.Literal; }
    }

    public class PrefixExpression : IExpression
    {
        public Token Token { get; set; }
        public string Operator { get; set; }
        public IExpression Right;
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => $"({Operator}{Right.String})"; }
    }

    public class InfixExpression : IExpression
    {
        public Token Token { get; set; }
        public IExpression Left;
        public string Operator { get; set; }
        public IExpression Right;
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => $"({Left.String} {Operator} {Right.String})"; }
    }

    public class Boolean_ : IExpression
    {
        public Token Token { get; set; }
        public bool Value { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => Token.Literal.ToLower(); }
        
    }

    public class BlockStatement : IStatement
    {
        public Token Token { get; set; }
        public List<IStatement> Statements { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var stmt in Statements)
                {
                    sb.Append(stmt.String);
                }
                return sb.ToString();
            }
        }
    }

    public class IfExpression : IExpression
    {
        public Token Token { get; set; }
        public IExpression Condition { get; set; }
        public BlockStatement Consequence { get; set; }
        public BlockStatement Alternative { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("if");
                sb.Append(Condition.String);
                sb.Append(" ");
                sb.Append(Consequence.String);

                if (Alternative != null)
                {
                    sb.Append("else ");
                    sb.Append(Alternative.String);
                }                

                return sb.ToString();
            }
        }
    }

    public class FunctionLiteral : IExpression
    {
        public Token Token { get; set; }
        public List<Identifier> Parameters { get; set; }
        public BlockStatement Body { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                var params_ = new List<string>();

                foreach (var p in Parameters)
                {
                    params_.Add(p.String);
                }

                sb.Append(TokenLiteral);
                sb.Append("(");
                sb.Append(string.Join(", ", params_));
                sb.Append(") ");
                sb.Append(Body.String);

                return sb.ToString();
            }
        }
    }

    public class CallExpression : IExpression
    {
        public Token Token { get; set; }
        public IExpression Function { get; set; }
        public List<IExpression> Arguments { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                var args = new List<string>();

                foreach (var a in Arguments)
                {
                    args.Add(a.String);
                }

                sb.Append(Function.String);
                sb.Append("(");
                sb.Append(string.Join(", ", args));
                sb.Append(")");

                return sb.ToString();
            }
        }
    }

    public class StringLiteral : IExpression
    {
        public Token Token { get; set; }
        public string Value { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String { get => Token.Literal; }
    }

    public class ArrayLiteral : IExpression
    {
        public Token Token { get; set; }
        public List<IExpression> Elements { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                var elements = new List<string>();

                foreach (var e in Elements)
                {
                    elements.Add(e.String);
                }

                sb.Append("[");
                sb.Append(string.Join(", ", elements));
                sb.Append("]");

                return sb.ToString();
            }
        }
    }

    public class IndexExpression : IExpression
    {
        public Token Token { get; set; }

        // The object being accessed and it's an expression because it can be
        // identifier, an array literal, or a function call.
        public IExpression Left { get; set; }
        public IExpression Index { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("(");
                sb.Append(Left.String);
                sb.Append("[");
                sb.Append(Index.String);
                sb.Append("])");

                return sb.ToString();
            }
        }
    }

    public class HashLiteral : IExpression
    {
        public Token Token { get; set; }
        public Dictionary<IExpression, IExpression> Pairs { get; set; }
        public string TokenLiteral { get => Token.Literal; }
        public string String        
        {
            get
            {
                var sb = new StringBuilder();
                var pairs = new List<string>();
                
                foreach (var kv in Pairs)
                {
                    pairs.Add($"{kv.Key.String}:{kv.Value.String}");
                }

                sb.Append("{");
                sb.Append(string.Join(", ", pairs));
                sb.Append("}");
                
                return sb.ToString();
            }
        }
    }
}