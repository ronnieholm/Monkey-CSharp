using System.Collections.Generic;
using Xunit;
using Monkey.Core;

namespace Monkey.Tests;

public class LexerTests
{
    [Fact]
    public void TestNextToken()
    {
        // Although it looks like Monkey source code, some lines don't make
        // sense. That's okay, because the lexer's job isn't to tell use
        // whether code makes sense, works, or contains errors. That comes
        // later. The lexer should only turn the source into tokens.
        const string source = """
                              let five = 5;
                              let ten = 10;
                              let add = fn(x, y) {
                                  x + y;
                              };
                              let result = add(five, ten);
                              !-/*5;
                              5 < 10 > 5;
                              
                              if (5 < 10) {
                                  return true;
                              } else {
                                  return false;
                              }
                              
                              10 == 10;
                              10 != 9;
                              "foobar"
                              "foo bar"
                              [1, 2];
                              {"foo": "bar"}
                              """;

        var tokens = new List<Token>
        {
            new(TokenType.Let, "let"),
            new(TokenType.Ident, "five"),
            new(TokenType.Assign, "="),
            new(TokenType.Int, "5"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.Let, "let"),
            new(TokenType.Ident, "ten"),
            new(TokenType.Assign, "="),
            new(TokenType.Int, "10"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.Let, "let"),
            new(TokenType.Ident, "add"),
            new(TokenType.Assign, "="),
            new(TokenType.Function, "fn"),
            new(TokenType.LParen, "("),
            new(TokenType.Ident, "x"),
            new(TokenType.Comma, ","),
            new(TokenType.Ident, "y"),
            new(TokenType.RParen, ")"),
            new(TokenType.LBrace, "{"),
            new(TokenType.Ident, "x"),
            new(TokenType.Plus, "+"),
            new(TokenType.Ident, "y"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.RBrace, "}"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.Let, "let"),
            new(TokenType.Ident, "result"),
            new(TokenType.Assign, "="),
            new(TokenType.Ident, "add"),
            new(TokenType.LParen, "("),
            new(TokenType.Ident, "five"),
            new(TokenType.Comma, ","),
            new(TokenType.Ident, "ten"),
            new(TokenType.RParen, ")"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.Bang, "!"),
            new(TokenType.Minus, "-"),
            new(TokenType.Slash, "/"),
            new(TokenType.Asterisk, "*"),
            new(TokenType.Int, "5"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.Int, "5"),
            new(TokenType.Lt, "<"),
            new(TokenType.Int, "10"),
            new(TokenType.Gt, ">"),
            new(TokenType.Int, "5"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.If, "if"),
            new(TokenType.LParen, "("),
            new(TokenType.Int, "5"),
            new(TokenType.Lt, "<"),
            new(TokenType.Int, "10"),
            new(TokenType.RParen, ")"),
            new(TokenType.LBrace, "{"),
            new(TokenType.Return, "return"),
            new(TokenType.True, "true"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.RBrace, "}"),
            new(TokenType.Else, "else"),
            new(TokenType.LBrace, "{"),
            new(TokenType.Return, "return"),
            new(TokenType.False, "false"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.RBrace, "}"),
            new(TokenType.Int, "10"),
            new(TokenType.Eq, "=="),
            new(TokenType.Int, "10"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.Int, "10"),
            new(TokenType.NotEq, "!="),
            new(TokenType.Int, "9"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.String, "foobar"),
            new(TokenType.String, "foo bar"),
            new(TokenType.LBracket, "["),
            new(TokenType.Int, "1"),
            new(TokenType.Comma, ","),
            new(TokenType.Int, "2"),
            new(TokenType.RBracket, "]"),
            new(TokenType.Semicolon, ";"),
            new(TokenType.LBrace, "{"),
            new(TokenType.String, "foo"),
            new(TokenType.Colon, ":"),
            new(TokenType.String, "bar"),
            new(TokenType.RBrace, "}"),
            new(TokenType.Eof, ""),
        };

        var lexer = new Lexer(source);
        foreach (var expected in tokens)
        {
            var actual = lexer.NextToken();
            Assert.Equal(expected, actual);
        }
    }
}