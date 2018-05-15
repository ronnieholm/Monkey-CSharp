using System;
using System.Collections.Generic;

// Tip: Setting a breakpoint in one of the parsing methods and inspecting the
// call stack when its hit, will effectively show the Abstract Syntax Tree at
// that point during parsing.

namespace Monkey.Core
{
    using PrefixParseFn = Func<IExpression>;
    using InfixParseFn = Func<IExpression, IExpression>;

    // In order to view output written to stdout, either run the tests from the
    // command line with "dotnet test Monkey.Tests", use the xUnit GUI runner,
    // or in VSCode run tests through the .NET Test Explorer plugin. Running
    // tests from within the VSCode editor's "run test" or "debug test" links,
    // stdout isn't redirected to the VSCode's Output tab.
    public class ParserTracer
    {
        const char TraceIdentPlaceholder = '\t';
        static int traceLevel = 0;
        bool _withTracing;

        void IncIdent() => traceLevel++;
        void DecIdent() => traceLevel--;
        string IdentLevel() => new string(TraceIdentPlaceholder, traceLevel);
        void TracePrint(string message) 
        {
            if (_withTracing)
            {
                Console.WriteLine($"{IdentLevel()}{message}");
            }
        }

        public ParserTracer(bool withTracing)
        {
            _withTracing = withTracing;
        }

        public void Trace(string message)
        {
            TracePrint($"BEGIN {message}");
            IncIdent();
        }
        public void Untrace(string message)
        {
            DecIdent();
            TracePrint($"END {message}");
        }
    }

    public class Parser
    {
        Lexer _lexer;

        // For visualizing and debugging the Pratt expression parser for Monkey
        // expressions.
        ParserTracer _tracer;

        // Acts like _position and PeekChar within the lexer, but instead of
        // pointing to a character in the input they point to the current and
        // next tokens. We need to look at _curToken, the current token under
        // examination, to decide what to do next, and we need _peekToken to
        // guide the decision in case _curToken doesn't provide us with enough
        // information, e.g., with the input "5;", then _curToken is Int and we
        // require _peekToken to decide if where're at the end of the line or if
        // we're at the start of an arithmetic expression. In effect, this
        // implements a parser with one token lookahead.
        Token _curToken;
        Token _peekToken;

        public List<string> Errors { get; private set; }

        // Functions based on token type called as part of Pratt parsing.
        Dictionary<TokenType, PrefixParseFn> _prefixParseFns;
        Dictionary<TokenType, InfixParseFn> _infixParseFns;

        // Actual numeric numbers doesn't matter, but the order and the relation
        // to each other does. We want to be able to answer questions such as
        // whether operator * has higher precedence than operator ==. While
        // using an enum over a class with integer constants alliviates the need
        // to explicitly assign a value to each member, it making debugging the
        // Pratt parser slightly more difficult. During precedence value
        // comparisons, the debugger will show the strings over their its
        // implicit number.
        enum PrecedenceLevel
        {
            None = 0,
            Lowest,
            Equals,         // ==
            LessGreater,    // > or <
            Sum,            // +
            Product,        // *
            Prefix,         // -x or !x
            Call,           // myFunction(x)
            Index           // array[index]
        }

        // Table of precedence associated with token. Observe how not every
        // precedence value is present (Lowest and Prefix missing) and some are
        // levels are appear more than once (LessGreater, Sum, Product). Lowest
        // serves as a starting precedence for the Pratt parser while Prefix
        // isn't associated with a token but an expression as a whole. On the
        // other hand, some operators, such as multiplication and division,
        // share the same precedence level.
        Dictionary<TokenType, PrecedenceLevel> precedences = new Dictionary<TokenType, PrecedenceLevel>
        {
            { TokenType.Eq, PrecedenceLevel.Equals },
            { TokenType.NotEq, PrecedenceLevel.Equals },
            { TokenType.Lt, PrecedenceLevel.LessGreater },
            { TokenType.Gt, PrecedenceLevel.LessGreater },
            { TokenType.Plus, PrecedenceLevel.Sum },
            { TokenType.Minus, PrecedenceLevel.Sum },
            { TokenType.Slash, PrecedenceLevel.Product },
            { TokenType.Asterisk, PrecedenceLevel.Product },
            { TokenType.LParen, PrecedenceLevel.Call },
            { TokenType.LBracket, PrecedenceLevel.Index }            
        };

        private void RegisterPrefix(TokenType t, PrefixParseFn fn) => _prefixParseFns.Add(t, fn);
        private void RegisterInfix(TokenType t, InfixParseFn fn) => _infixParseFns.Add(t, fn);

