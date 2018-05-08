using System;
using System.Collections.Generic;
using static System.Console;
using Monkey.Core;

namespace Monkey.Cli
{
    class Program
    {
        const string MonkeyFace= @"                 __,__
        .--.  .-""     ""-.  .--.
       / .. \/   .-. .-. \/ .. \
       | |  '|  /   Y   \ |'  | |
       | \   \  \ 0 | 0 / /   / |
        \ '- ,\.-""""""""""""""-./, -' /
         ''-' /_   ^ ^   _\ '-''
             |  \._   _./  |
             \   \ '~' /   /
              '._ '-=-' _.'
                 '-----'
        ";
        
        static void Main(string[] args)
        {
            const string prompt = ">> ";

            WriteLine($"Hello {Environment.UserName}! This is the Monkey programming language!");
            WriteLine("Feel free to type in commands");

            // Environment must survive across input and remain for as long as
            // the REPL is running. Therefore it's placed outside the loop.
            var env = new MonkeyEnvironment();
            while (true)
            {
                Write(prompt);
                var line = ReadLine();
                var lexer = new Lexer(line);
                var parser = new Parser(lexer, false);
                var program = parser.ParseProgram();

                if (parser.Errors.Count > 0)
                {
                    PrintParserErrors(parser.Errors);
                    continue;
                }
                
                var evaluated = Evaluator.Eval(program, env);
                if (evaluated != null)
                {
                    WriteLine(evaluated.Inspect());
                }
            }
        }

        static void PrintParserErrors(List<string> errors)
        {
            WriteLine(MonkeyFace);
            WriteLine("Woops! We ran into some monkey business here!");
            WriteLine(" Parser errors");
            foreach (var msg in errors)
            {
                WriteLine($"\t{msg}\n");
            }
        }
    }
}
