using System.Collections.Generic;
using Xunit;
using Monkey.Core;

namespace Monkey.Tests
{
    public class AstTests
    {
        [Fact]
        public void TestString()
        {
            // In this test we construct the AST by hand. Then writing tests for
            // the we parser we don't, but make assertions about the AST which
            // the parser produces. Tests shows that we can add another readable
            // layer of tests for our parser by comparing the parser output with
            // a string, a feature especially handdy when parsing expressions.
            var program = new Program {
                Statements = new List<IStatement> {
                    new LetStatement {
                        Token = new Token { Type = TokenType.Let, Literal = "let" },
                        Name = new Identifier {
                            Token = new Token { Type = TokenType.Ident, Literal = "myVar" },
                            Value = "myVar"
                        },
                        Value = new Identifier {
                            Token = new Token { Type = TokenType.Ident, Literal = "anotherVar" },
                            Value = "anotherVar"
                        }
                    }
                }
            };

            var x = program.String;
            Assert.Equal("let myVar = anotherVar;", program.String);
        }
    }
}