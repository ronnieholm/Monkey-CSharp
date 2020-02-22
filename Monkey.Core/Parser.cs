using System;
using System.Collections.Generic;

// Tip: Setting a breakpoint in one of the parsing methods and inspecting the
// call stack when it's hit, effectively show the Abstract Syntax Tree at that
// point during parsing.

namespace Monkey.Core
{
    using PrefixParseFn = Func<IExpression>;
    using InfixParseFn = Func<IExpression, IExpression>;

    // In order to view output written to stdout, either run the tests from the
    // command line with "dotnet test Monkey.Tests", use the xUnit GUI runner,
    // or in VSCode run tests through the .NET Test Explorer plugin. Running
    // tests from within VSCode through the editor "run test" or "debug test",
    // stdout isn't redirected to the VSCode's Output tab.
    public class ParserTracer
    {
        private const char TraceIdentPlaceholder = '\t';
        private static int _traceLevel;
        private readonly bool _withTracing;

        private static void IncIdent() => _traceLevel++;
        private static void DecIdent() => _traceLevel--;
        private static string IdentLevel() => new string(TraceIdentPlaceholder, _traceLevel);

        private void TracePrint(string message) 
        {
            if (_withTracing)
                Console.WriteLine($"{IdentLevel()}{message}");
        }

        public ParserTracer(bool withTracing) => _withTracing = withTracing;

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
        readonly Lexer _lexer;

        // For visualizing and debugging the Pratt expression parser.
        readonly ParserTracer _tracer;

        // Acts like _position and PeekChar within the lexer, but instead of
        // pointing to characters in the input they point to current and next
        // tokens. We need _curToken, the current token under examination, to
        // decide what to do next, and we need _peekToken to guide the decision
        // in case _curToken doesn't provide us with enough information, e.g.,
        // with input "5;", _curToken is Int and we require _peekToken to decide
        // if we're at the end of the line or at the start of an arithmetic
        // expression. This implements a parser with one token lookahead.
        Token _curToken;
        Token _peekToken;

        public List<string> Errors { get; }

        // Functions based on token type called as part of Pratt parsing.
        readonly Dictionary<TokenType, PrefixParseFn> _prefixParseFns;
        readonly Dictionary<TokenType, InfixParseFn> _infixParseFns;

        // It's the relative and not absolute values of levels that matter.
        // During parsing we want to answer questions such as whether product
        // has higher precedence than equals. While using an enum over a class
        // with integer constants alleviates the need to explicitly assign a
        // value to each member, it makes debugging the Pratt parser slightly
        // more difficult. During precedence value comparisons, the debugger
        // shows strings instead of their values.
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

        // Table of precedence to map token type to precedence level. Not every
        // precedence level is present (Lowest and Prefix) and some precedence
        // levels appear more than once (LessGreater, Sum, Product). Lowest
        // serves as starting precedence for the Pratt parser while Prefix isn't
        // associated with any token but an expression as a whole. On the other
        // hand some operators such as multiplication and division share
        // precedence level.
        readonly Dictionary<TokenType, PrecedenceLevel> _precedences = new Dictionary<TokenType, PrecedenceLevel>
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
            var p = new Program { Statements = new List<IStatement>() };
            while (!CurTokenIs(TokenType.Eof))
            {
                var s = ParseStatement();
                if (s != null)
                    p.Statements.Add(s);
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
                    // return. If none of those got matched, try to parse input
                    // as a pseudo ExpressionStatement.
                    return ParseExpressionStatement();
            }
        }

        private LetStatement ParseLetStatement()
        {
            var stmt = new LetStatement() { Token = _curToken };
            if (!ExpectPeek(TokenType.Ident))
                return null;

            stmt.Name = new Identifier() { Token = _curToken, Value = _curToken.Literal };
            if (!ExpectPeek(TokenType.Assign))
                return null;

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
                NextToken();
            return stmt;
        }

        private ExpressionStatement ParseExpressionStatement()
        {
            _tracer.Trace(nameof(ParseExpressionStatement));
            var stmt = new ExpressionStatement { Token = _curToken };

            // Pass in lowest precedence since we haven't parsed anything yet.
            stmt.Expression = ParseExpression(PrecedenceLevel.Lowest);

            // Expression statements end with optional semicolon.
            if (PeekTokenIs(TokenType.Semicolon))
            {
                NextToken();
            }
            _tracer.Untrace("ParseExpressionStatement");
            return stmt;
        }

