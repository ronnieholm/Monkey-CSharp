using System;
using System.Collections.Generic;
using System.IO;
using Monkey.Core;
using static System.Console;

namespace Monkey.Cli
{
    internal static class Program
    {
        private const string MonkeyFace= @"                 __,__
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

        private static void Main(string[] args)
        {
            const string prompt = ">> ";
            WriteLine(MonkeyFace);
            
            if (args.Length == 0)
            {
                WriteLine($"Hello {Environment.UserName}! This is the Monkey programming language!");
                WriteLine("Feel free to type in commands");
            }

            // The environment must survive across inputs and remain for as long
            // as the REPL is running. Otherwise it wouldn't be possible for
            // variables and functions to survive across inputs.
            var env = new MonkeyEnvironment();
            while (true)
            {
                string? line;
                if (args.Length == 0)
                {
                    Write(prompt);
                    line = ReadLine();
                }
                else
                    line = File.ReadAllText(args[0]);

                if (line == null)
                {
                    WriteLine("Invalid input");
                    continue;
                }

                var lexer = new Lexer(line);
                var parser = new Parser(lexer, false);
                var program = parser.ParseProgram();

                if (parser.Errors.Count > 0)
                {
                    PrintParserErrors(parser.Errors);
                    continue;
                }

                var evaluated = Evaluator.Eval(program, env);
                WriteLine(evaluated.Inspect());

                if (args.Length == 1)
                    break;
            }
        }

        private static void PrintParserErrors(List<string> errors)
        {
            WriteLine("Whoops! We ran into some monkey business here!");
            WriteLine(" Parser errors");
            foreach (var msg in errors)
                WriteLine($"\t{msg}\n");
        }
    }
}
