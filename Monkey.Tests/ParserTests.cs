using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Monkey.Core;
using Boolean = Monkey.Core.Boolean;

namespace Monkey.Tests;

public class ParserTests
{
    private static Program SetupProgram(string source)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer, false);
        var program = parser.ParseProgram();
        CheckParserErrors(parser);
        return program;
    }

    [Theory]
    [InlineData("let x = 5;", "x", 5L)]
    [InlineData("let y = true", "y", true)]
    [InlineData("let foobar = y;", "foobar", "y")]
    public void TestLetStatements(string source, string expectedIdentifier, object expectedValue)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = p.Statements[0];
        TestLetStatement(stmt, expectedIdentifier);

        var letStmt = Assert.IsType<LetStatement>(stmt);
        var val = letStmt.Value;
        TestLiteralExpression(val, expectedValue);
    }

    [Theory]
    [InlineData("return 5;", 5L)]
    [InlineData("return true;", true)]
    [InlineData("return foobar;", "foobar")]
    public void TestReturnStatements(string source, object expected)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = p.Statements[0];
        var returnStmt = Assert.IsType<ReturnStatement>(stmt);
        Assert.Equal("return", returnStmt.TokenLiteral);
        TestLiteralExpression(returnStmt.ReturnValue, expected);
    }

    [Theory]
    [InlineData("foobar")]
    public void TestIdentifierExpression(string source)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        Assert.IsType<Identifier>(stmt.Expression);
        var ident = (Identifier)stmt.Expression;
        Assert.Equal("foobar", ident.Value);
        Assert.Equal("foobar", ident.TokenLiteral);
    }

    [Theory]
    [InlineData("5")]
    public void TestIntegerLiteralExpression(string source)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var literal = Assert.IsType<IntegerLiteral>(stmt.Expression);
        Assert.Equal(5, literal.Value);
        Assert.Equal("5", literal.TokenLiteral);
    }

    [Theory]
    [InlineData("!5;", "!", 5L)]
    [InlineData("-15;", "-", 15L)]
    [InlineData("!true;", "!", true)]
    [InlineData("!false;", "!", false)]
    public void TestParsingPrefixExpressions(string source, string op, object value)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var expr = Assert.IsType<PrefixExpression>(stmt.Expression);
        Assert.Equal(op, expr.Operator);
        TestLiteralExpression(expr.Right, value);
    }

    [Theory]
    [InlineData("5 + 5;", 5L, "+", 5L)]
    [InlineData("5 - 5;", 5L, "-", 5L)]
    [InlineData("5 * 5;", 5L, "*", 5L)]
    [InlineData("5 / 5;", 5L, "/", 5L)]
    [InlineData("5 > 5;", 5L, ">", 5L)]
    [InlineData("5 < 5;", 5L, "<", 5L)]
    [InlineData("5 == 5;", 5L, "==", 5L)]
    [InlineData("5 != 5;", 5L, "!=", 5L)]
    [InlineData("true == true", true, "==", true)]
    [InlineData("true != false", true, "!=", false)]
    [InlineData("false == false", false, "==", false)]
    public void TestParsingInfixExpressions(string source, object leftValue, string op, object rightValue)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        TestInfixExpression(stmt.Expression, leftValue, op, rightValue);
    }

    [Theory]
    [InlineData("-a * b", "((-a) * b)")]
    [InlineData("!-a", "(!(-a))")]
    [InlineData("a + b + c", "((a + b) + c)")]
    [InlineData("a + b - c", "((a + b) - c)")]
    [InlineData("a * b * c", "((a * b) * c)")]
    [InlineData("a * b / c", "((a * b) / c)")]
    [InlineData("a + b / c", "(a + (b / c))")]
    [InlineData("a + b * c + d / e - f", "(((a + (b * c)) + (d / e)) - f)")]
    [InlineData("3 + 4; -5 * -5", "(3 + 4)((-5) * (-5))")]
    [InlineData("5 > 4 == 3 < 4", "((5 > 4) == (3 < 4))")]
    [InlineData("5 > 4 != 3 < 4", "((5 > 4) != (3 < 4))")]
    [InlineData("3 + 4 * 5 == 3 * 1 + 4 * 5", "((3 + (4 * 5)) == ((3 * 1) + (4 * 5)))")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("3 > 5 == false", "((3 > 5) == false)")]
    [InlineData("3 < 5 == false", "((3 < 5) == false)")]
    [InlineData("1 + (2 + 3) + 4", "((1 + (2 + 3)) + 4)")]
    [InlineData("(5 + 5) * 2", "((5 + 5) * 2)")]
    [InlineData("2 / (5 + 5)", "(2 / (5 + 5))")]
    [InlineData("-(5 + 5)", "(-(5 + 5))")]
    [InlineData("!(true == true)", "(!(true == true))")]
    [InlineData("a + add(b * c) + d", "((a + add((b * c))) + d)")]
    [InlineData("add(a, b, 1, 2 * 3, 4 + 5, add(6, 7 * 8))", "add(a, b, 1, (2 * 3), (4 + 5), add(6, (7 * 8)))")]
    [InlineData("add(a + b + c * d / f + g)", "add((((a + b) + ((c * d) / f)) + g))")]
    [InlineData("a * [1, 2, 3, 4][b * c] * d", "((a * ([1, 2, 3, 4][(b * c)])) * d)")]
    [InlineData("add(a * b[2], b[1], 2 * [1, 2][1])", "add((a * (b[2])), (b[1]), (2 * ([1, 2][1])))")]
    public void TestOperatorPrecedenceParsing(string source, string expected)
    {
        var p = SetupProgram(source);
        Assert.Equal(expected, p.String);
    }

    [Theory]
    [InlineData("true;", true)]
    [InlineData("false;", false)]
    public void TestBooleanExpression(string source, bool expected)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var boolean = Assert.IsType<Boolean>(stmt.Expression);
        Assert.Equal(expected, boolean.Value);
    }

    [Theory]
    [InlineData("if (x < y) { x }")]
    public void TestIfExpression(string source)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var expr = Assert.IsType<IfExpression>(stmt.Expression);
        TestInfixExpression(expr.Condition, "x", "<", "y");
        Assert.Single(expr.Consequence.Statements);
        var consequence = Assert.IsType<ExpressionStatement>(expr.Consequence.Statements[0]);
        TestIdentifier(consequence.Expression, "x");
        Assert.Null(expr.Alternative);
    }

    [Theory]
    [InlineData("if (x < y) { x } else { y }")]
    public void TestIfElseExpression(string source)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var expr = Assert.IsType<IfExpression>(stmt.Expression);
        TestInfixExpression(expr.Condition, "x", "<", "y");
        Assert.Single(expr.Consequence.Statements);
        var consequence = Assert.IsType<ExpressionStatement>(expr.Consequence.Statements[0]);
        TestIdentifier(consequence.Expression, "x");
        Assert.Single(expr.Alternative!.Statements);
        var alternative = (ExpressionStatement)expr.Alternative.Statements[0];
        TestIdentifier(alternative.Expression, "y");
    }

    [Theory]
    [InlineData("fn(x, y) { x + y; }")]
    public void TestFunctionLiteralParsing(string source)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var function = Assert.IsType<FunctionLiteral>(stmt.Expression);
        Assert.Equal(2, function.Parameters.Count);
        TestLiteralExpression(function.Parameters[0], "x");
        TestLiteralExpression(function.Parameters[1], "y");
        Assert.Single(function.Body.Statements);
        var bodyStmt = Assert.IsType<ExpressionStatement>(function.Body.Statements[0]);
        TestInfixExpression(bodyStmt.Expression, "x", "+", "y");
    }

    [Theory]
    [InlineData("fn() {};", new string[0])]
    [InlineData("fn(x) {};", new[] { "x" })]
    [InlineData("fn(x, y, z) {};", new[] { "x", "y", "z" })]
    public void TestFunctionParameterParsing(string source, string[] expected)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var function = Assert.IsType<FunctionLiteral>(stmt.Expression);
        Assert.Equal(expected.Length, function.Parameters.Count);

        for (var i = 0; i < expected.Length; i++)
            TestLiteralExpression(function.Parameters[i], expected[i]);
    }

    [Theory]
    [InlineData("add(1, 2 * 3, 4 + 5);")]
    public void TestCallExpressionParsing(string source)
    {
        var p = SetupProgram(source);
        Assert.Single(p.Statements);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var expr = Assert.IsType<CallExpression>(stmt.Expression);
        TestIdentifier(expr.Function, "add");
        Assert.Equal(3, expr.Arguments.Count);
        TestLiteralExpression(expr.Arguments[0], 1L);
        TestInfixExpression(expr.Arguments[1], 2L, "*", 3L);
        TestInfixExpression(expr.Arguments[2], 4L, "+", 5L);
    }

    [Theory]
    [InlineData("add();", "add", new string[0])]
    [InlineData("add(1);", "add", new[] { "1" })]
    [InlineData("add(1, 2 * 3, 4 + 5);", "add", new[] { "1", "(2 * 3)", "(4 + 5)" })]
    public void TestCallExpressionParameterParsing(string source, string expectedIdent, string[] expectedArgs)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var expr = Assert.IsType<CallExpression>(stmt.Expression);
        TestIdentifier(expr.Function, expectedIdent);
        Assert.Equal(expectedArgs.Length, expr.Arguments.Count);
        for (var i = 0; i < expectedArgs.Length; i++)
            Assert.Equal(expectedArgs[i], expr.Arguments[i].String);
    }

    [Theory]
    [InlineData("\"hello world\"")]
    public void TestStringLiteralExpression(string source)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var literal = Assert.IsType<StringLiteral>(stmt.Expression);
        Assert.Equal("hello world", literal.Value);
    }

    [Theory]
    [InlineData("[1, 2 * 2, 3 + 3]")]
    public void TestParsingArrayLiterals(string source)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var array = Assert.IsType<ArrayLiteral>(stmt.Expression);
        Assert.Equal(3, array.Elements.Count);
        TestIntegerLiteral(array.Elements[0], 1L);
        TestInfixExpression(array.Elements[1], 2L, "*", 2L);
        TestInfixExpression(array.Elements[2], 3L, "+", 3L);
    }

    [Theory]
    [InlineData("myArray[1 + 1]")]
    public void TestParsingIndexExpression(string source)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var indexExpr = Assert.IsType<IndexExpression>(stmt.Expression);
        TestIdentifier(indexExpr.Left, "myArray");
        TestInfixExpression(indexExpr.Index, 1L, "+", 1L);
    }

    [Theory]
    [InlineData(@"{""one"": 1, ""two"": 2, ""three"": 3}")]
    public void TestParsingHashLiteralsStringKeys(string source)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var hash = Assert.IsType<HashLiteral>(stmt.Expression);
        Assert.Equal(3, hash.Pairs.Count);

        var expected = new Dictionary<string, long>
        {
            { "one", 1L },
            { "two", 2L },
            { "three", 3L },
        };

        foreach (var (expression, value) in hash.Pairs)
        {
            var key = Assert.IsType<StringLiteral>(expression);
            var expectedValue = expected[key.String];
            TestIntegerLiteral(value, expectedValue);
        }
    }

    [Theory]
    [InlineData("{}")]
    public void TestParsingEmptyHashLiteral(string source)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var hash = Assert.IsType<HashLiteral>(stmt.Expression);
        Assert.Empty(hash.Pairs);
    }

    [Theory]
    [InlineData(@"{""one"": 0 + 1, ""two"": 10 - 8, ""three"": 15 / 3}")]
    public void TestParsingHashLiteralsWithExpressions(string source)
    {
        var p = SetupProgram(source);
        var stmt = Assert.IsType<ExpressionStatement>(p.Statements[0]);
        var hash = Assert.IsType<HashLiteral>(stmt.Expression);
        Assert.Equal(3, hash.Pairs.Count);

        var tests = new Dictionary<string, Action<Expression>>
        {
            { "one", e => TestInfixExpression(e, 0L, "+", 1L) },
            { "two", e => TestInfixExpression(e, 10L, "-", 8L) },
            { "three", e => TestInfixExpression(e, 15L, "/", 3L) },
        };

        foreach (var (key, value) in hash.Pairs)
        {
            Assert.IsType<StringLiteral>(key);
            var testFunc = tests[key.String];
            testFunc(value);
        }
    }

    private void TestIntegerLiteral(Expression il, long value)
    {
        var lit = Assert.IsType<IntegerLiteral>(il);
        Assert.Equal(lit.Value, value);
        Assert.Equal(lit.TokenLiteral, value.ToString());
    }

    private static void TestIdentifier(Expression expr, string value)
    {
        var ident = Assert.IsType<Identifier>(expr);
        Assert.Equal(value, ident.Value);
        Assert.Equal(value, ident.TokenLiteral);
    }

    private void TestLiteralExpression<T>(Expression expr, T expected)
    {
        if (expected == null) throw new ArgumentNullException(nameof(expected));
        if (expected is long i)
            TestIntegerLiteral(expr, i);
        else if (expected is string s)
            TestIdentifier(expr, s);
        else if (expected is bool b)
            TestBooleanLiteral(expr, b);
        else
            throw new Exception($"Unsupported type: {expected.GetType()}");
    }

    private void TestInfixExpression<TLeft, TRight>(Expression expr, TLeft left, string op, TRight right)
    {
        var opExpr = Assert.IsType<InfixExpression>(expr);
        TestLiteralExpression(opExpr.Left, left);
        Assert.Equal(op, opExpr.Operator);
        TestLiteralExpression(opExpr.Right, right);
    }

    private static void TestBooleanLiteral(Expression expr, bool value)
    {
        var bo = Assert.IsType<Boolean>(expr);
        Assert.Equal(value, bo.Value);
        Assert.Equal(value.ToString().ToLower(), bo.TokenLiteral);
    }

    private static void TestLetStatement(Statement s, string name)
    {
        Assert.Equal("let", s.TokenLiteral);
        var l = Assert.IsType<LetStatement>(s);
        Assert.Equal(name, l.Name.Value);
        Assert.Equal(name, l.Name.TokenLiteral);
    }

    private static void CheckParserErrors(Parser p)
    {
        if (p.Errors.Count == 0)
            return;
        var s = p.Errors.Aggregate("", (current, e) => $"{current}{e}\n");
        throw new Exception($"Parser encountered {p.Errors.Count} errors\n{s}");
    }
}