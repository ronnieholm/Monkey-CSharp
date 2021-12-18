using System.Collections.Generic;
using Xunit;
using Monkey.Core;

namespace Monkey.Tests;

public class AstTests
{
    [Fact]
    public void TestString()
    {
        // In this test we construct the AST by hand. Then writing tests for
        // the we parser we don't, but make assertions about the AST which
        // the parser produces. Tests shows that we can add another readable
        // layer of tests for our parser by comparing the parser output with
        // a string, a feature especially handy when parsing expressions.
        var program = new Program(
            new List<Statement>
            {
                new LetStatement(
                    new Token(TokenType.Let, "let"),
                    new Identifier(new Token(TokenType.Ident, "myVar"), "myVar"),
                    new Identifier(new Token(TokenType.Ident, "anotherVar"), "anotherVar"))
            });
        Assert.Equal("let myVar = anotherVar;", program.String);
    }
}