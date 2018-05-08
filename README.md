# MonkeyLang

This repository contains an interpreter for the Monkey dynamic programming
language from the [Writing an interpreter in Go](https://interpreterbook.com)
book. The code is closely based on the book and has feature parity with it, but
is implemented in idiomatic C# and runs on .NET Core.

The complete implementation of the lexer, parser, and evaluator is 2,200 lines
of code with an additional 1,150 lines of tests. Not a lot for such a capable
language, implemented entirely without third party libraries.

The Monkey parser is a hand written hybrid of a traditional recursive descent
parser for statements and a Pratt (Top Down Operator Precedence) parser for
expressions. The hybrid parser ensures efficient parsing while supporting
operator precedence and associativity. The parser outputs an abstract syntax
tree which the tree walking evaluator traverses as part of executing the Monkey
program.

## Features

The Monkey interpreter supports mathematical expressions, variable bindings,
functions and the application of those functions, conditionals, return
statements, and advanced concepts such as higher-order functions and closures.
The data types supported are integers, booleans, strings, arrays, and hashes.

## Examples

See the [official](https://interpreterbook.com) homepage. Navigate to "The
Monkey Programming Language" section for examples and browse through the Tests
in this repository.

## Getting started

    $ git clone https://github.com/ronnieholm/MonkeyLang.git
    $ cd MonkeyLang
    $ dotnet build
    $ dotnet test Monkey.Tests
    $ dotnet run -p Monkey.Cli

## Resources

- [Top Down Operator
  Precedence](https://web.archive.org/web/20151223215421/http://hall.org.ua/halls/wizzard/pdf/Vaughan.Pratt.TDOP.pdf),
  Vaughan R. Pratt.
- [Some problems of recursive descent parsers](https://eli.thegreenplace.net/2009/03/14/some-problems-of-recursive-descent-parsers) by Eli Bendersky.
- [Top-Down operator precedence parsing](https://eli.thegreenplace.net/2010/01/02/top-down-operator-precedence-parsing) by Eli Bendersky.
- [A recursive descent parser with an infix expression evaluator](https://eli.thegreenplace.net/2009/03/20/a-recursive-descent-parser-with-an-infix-expression-evaluator) by Eli Bendersky.
- [Parsing expressions by precedence climbing](https://eli.thegreenplace.net/2012/08/02/parsing-expressions-by-precedence-climbing.html) by Eli Bendersky.
- [Practical explanation and example of Pratt parser](http://journal.stuffwithstuff.com/2011/03/19/pratt-parsers-expression-parsing-made-easy) by Bob Nystrom.
- [GoRuby](https://github.com/goruby/goruby) by (Michael
  Wagner)[https://twitter.com/mitch000001] extends the concepts, structures, and
  code of Monkey to Ruby.