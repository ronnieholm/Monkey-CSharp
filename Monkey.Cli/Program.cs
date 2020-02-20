using System;
using System.Collections.Generic;
using System.IO;
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
                var line = "";
                if (args.Length == 0)
                {
                    Write(prompt);
                    line = ReadLine();                                   
                }
                else
                    line = File.ReadAllText(args[0]);

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
                    WriteLine(evaluated.Inspect());

                if (args.Length == 1)
                    break;
            }
        }

        static void PrintParserErrors(List<string> errors)
        {
            WriteLine(MonkeyFace);
            WriteLine("Woops! We ran into some monkey business here!");
            WriteLine(" Parser errors");
            foreach (var msg in errors)
                WriteLine($"\t{msg}\n");
        }
    }
}
