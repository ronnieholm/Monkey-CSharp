using System.Collections.Generic;

namespace Monkey.Core
{
    public enum TokenType
    {
        Illegal,    // Unknown token/character
        Eof,        // End of File stops parsing

        // Identifiers and literals
        Ident,      // add, foobar, x, y
        Int,        // 123
        String,     // "foo"

        // Operators
        Assign,     // =
        Plus,       // +
        Minus,      // -
        Bang,       // !
        Asterisk,   // *
        Slash,      // /
        Lt,         // <
        Gt,         // >
        Eq,         // ==
        NotEq,      // !=

        // Delimiters
        Comma,      // ,
        Semicolon,  // ;
        LParen,     // (
        RParen,     // )
        LBrace,     // {
        RBrace,     // }
        LBracket,   // [
        RBracket,   // ]
        Colon,      // :

        // Keywords
        Function,
        Let,
        True,
        False,
        If,
        Else,
        Return
    }

    public record Token(TokenType Type, string Literal)
    {
        public Token(TokenType type, char literal) : this(type, literal.ToString())
        {
        }
    }
    
    public class Lexer
    {
        private readonly string _source;

        // Position in source where last character was read.
        private int _position;

        // Position in source where next character is read.
        private int _readPosition;

        // Character under examination.
        private char _ch;

        private readonly Dictionary<string, TokenType> _keywords = new()
        {
            { "fn", TokenType.Function },
            { "let", TokenType.Let },
            { "true", TokenType.True },
            { "false", TokenType.False },
            { "if", TokenType.If },
            { "else", TokenType.Else },
            { "return", TokenType.Return }
        };

        public Lexer(string source)
        {
            _source = source;
            ReadChar();
        }

        public Token NextToken()
        {
            Token tok;
            SkipWhitespace();

            switch (_ch)
            {
                case '=':
                    if (PeekChar() == '=')
                    {
                        var c = _ch;
                        ReadChar();
                        tok = new Token(TokenType.Eq, $"{c}{_ch}");
                    }
                    else
                        tok = new Token(TokenType.Assign, _ch);
                    break;
                case '+':
                    tok = new Token(TokenType.Plus, _ch);
                    break;
                case '-':
                    tok = new Token(TokenType.Minus, _ch);
                    break;
                case '!':
                    if (PeekChar() == '=')
                    {
                        var c = _ch;
                        ReadChar();
                        tok = new Token(TokenType.NotEq, $"{c}{_ch}");
                    }
                    else
                        tok = new Token(TokenType.Bang, _ch);
                    break;
                case '/':
                    tok = new Token(TokenType.Slash, _ch);
                    break;
                case '*':
                    tok = new Token(TokenType.Asterisk, _ch);
                    break;
                case '<':
                    tok = new Token(TokenType.Lt, _ch);
                    break;
                case '>':
                    tok = new Token(TokenType.Gt, _ch);
                    break;
                case ';':
                    tok = new Token(TokenType.Semicolon, _ch);
                    break;
                case ',':
                    tok = new Token(TokenType.Comma, _ch);
                    break;
                case '(':
                    tok = new Token(TokenType.LParen, _ch);
                    break;
                case ')':
                    tok = new Token(TokenType.RParen, _ch);
                    break;
                case '{':
                    tok = new Token(TokenType.LBrace, _ch);
                    break;
                case '}':
                    tok = new Token(TokenType.RBrace, _ch);
                    break;
                case '"':
                    tok = new Token(TokenType.String, ReadString());
                    break;
                case '[':
                    tok = new Token(TokenType.LBracket, _ch);
                    break;
                case ']':
                    tok = new Token(TokenType.RBracket, _ch);
                    break;
                case ':':
                    tok = new Token(TokenType.Colon, _ch);
                    break;
                case '\0':
                    tok = new Token(TokenType.Eof, "");
                    break;
                default:
                    if (IsLetter(_ch))
                    {
                        var ident = ReadIdentifier();
                        var type = LookupIdent(ident);
                        tok = new Token(type, ident);
                        return tok;
                    }
                    if (IsDigit(_ch))
                    {
                        var literal = ReadNumber();
                        return new Token(TokenType.Int, literal);
                    }

                    tok = new Token(TokenType.Illegal, _ch);
                    ReadChar();
                    return tok;
            }

            ReadChar();
            return tok;
        }

        private TokenType LookupIdent(string ident)
        {
            return _keywords.ContainsKey(ident)
                ? _keywords[ident]
                : TokenType.Ident;
        }

        private char PeekChar()
        {
            return _readPosition >= _source.Length
                ? '\0'
                : _source[_readPosition];
        }

        private void ReadChar()
        {
            _ch = _readPosition >= _source.Length
                ? '\0'
                : _source[_readPosition];
            _position = _readPosition;
            _readPosition++;
        }

        private string ReadIdentifier()
        {
            var p = _position;
            while (IsLetter(_ch))
                ReadChar();
            return _source.Substring(p, _position - p);
        }

        private string ReadNumber()
        {
            var p = _position;
            while (IsDigit(_ch))
                ReadChar();
            return _source.Substring(p, _position - p);
        }

        private static bool IsLetter(char ch) =>
            'a' <= ch && ch <= 'z' || 'A' <= ch && ch <= 'Z' || ch == '_';

        private void SkipWhitespace()
        {
            while (_ch is ' ' or '\t' or '\n' or '\r')
                ReadChar();
        }

        private static bool IsDigit(char ch) =>
            '0' <= ch && ch <= '9';

        private string ReadString()
        {
            var position = _position + 1;

            // BUG: Passing a string which isn't " terminated causes an infinite
            // loop because even though we reached the end of source, the "
            // characters hasn't been reached.
            do
            {
                ReadChar();
            }
            while (_ch != '"');
            return _source.Substring(position, _position - position);
        }
    }
}
