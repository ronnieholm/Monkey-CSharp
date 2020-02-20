# Monkey-CSharp

A C# port of the Monkey programming language from the [Writing an interpreter in
Go](https://interpreterbook.com) book. It's written in idiomatic C# targeting
.NET Core on Windows, Mac, and Linux.

From the book:

> It supports mathematical expressions, variable bindings, functions and the
> application of those functions, conditionals, return statements and even
> advanced concepts like higher-order functions and closures. And then there are
> the different data types: integers, booleans, strings, arrays and hashes.

The Monkey parser consists of a hand-written LL(1) traditional recursive descent
parser for statements combined with a Pratt parser for expressions. The hybrid
parser ensures efficient parsing while elegantly supporting operator precedence.
Its outputs is an abstract syntax tree walked by the evaluator as part of
program execution.

The complete implementation of the lexer, parser, and evaluator consists of
1,500 lines of code with an additional 925 lines of tests. Not a lot for such a
capable interpreter, implemented entirely without third party libraries.

## Examples

See the [official](https://interpreterbook.com) homepage. Navigate to "The
Monkey Programming Language" section for examples and browse through the tests
in this repository.

Also have a look at the unit tests and Examples folder in this repository.

## Getting started

    $ git clone https://github.com/ronnieholm/Monkey-CSharp.git
    $ cd Monkey-CSharp
    $ dotnet build
    $ dotnet test Monkey.Tests
    $ dotnet run -p Monkey.Cli

## Resources

- [Top Down Operator
  Precedence](https://web.archive.org/web/20151223215421/http://hall.org.ua/halls/wizzard/pdf/Vaughan.Pratt.TDOP.pdf),
  Vaughan R. Pratt.
- [Some problems of recursive descent parsers](https://eli.thegreenplace.net/2009/03/14/some-problems-of-recursive-descent-parsers), Eli Bendersky.
- [Top-Down operator precedence parsing](https://eli.thegreenplace.net/2010/01/02/top-down-operator-precedence-parsing), Eli Bendersky.
- [A recursive descent parser with an infix expression evaluator](https://eli.thegreenplace.net/2009/03/20/a-recursive-descent-parser-with-an-infix-expression-evaluator), Eli Bendersky.
- [Parsing expressions by precedence climbing](https://eli.thegreenplace.net/2012/08/02/parsing-expressions-by-precedence-climbing.html), Eli Bendersky.
- [Practical explanation and example of Pratt parser](http://journal.stuffwithstuff.com/2011/03/19/pratt-parsers-expression-parsing-made-easy), Bob Nystrom.
- [GoRuby](https://github.com/goruby/goruby) by [Michael
  Wagner](https://twitter.com/mitch000001) extends the concepts, structures, and
  code of Monkey to Ruby.
- [GoAwk](https://github.com/benhoyt/goawk) implements the relatively simple Awk language.