        public Parser(Lexer lexer, bool withTracing)
        {
            _lexer = lexer;
            _tracer = new ParserTracer(withTracing);
            Errors = new List<string>();

            _prefixParseFns = new Dictionary<TokenType, PrefixParseFn>();
            RegisterPrefix(TokenType.Ident, ParseIdentifier);
            RegisterPrefix(TokenType.Int, ParseIntegerLiteral);
            RegisterPrefix(TokenType.Bang, ParsePrefixExpression);
            RegisterPrefix(TokenType.Minus, ParsePrefixExpression);
            RegisterPrefix(TokenType.True, ParseBoolean);
            RegisterPrefix(TokenType.False, ParseBoolean);
            RegisterPrefix(TokenType.LParen, ParseGroupedExpression);
            RegisterPrefix(TokenType.If, ParseIfExpression);
            RegisterPrefix(TokenType.Function, ParseFunctionLiteral);
            RegisterPrefix(TokenType.String, ParseStringLiteral);
            RegisterPrefix(TokenType.LBracket, ParseArrayLiteral);
            RegisterPrefix(TokenType.LBrace, ParseHashLiteral);

            _infixParseFns = new Dictionary<TokenType, InfixParseFn>();
            RegisterInfix(TokenType.Plus, ParseInfixExpression);
            RegisterInfix(TokenType.Minus, ParseInfixExpression);
            RegisterInfix(TokenType.Slash, ParseInfixExpression);
            RegisterInfix(TokenType.Asterisk, ParseInfixExpression);
            RegisterInfix(TokenType.Eq, ParseInfixExpression);
            RegisterInfix(TokenType.NotEq, ParseInfixExpression);
            RegisterInfix(TokenType.Lt, ParseInfixExpression);
            RegisterInfix(TokenType.Gt, ParseInfixExpression);
            RegisterInfix(TokenType.LParen, ParseCallExpression);
            RegisterInfix(TokenType.LBracket, ParseIndexExpression);

            // Read two tokens so _curToken and _peekToken tokens are both set.
            NextToken();
            NextToken();
        }

        public Program ParseProgram()
        {
            var p = new Program();
            p.Statements = new List<IStatement>();

            while (!CurTokenIs(TokenType.Eof))
            {
                var s = ParseStatement();
                if (s != null)
                {
                    p.Statements.Add(s);
                }
                NextToken();
            }
            return p;
        }

        private void NextToken()
        {
            _curToken = _peekToken;
            _peekToken = _lexer.NextToken();
        }

        private IStatement ParseStatement()
        {
            switch (_curToken.Type)
            {
                case TokenType.Let:
                    return ParseLetStatement();
                case TokenType.Return:
                    return ParseReturnStatement();
                default:
                    // The only two real statement types in Monkey are let and
                    // return. If none of those matched, try to parse input as
                    // pseudo ExpressionStatement.
                    return ParseExpressionStatement();
            }
        }

        private LetStatement ParseLetStatement()
        {
            var stmt = new LetStatement() { Token = _curToken };
            if (!ExpectPeek(TokenType.Ident))
            {
                return null;
            }

            stmt.Name = new Identifier() { Token = _curToken, Value = _curToken.Literal };
            if (!ExpectPeek(TokenType.Assign))
            {
                return null;
            }

            NextToken();
            stmt.Value = ParseExpression(PrecedenceLevel.Lowest);
            if (PeekTokenIs(TokenType.Semicolon))
            {
                NextToken();
            }

            return stmt;
        }

        private ReturnStatement ParseReturnStatement()
        {
            var stmt = new ReturnStatement() { Token = _curToken };

            NextToken();
            stmt.ReturnValue = ParseExpression(PrecedenceLevel.Lowest);

            if (PeekTokenIs(TokenType.Semicolon))
            {
                NextToken();
            }
            return stmt;
        }

        private ExpressionStatement ParseExpressionStatement()
        {
            _tracer.Trace("ParseExpressionStatement");
            var stmt = new ExpressionStatement { Token = _curToken };

            // Pass in lowest precedence since we haven't parsed anything yet.
            stmt.Expression = ParseExpression(PrecedenceLevel.Lowest);

            // Expression statements end with optional simicolon.
            if (PeekTokenIs(TokenType.Semicolon))
            {
                NextToken();
            }
            _tracer.Untrace("ParseExpressionStatement");
            return stmt;
        }