        private IExpression ParseExpression(PrecedenceLevel precedence)
        {
            _tracer.Trace(nameof(ParseExpression));
            var ok = _prefixParseFns.TryGetValue(_curToken.Type, out PrefixParseFn prefix);
            if (!ok)
            {
                NoPrefixParseFnError(_curToken.Type);
                return null;
            }
            var leftExpr = prefix();

            // precedence is what the Pratt paper refers to as right-binding
            // power and PeekPrecedence() is what it refers to as left-binding
            // power. For as long as left-binding power > right-binding power,
            // add another level to the Abstract Syntax Three, signifying
            // operations which need to be carried out first when the expression
            // is evaluated.
            while (!PeekTokenIs(TokenType.Semicolon) && precedence < PeekPrecedence())
            {
                ok = _infixParseFns.TryGetValue(_peekToken.Type, out InfixParseFn infix);
                if (!ok)
                    return leftExpr;

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

        private void PeekError(TokenType t) =>
            Errors.Add($"Expected next token to be {t}, got {_peekToken.Type} instead.");

        private IExpression ParseIdentifier() =>
            new Identifier { Token = _curToken, Value = _curToken.Literal };

        private IExpression ParseIntegerLiteral()
        {
            _tracer.Trace(nameof(ParseIntegerLiteral));
            var lit = new IntegerLiteral { Token = _curToken };

            var ok = long.TryParse(_curToken.Literal, out long value);
            if (!ok)
            {
                Errors.Add($"Could not parse '{_curToken.Literal}' as integer");
                return null;
            }
            lit.Value = value;
            _tracer.Untrace("ParseIntegerLiteral");
            return lit;
        }

        private IExpression ParseBoolean() =>
            new Boolean_ { Token = _curToken, Value = CurTokenIs(TokenType.True) };

        private IExpression ParsePrefixExpression()
        {
            _tracer.Trace(nameof(ParsePrefixExpression));
            var expr = new PrefixExpression { Token = _curToken, Operator = _curToken.Literal };
            NextToken();
            expr.Right = ParseExpression(PrecedenceLevel.Prefix);
            _tracer.Untrace("ParsePrefixExpression");
            return expr;
        }

        private void NoPrefixParseFnError(TokenType type) =>
            Errors.Add($"No prefix parse function for {type} found");

        private IExpression ParseInfixExpression(IExpression left)
        {
            _tracer.Trace(nameof(ParseInfixExpression));
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
            // null to be returned. The null is passed to Eval() but since no
            // node type is defined for null, we end up in Eval()'s default case
            // which throws an Exception. In the process of throwing this
            // exception it itself throws a NullReferenceException because node
            // in "throw new Exception($"Invalid node type: {node.GetType()}");"
            // is null. Python implementation doesn't have this issue.
            if (!ExpectPeek(TokenType.RBracket))
                return null;
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
                return null;

            NextToken();
            expression.Condition = ParseExpression(PrecedenceLevel.Lowest);

            if (!ExpectPeek(TokenType.RParen))
                return null;
            if (!ExpectPeek(TokenType.LBrace))
                return null;

            expression.Consequence = ParseBlockStatement();

            if (PeekTokenIs(TokenType.Else))
            {
                NextToken();
                if (!ExpectPeek(TokenType.LBrace))
                    return null;

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
            // infinite loop. Python implementation doesn't have this issue.
            while (!CurTokenIs(TokenType.RBrace))
            {
                var stmt = ParseStatement();
                if (stmt != null)
                    block.Statements.Add(stmt);
                NextToken();
            }
            return block;
        }

        private IExpression ParseFunctionLiteral()
        {
            var lit = new FunctionLiteral { Token = _curToken };

            if (!ExpectPeek(TokenType.LParen))
                return null;

            lit.Parameters = ParseFunctionParameters();

            if (!ExpectPeek(TokenType.LBrace))
                return null;

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
                return null;

            return identifiers;
        }

        private IExpression ParseStringLiteral() =>
            new StringLiteral { Token = _curToken, Value = _curToken.Literal };

        private IExpression ParseArrayLiteral()
        {
            return new ArrayLiteral { Token = _curToken, Elements = ParseExpressionList(TokenType.RBracket) };
        }

        // Similar to ParseFunctionParameters() except it's more general and
        // returns a list of expression rather than a list of identifiers.
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
                return null;

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
                    return null;
            }

            if (!ExpectPeek(TokenType.RBrace))
                return null;

            return hash;
        }

        private PrecedenceLevel PeekPrecedence()
        {
            var ok = _precedences.TryGetValue(_peekToken.Type, out PrecedenceLevel pv);

            // Returning Lowest when precedence level could not be determined
            // enables us to parse grouped expression. The RParen token doesn't
            // have an associated precedence, and returning Lowest is what
            // causes the parser to finish evaluating a subexpression as a
            // whole.
            return ok ? pv : PrecedenceLevel.Lowest;
        }

        private PrecedenceLevel CurPrecedence()
        {
            var ok = _precedences.TryGetValue(_curToken.Type, out PrecedenceLevel pv);
            return ok ? pv : PrecedenceLevel.Lowest;
        }
    }
}
