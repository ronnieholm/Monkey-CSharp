using System.Collections.Generic;
using Xunit;
using Monkey.Core;

namespace Monkey.Tests
{
    public class LexerTests
    {
        [Fact]
        public void TestNextToken()
        {
            // Although it looks like Monkey source code, some lines don't make
            // sense. That's okay, because the lexer's job isn't to tell use
            // whether code makes sense, works, or contains errors. That comes
            // later. The lexer should only turn the source into tokens.
            const string source = @"
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
                ""foobar""
                ""foo bar""
                [1, 2];
                {""foo"": ""bar""}";

            var tokens = new List<Token>
            {
                new Token(TokenType.Let, "let"),
                new Token(TokenType.Ident, "five"),
                new Token(TokenType.Assign, "="),
                new Token(TokenType.Int, "5"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.Let, "let"),
                new Token(TokenType.Ident, "ten"),
                new Token(TokenType.Assign, "="),
                new Token(TokenType.Int, "10"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.Let, "let"),
                new Token(TokenType.Ident, "add"),
                new Token(TokenType.Assign, "="),
                new Token(TokenType.Function, "fn"),
                new Token(TokenType.LParen, "("),
                new Token(TokenType.Ident, "x"),
                new Token(TokenType.Comma, ","),
                new Token(TokenType.Ident, "y"),
                new Token(TokenType.RParen, ")"),
                new Token(TokenType.LBrace, "{"),
                new Token(TokenType.Ident, "x"),
                new Token(TokenType.Plus, "+"),
                new Token(TokenType.Ident, "y"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.RBrace, "}"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.Let, "let"),
                new Token(TokenType.Ident, "result"),
                new Token(TokenType.Assign, "="),
                new Token(TokenType.Ident, "add"),
                new Token(TokenType.LParen, "("),
                new Token(TokenType.Ident, "five"),
                new Token(TokenType.Comma, ","),
                new Token(TokenType.Ident, "ten"),
                new Token(TokenType.RParen, ")"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.Bang, "!"),
                new Token(TokenType.Minus, "-"),
                new Token(TokenType.Slash, "/"),
                new Token(TokenType.Asterisk, "*"),
                new Token(TokenType.Int, "5"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.Int, "5"),
                new Token(TokenType.Lt, "<"),
                new Token(TokenType.Int, "10"),
                new Token(TokenType.Gt, ">"),
                new Token(TokenType.Int, "5"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.If, "if"),
                new Token(TokenType.LParen, "("),
                new Token(TokenType.Int, "5"),
                new Token(TokenType.Lt, "<"),
                new Token(TokenType.Int, "10"),
                new Token(TokenType.RParen, ")"),
                new Token(TokenType.LBrace, "{"),
                new Token(TokenType.Return, "return"),
                new Token(TokenType.True, "true"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.RBrace, "}"),
                new Token(TokenType.Else, "else"),
                new Token(TokenType.LBrace, "{"),
                new Token(TokenType.Return, "return"),
                new Token(TokenType.False, "false"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.RBrace, "}"),
                new Token(TokenType.Int, "10"),
                new Token(TokenType.Eq, "=="),
                new Token(TokenType.Int, "10"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.Int, "10"),
                new Token(TokenType.NotEq, "!="),
                new Token(TokenType.Int, "9"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.String, "foobar"),
                new Token(TokenType.String, "foo bar"),
                new Token(TokenType.LBracket, "["),
                new Token(TokenType.Int, "1"),
                new Token(TokenType.Comma, ","),
                new Token(TokenType.Int, "2"),
                new Token(TokenType.RBracket, "]"),
                new Token(TokenType.Semicolon, ";"),
                new Token(TokenType.LBrace, "{"),
                new Token(TokenType.String, "foo"),
                new Token(TokenType.Colon, ":"),
                new Token(TokenType.String, "bar"),
                new Token(TokenType.RBrace, "}"),
                new Token(TokenType.Eof, ""),
            };

            var lexer = new Lexer(source);
            foreach (var expected in tokens)
            {
                var actual = lexer.NextToken();
                Assert.Equal(expected, actual);
            }
        }
    }
}
