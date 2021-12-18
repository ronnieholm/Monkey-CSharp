using System;
using System.Collections.Generic;

// Tip: Setting a breakpoint in one of the parsing methods and inspecting the
// call stack when it's hit, effectively show the Abstract Syntax Tree at that
// point during parsing.

namespace Monkey.Core;

using PrefixParseFn = Func<Expression?>;
using InfixParseFn = Func<Expression, Expression?>;

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
    private static string IdentLevel() => new(TraceIdentPlaceholder, _traceLevel);

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
    private readonly Lexer _lexer;

    // For visualizing and debugging the Pratt expression parser.
    private readonly ParserTracer _tracer;

    // Acts like _position and PeekChar within the lexer, but instead of
    // pointing to characters in the source they point to current and next
    // tokens. We need _curToken, the current token under examination, to
    // decide what to do next, and we need _peekToken to guide the decision
    // in case _curToken doesn't provide us with enough information, e.g.,
    // with source "5;", _curToken is Int and we require _peekToken to
    // decide if we're at the end of the line or at the start of an
    // arithmetic expression. This implements a parser with one token
    // lookahead.
    private Token _curToken = null!;
    private Token _peekToken = null!;

    public List<string> Errors { get; }

    // Functions based on token type called as part of Pratt parsing.
    private readonly Dictionary<TokenType, PrefixParseFn> _prefixParseFns;
    private readonly Dictionary<TokenType, InfixParseFn> _infixParseFns;

    // It's the relative and not absolute values of levels that matter.
    // During parsing we want to answer questions such as whether product
    // has higher precedence than equals. While using an enum over a class
    // with integer constants alleviates the need to explicitly assign a
    // value to each member, it makes debugging the Pratt parser slightly
    // more difficult. During precedence value comparisons, the debugger
    // shows strings instead of their values.
    private enum PrecedenceLevel
    {
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
    private readonly Dictionary<TokenType, PrecedenceLevel> _precedences = new()
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
        var statements = new List<Statement>();
        while (!CurTokenIs(TokenType.Eof))
        {
            var s = ParseStatement();

            // Many ParseX() methods return null which bubbles up the call
            // stack to here where null enables continued parsing on error.
            // The alternative would be for ParseX() methods to immediately
            // stop parsing and signal a parser error. But that wouldn't
            // allow collecting multiple errors to improve the user
            // experience. Instead parsing would halt on the first error. As
            // an example, ExpectPeek() returns null if the expected token
            // isn't found _and_ adds an error message to the Errors
            // collection. That null bubbles up to ParseStatement() which
            // skips adding the statement to the AST and moves on the next
            // statement. Evaluation of the AST shouldn't be attempted in
            // case of parser errors as the faulting parts of the program is
            // missing from the AST, likely resulting in evaluation errors.
            if (s != null)
                statements.Add(s);
            NextToken();
        }
        return new Program(statements);
    }

    private void NextToken()
    {
        _curToken = _peekToken;
        _peekToken = _lexer.NextToken();
    }

    private Statement? ParseStatement()
    {
        return _curToken.Type switch
        {
            TokenType.Let => ParseLetStatement(),
            TokenType.Return => ParseReturnStatement(),

            // The only two real statement types in Monkey are let and
            // return. If none of those got matched, try to parse source as
            // a pseudo ExpressionStatement.
            _ => ParseExpressionStatement()
        };
    }

    private LetStatement? ParseLetStatement()
    {
        var token = _curToken;
        if (!ExpectPeek(TokenType.Ident))
            return null;

        var name = new Identifier(_curToken, _curToken.Literal);
        if (!ExpectPeek(TokenType.Assign))
            return null;

        NextToken();
        var value = ParseExpression(PrecedenceLevel.Lowest);

        // Whenever a sub-parser such as ParseExpression() returns null, we
        // want to bubble up that null to ParseProgram() so that it can skip
        // adding the statement to the AST.
        if (value == null)
            return null;
        if (PeekTokenIs(TokenType.Semicolon))
            NextToken();
        return new LetStatement(token, name, value);
    }

    private ReturnStatement? ParseReturnStatement()
    {
        var token = _curToken;
        NextToken();
        var returnValue = ParseExpression(PrecedenceLevel.Lowest);
        if (returnValue == null)
            return null;
        if (PeekTokenIs(TokenType.Semicolon))
            NextToken();
        return new ReturnStatement(token, returnValue);
    }

    private ExpressionStatement? ParseExpressionStatement()
    {
        _tracer.Trace(nameof(ParseExpressionStatement));
        var token = _curToken;

        // Pass in lowest precedence since we haven't parsed anything yet.
        var expression = ParseExpression(PrecedenceLevel.Lowest);
        if (expression == null)
            return null;

        // Expression statements end with optional semicolon.
        if (PeekTokenIs(TokenType.Semicolon))
            NextToken();
        _tracer.Untrace("ParseExpressionStatement");
        return new ExpressionStatement(token, expression);
    }

    private Expression? ParseExpression(PrecedenceLevel precedence)
    {
        _tracer.Trace(nameof(ParseExpression));
        var ok = _prefixParseFns.TryGetValue(_curToken.Type, out var prefix);
        if (!ok)
        {
            NoPrefixParseFnError(_curToken.Type);
            return null;
        }
        var leftExpr = prefix!();
        if (leftExpr == null)
            return null;

        // precedence is what the Pratt paper refers to as right-binding
        // power and PeekPrecedence() is what it refers to as left-binding
        // power. For as long as left-binding power > right-binding power,
        // add another level to the Abstract Syntax Three, signifying
        // operations which need to be carried out first when the expression
        // is evaluated.
        while (!PeekTokenIs(TokenType.Semicolon) && precedence < PeekPrecedence())
        {
            ok = _infixParseFns.TryGetValue(_peekToken.Type, out var infix);
            if (!ok)
                return leftExpr;
            NextToken();
            leftExpr = infix!(leftExpr);
            if (leftExpr == null)
                return null;
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

    private Identifier ParseIdentifier() =>
        new(_curToken, _curToken.Literal);

    private IntegerLiteral? ParseIntegerLiteral()
    {
        _tracer.Trace(nameof(ParseIntegerLiteral));
        var token = _curToken;
        var ok = long.TryParse(_curToken.Literal, out var value);
        if (!ok)
        {
            Errors.Add($"Could not parse '{_curToken.Literal}' as integer");
            return null;
        }
        _tracer.Untrace("ParseIntegerLiteral");
        return new IntegerLiteral(token, value);
    }

    private Boolean ParseBoolean() =>
        new Boolean(_curToken, CurTokenIs(TokenType.True));

    private Expression? ParsePrefixExpression()
    {
        _tracer.Trace(nameof(ParsePrefixExpression));
        var token = _curToken;
        NextToken();
        var right = ParseExpression(PrecedenceLevel.Prefix);
        if (right == null)
            return null;
        _tracer.Untrace("ParsePrefixExpression");
        return new PrefixExpression(token, token.Literal, right);
    }

    private void NoPrefixParseFnError(TokenType type) =>
        Errors.Add($"No prefix parse function for {type} found");

    private InfixExpression? ParseInfixExpression(Expression left)
    {
        _tracer.Trace(nameof(ParseInfixExpression));
        var token = _curToken;
        var p = CurPrecedence();
        NextToken();
        var right = ParseExpression(p);
        if (right == null)
            return null;
        _tracer.Untrace("ParseInfixExpression");
        return new InfixExpression(token, token.Literal, left, right);
    }

    private Expression? ParseCallExpression(Expression function)
    {
        var arguments = ParseExpressionList(TokenType.RParen);
        if (arguments == null)
            return null;
        return new CallExpression(_curToken, function, arguments);
    }

    private IndexExpression? ParseIndexExpression(Expression left)
    {
        var token = _curToken;
        NextToken();
        var index = ParseExpression(PrecedenceLevel.Lowest);
        if (index == null)
            return null;

        // BUG: Attempting to parse "{}[""foo""" with a missing ] causes
        // null to be returned. The null is passed to Eval() but since no
        // node type is defined for null, we end up in Eval()'s default case
        // which throws an Exception. In the process of throwing this
        // exception it itself throws a NullReferenceException because node
        // in "throw new Exception($"Invalid node type: {node.GetType()}");"
        // is null. Python implementation doesn't have this issue.
        return ExpectPeek(TokenType.RBracket) 
            ? new IndexExpression(token, left, index)
            : null;
    }

    private Expression? ParseGroupedExpression()
    {
        NextToken();
        var expr = ParseExpression(PrecedenceLevel.Lowest);
        return !ExpectPeek(TokenType.RParen) ? null : expr;
    }

    private IfExpression? ParseIfExpression()
    {
        var token = _curToken;
        if (!ExpectPeek(TokenType.LParen))
            return null;

        NextToken();
        var condition = ParseExpression(PrecedenceLevel.Lowest);
        if (condition == null)
            return null;
        if (!ExpectPeek(TokenType.RParen))
            return null;
        if (!ExpectPeek(TokenType.LBrace))
            return null;

        var consequence = ParseBlockStatement();
        BlockStatement? alternative = null;
        if (PeekTokenIs(TokenType.Else))
        {
            NextToken();
            if (!ExpectPeek(TokenType.LBrace))
                return null;
            alternative = ParseBlockStatement();
        }
        return new IfExpression(token, condition, consequence, alternative);
    }

    private BlockStatement ParseBlockStatement()
    {
        var token = _curToken;
        var statements = new List<Statement>();
        NextToken();

        // BUG: If '}' is missing from the program, this code goes into an
        // infinite loop. Python implementation doesn't have this issue.
        while (!CurTokenIs(TokenType.RBrace))
        {
            var stmt = ParseStatement();
            if (stmt != null)
                statements.Add(stmt);
            NextToken();
        }
        return new BlockStatement(token, statements);
    }

    private FunctionLiteral? ParseFunctionLiteral()
    {
        var token =  _curToken;
        if (!ExpectPeek(TokenType.LParen))
            return null;

        var parameters = ParseFunctionParameters();
        if (parameters == null)
            return null;
        if (!ExpectPeek(TokenType.LBrace))
            return null;

        var body = ParseBlockStatement();
        return new FunctionLiteral(token, parameters, body);
    }

    private List<Identifier>? ParseFunctionParameters()
    {
        var identifiers = new List<Identifier>();
        if (PeekTokenIs(TokenType.RParen))
        {
            NextToken();
            return identifiers;
        }

        NextToken();
        var ident = new Identifier(_curToken, _curToken.Literal);
        identifiers.Add(ident);

        while (PeekTokenIs(TokenType.Comma))
        {
            NextToken();
            NextToken();
            ident = new Identifier(_curToken, _curToken.Literal);
            identifiers.Add(ident);
        }

        if (!ExpectPeek(TokenType.RParen))
            return null;
        return identifiers;
    }

    private StringLiteral ParseStringLiteral() =>
        new(_curToken, _curToken.Literal);

    private ArrayLiteral? ParseArrayLiteral()
    {
        var expr = ParseExpressionList(TokenType.RBracket);
        return expr == null ? null: new ArrayLiteral(_curToken, expr);
    }

    // Similar to ParseFunctionParameters() except it's more general and
    // returns a list of expression rather than a list of identifiers.
    private List<Expression>? ParseExpressionList(TokenType end)
    {
        var list = new List<Expression>();
        if (PeekTokenIs(end))
        {
            NextToken();
            return list;
        }

        NextToken();
        var expr = ParseExpression(PrecedenceLevel.Lowest);
        if (expr == null)
            return null;
        list.Add(expr);

        while (PeekTokenIs(TokenType.Comma))
        {
            NextToken();
            NextToken();
            var expr1 = ParseExpression(PrecedenceLevel.Lowest);
            if (expr1 == null)
                return null;
            list.Add(expr1);
        }

        return ExpectPeek(end) ? list : null;
    }

    private HashLiteral? ParseHashLiteral()
    {
        var token = _curToken;
        var pairs = new Dictionary<Expression, Expression>();
        while (!PeekTokenIs(TokenType.RBrace))
        {
            NextToken();
            var key = ParseExpression(PrecedenceLevel.Lowest);
            if (key == null)
                return null;

            if (!ExpectPeek(TokenType.Colon))
                return null;

            NextToken();
            var value = ParseExpression(PrecedenceLevel.Lowest);
            if (value == null)
                return null;
            pairs.Add(key, value);

            if (!PeekTokenIs(TokenType.RBrace) && !ExpectPeek(TokenType.Comma))
                return null;
        }

        return ExpectPeek(TokenType.RBrace) ? new HashLiteral(token, pairs) : null;
    }

    private PrecedenceLevel PeekPrecedence()
    {
        var ok = _precedences.TryGetValue(_peekToken.Type, out var pv);

        // Returning Lowest when precedence level could not be determined
        // enables us to parse grouped expression. The RParen token doesn't
        // have an associated precedence, and returning Lowest is what
        // causes the parser to finish evaluating a subexpression as a
        // whole.
        return ok ? pv : PrecedenceLevel.Lowest;
    }

    private PrecedenceLevel CurPrecedence()
    {
        var ok = _precedences.TryGetValue(_curToken.Type, out var pv);
        return ok ? pv : PrecedenceLevel.Lowest;
    }
}