        private IExpression ParseExpression(PrecedenceLevel precedence)
        {
            _tracer.Trace("ParseExpression");
            PrefixParseFn prefix;
            var ok = _prefixParseFns.TryGetValue(_curToken.Type, out prefix);
            if (!ok)
            {
                NoPrefixParseFnError(_curToken.Type);
                return null;
            }
            var leftExpr = prefix();

            // The precedence is what the original Pratt paper refers to as
            // right-binding power and PeekPrecedence() is what it refers to as
            // left-binding power. For as long as left-binding power >
            // right-binding power, we're going to add another level to the
            // Abstract Syntax Three, signifying operations which need to be
            // carried out first when the expression is evaluated.
            while (!PeekTokenIs(TokenType.Semicolon) && precedence < PeekPrecedence())
            {
                InfixParseFn infix;
                ok = _infixParseFns.TryGetValue(_peekToken.Type, out infix);
                if (!ok)
                {
                    return leftExpr;
                }
                NextToken();
                leftExpr = infix(leftExpr);
            }
            _tracer.Untrace("ParseExpression");

            return leftExpr;
        }

        private bool CurTokenIs(TokenType t) => _curToken.Type == t;

        private bool PeekTokenIs(TokenType t) => _peekToken.Type == t;

        private bool ExpectPeek(TokenType t)
        {
            if (PeekTokenIs(t))
            {
                NextToken();
                return true;
            }

            PeekError(t);
            return false;
        }

        private void PeekError(TokenType t)
        {
            Errors.Add($"Expected next token to be {t}, got {_peekToken.Type} instead.");
        }

        private IExpression ParseIdentifier()
        {
            return new Identifier { Token = _curToken, Value = _curToken.Literal };
        }

        private IExpression ParseIntegerLiteral()
        {
            _tracer.Trace("ParseIntegerLiteral");
            var lit = new IntegerLiteral { Token = _curToken };

            long value;
            var ok = long.TryParse(_curToken.Literal, out value);
            if (!ok)
            {
                Errors.Add($"Could not parse '{_curToken.Literal}' as integer");
                return null;
            }
            lit.Value = value;
            _tracer.Untrace("ParseIntegerLiteral");
            return lit;
        }

        private IExpression ParseBoolean()
        {
            return new Boolean_ { Token = _curToken, Value = CurTokenIs(TokenType.True) };
        }

        private IExpression ParsePrefixExpression()
        {
            _tracer.Trace("ParsePrefixExpression");
            var expr = new PrefixExpression { Token = _curToken, Operator = _curToken.Literal };
            NextToken();
            expr.Right = ParseExpression(PrecedenceLevel.Prefix);
            _tracer.Untrace("ParsePrefixExpression");
            return expr;
        }

        private void NoPrefixParseFnError(TokenType type)
        {
            Errors.Add($"No prefix parse function for {type} found");
        }

        private IExpression ParseInfixExpression(IExpression left)
        {
            _tracer.Trace("ParseInfixExpression");
            var expr = new InfixExpression { Token = _curToken, Operator = _curToken.Literal, Left = left };
            var p = CurPrecedence();
            NextToken();
            expr.Right = ParseExpression(p);
            _tracer.Untrace("ParseInfixExpression");
            return expr;
        }

        private IExpression ParseCallExpression(IExpression function)
        {
            var expr = new CallExpression { Token = _curToken, Function = function };
            expr.Arguments = ParseExpressionList(TokenType.RParen);
            return expr;
        }

        private IExpression ParseIndexExpression(IExpression left)
        {
            var expr = new IndexExpression { Token = _curToken, Left = left };

            NextToken();
            expr.Index = ParseExpression(PrecedenceLevel.Lowest);

            // BUG: Attempting to parse "{}[""foo""" with a missing ] causes
            // null to be returned. The null is passed to Eval() for evaluation
            // but since no node type is defined for null, we end up in Eval()'s
            // default case which throws an Exception. In the process of
            // throwing this exception it itself throws a NullReferenceException
            // because node in "throw new Exception($"Invalid node type:
            // {node.GetType()}");" is null.
            if (!ExpectPeek(TokenType.RBracket))
            {
                return null;
            }
            return expr;
        }

        private IExpression ParseGroupedExpression()
        {
            NextToken();
            var expr = ParseExpression(PrecedenceLevel.Lowest);
            return !ExpectPeek(TokenType.RParen) ? null : expr;
        }

