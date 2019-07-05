using System.Collections.Generic;

namespace Monkey.Core
{
    public enum TokenType
    {
        Illegal,    // Unknown token/character
        Eof,        // End of File stops parsing

        // Identifiers and literals
        Ident,      // add, foobar, x, y
        Int,        // 123456
        String,     // "foobar"

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

    public struct Token
    {
        public TokenType Type { get; set; }
        public string Literal { get; set; }

        public Token(TokenType type, string literal)
        {
            Type = type;
            Literal = literal;
        }

        public override string ToString() => $"({Type},{Literal})";
    }

    public class Lexer
    {
        string _input;

        // Position in input from where we last read a character.
        int _position;

        // Position in input from where we're going to read a character from
        // next.
        int _readPosition;

        // Character under examination.
        char _ch;

        Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>
        {
            { "fn", TokenType.Function },
            { "let", TokenType.Let },
            { "true", TokenType.True },
            { "false", TokenType.False },
            { "if", TokenType.If },
            { "else", TokenType.Else },
            { "return", TokenType.Return }
        };

        public Lexer(string input)
        {
            this._input = input;
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
                        tok = new Token(TokenType.Eq, c + _ch.ToString());
                    }
                    else
                        tok = new Token(TokenType.Assign, _ch.ToString());
                    break;
                case '+':
                    tok = new Token(TokenType.Plus, _ch.ToString());
                    break;
                case '-':
                    tok = new Token(TokenType.Minus, _ch.ToString());
                    break;
                case '!':
                    if (PeekChar() == '=')
                    {
                        var c = _ch;
                        ReadChar();
                        tok = new Token(TokenType.NotEq, c + _ch.ToString());
                    }
                    else
                        tok = new Token(TokenType.Bang, _ch.ToString());
                    break;
                case '/':
                    tok = new Token(TokenType.Slash, _ch.ToString());
                    break;
                case '*':
                    tok = new Token(TokenType.Asterisk, _ch.ToString());
                    break;
                case '<':
                    tok = new Token(TokenType.Lt, _ch.ToString());
                    break;
                case '>':
                    tok = new Token(TokenType.Gt, _ch.ToString());
                    break;
                case ';':
                    tok = new Token(TokenType.Semicolon, _ch.ToString());
                    break;
                case ',':
                    tok = new Token(TokenType.Comma, _ch.ToString());
                    break;
                case '(':
                    tok = new Token(TokenType.LParen, _ch.ToString());
                    break;
                case ')':
                    tok = new Token(TokenType.RParen, _ch.ToString());
                    break;
                case '{':
                    tok = new Token(TokenType.LBrace, _ch.ToString());
                    break;
                case '}':
                    tok = new Token(TokenType.RBrace, _ch.ToString());
                    break;
                case '"':
                    tok = new Token(TokenType.String, ReadString());
                    break;
                case '[':
                    tok = new Token(TokenType.LBracket, _ch.ToString());
                    break;
                case ']':
                    tok = new Token(TokenType.RBracket, _ch.ToString());
                    break;            
                case ':':
                    tok = new Token(TokenType.Colon, _ch.ToString());
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

                        // Early return is necessary because when calling
                        // ReadIdentifier() we call ReadChar() repeatedly,
                        // advancing _readPosition and _position past the last
                        // character of the current identifier. So we don't need
                        // to call NextToken after the switch again.
                        return tok;
                    }
                    else if (IsDigit(_ch))
                    {
                        var type = TokenType.Int;
                        var literal = ReadNumber();
                        return new Token(type, literal);
                    }
                    else
                    {
                        tok = new Token(TokenType.Illegal, _ch.ToString());
                        ReadChar();
                        return tok;
                    }
            }

            ReadChar();
            return tok;
        }

        public TokenType LookupIdent(string ident)
        {
            return keywords.ContainsKey(ident)
                ? keywords[ident]
                : TokenType.Ident;
        }

        private char PeekChar()
        {
            return _readPosition >= _input.Length
                ? '\0'
                : _input[_readPosition];
        }

        private void ReadChar()
        {
            _ch = _readPosition >= _input.Length
                ? '\0'
                : _input[_readPosition];
            _position = _readPosition;
            _readPosition++;
        }

        private string ReadIdentifier()
        {
            var p = _position;
            while (IsLetter(_ch))
                ReadChar();
            return _input.Substring(p, _position - p);
        }

        private string ReadNumber()
        {
            var p = _position;
            while (IsDigit(_ch))
                ReadChar();
            return _input.Substring(p, _position - p);
        }

        private bool IsLetter(char ch) =>
            'a' <= ch && ch <= 'z' || 'A' <= ch && ch <= 'Z' || ch == '_';

        private void SkipWhitespace()
        {
            while (_ch == ' ' || _ch == '\t' || _ch == '\n' || _ch == '\r')
                ReadChar();
        }

        private bool IsDigit(char ch) =>
            '0' <= ch && ch <= '9';

        private string ReadString()
        {
            var position = _position + 1;

            // BUG: Passing a string which isn't terminated by " cases an
            // infinite loop because even though we've reached the end of input,
            // the " characters hasn't been reached.
            do
            {
                ReadChar();
            }
            while (_ch != '"');
            return _input.Substring(position, _position - position);
        }
    }
}