        private IExpression ParseIfExpression()
        {
            var expression = new IfExpression { Token = _curToken };

            if (!ExpectPeek(TokenType.LParen))
            {
                return null;
            }

            NextToken();
            expression.Condition = ParseExpression(PrecedenceLevel.Lowest);

            if (!ExpectPeek(TokenType.RParen))
            {
                return null;
            }

            if (!ExpectPeek(TokenType.LBrace))
            {
                return null;
            }

            expression.Consequence = ParseBlockStatement();

            if (PeekTokenIs(TokenType.Else))
            {
                NextToken();
                if (!ExpectPeek(TokenType.LBrace))
                {
                    return null;
                }

                expression.Alternative = ParseBlockStatement();
            }

            return expression;
        }

        private BlockStatement ParseBlockStatement()
        {
            var block = new BlockStatement { Token = _curToken };
            block.Statements = new List<IStatement>();

            NextToken();

            // BUG: If '}' is missing from the program, this code goes into an
            // infinite loop.
            while (!CurTokenIs(TokenType.RBrace))
            {
                var stmt = ParseStatement();
                if (stmt != null)
                {
                    block.Statements.Add(stmt);
                }
                NextToken();
            }
            return block;
        }

        private IExpression ParseFunctionLiteral()
        {
            var lit = new FunctionLiteral { Token = _curToken };

            if (!ExpectPeek(TokenType.LParen))
            {
                return null;
            }

            lit.Parameters = ParseFunctionParameters();

            if (!ExpectPeek(TokenType.LBrace))
            {
                return null;
            }

            lit.Body = ParseBlockStatement();
            return lit;
        }

        private List<Identifier> ParseFunctionParameters()
        {
            var identifiers = new List<Identifier>();
            if (PeekTokenIs(TokenType.RParen))
            {
                NextToken();
                return identifiers;
            }

            NextToken();
            var ident = new Identifier { Token = _curToken, Value = _curToken.Literal };
            identifiers.Add(ident);

            while (PeekTokenIs(TokenType.Comma))
            {
                NextToken();
                NextToken();
                ident = new Identifier { Token = _curToken, Value = _curToken.Literal };
                identifiers.Add(ident);
            }

            if (!ExpectPeek(TokenType.RParen))
            {
                return null;
            }

            return identifiers;
        }

        private IExpression ParseStringLiteral()
        {
            return new StringLiteral { Token = _curToken, Value = _curToken.Literal };
        }

        private IExpression ParseArrayLiteral()
        {
            var array = new ArrayLiteral { Token = _curToken };
            array.Elements = ParseExpressionList(TokenType.RBracket);
            return array;
        }

        // Similar to ParseFunctionParameters() expect that it's more generic
        // and returns a list of expression rather than a list of identifiers.
        private List<IExpression> ParseExpressionList(TokenType end)
        {
            var list = new List<IExpression>();

            if (PeekTokenIs(end))
            {
                NextToken();
                return list;
            }

            NextToken();
            list.Add(ParseExpression(PrecedenceLevel.Lowest));

            while (PeekTokenIs(TokenType.Comma))
            {
                NextToken();
                NextToken();
                list.Add(ParseExpression(PrecedenceLevel.Lowest));
            }

            if (!ExpectPeek(end))
            {
                return null;
            }

            return list;
        }

        private IExpression ParseHashLiteral()
        {
            var hash = new HashLiteral { Token = _curToken, Pairs = new Dictionary<IExpression, IExpression>() };
            
            while (!PeekTokenIs(TokenType.RBrace))
            {
                NextToken();
                var key = ParseExpression(PrecedenceLevel.Lowest);

                if (!ExpectPeek(TokenType.Colon))
                {
                    return null;
                }

                NextToken();
                var value = ParseExpression(PrecedenceLevel.Lowest);
                hash.Pairs.Add(key, value);

                if (!PeekTokenIs(TokenType.RBrace) && !ExpectPeek(TokenType.Comma))
                {
                    return null;
                }
            }

            if (!ExpectPeek(TokenType.RBrace))
            {
                return null;
            }

            return hash;
        }

        private PrecedenceLevel PeekPrecedence()
        {
            PrecedenceLevel pv;
            var ok = precedences.TryGetValue(_peekToken.Type, out pv);

            // Returning Lowest when precedence cannot be found for a token is
            // what enables us to parse for instance grouped expression. The
            // RParen doesn't have an associated precedence, so when Lowest is
            // returned it causes the parser to finish evaluating a
            // subexpression as a whole.
            return ok ? pv : PrecedenceLevel.Lowest;
        }

        private PrecedenceLevel CurPrecedence()
        {
            PrecedenceLevel pv;
            var ok = precedences.TryGetValue(_curToken.Type, out pv);
            return ok ? pv : PrecedenceLevel.Lowest;
        }
    }
}