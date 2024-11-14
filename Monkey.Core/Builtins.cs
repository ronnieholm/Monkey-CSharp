using System;
using System.Linq;
using System.Collections.Generic;

namespace Monkey.Core;

public static class MonkeyBuiltins
{
    public static readonly Dictionary<string, MonkeyBuiltin> Builtins = new();

    static MonkeyBuiltins()
    {
        Builtins.Add("len", new MonkeyBuiltin(Len));
        Builtins.Add("first", new MonkeyBuiltin(First));
        Builtins.Add("last", new MonkeyBuiltin(Last));
        Builtins.Add("rest", new MonkeyBuiltin(Rest));
        Builtins.Add("push", new MonkeyBuiltin(Push));
        Builtins.Add("puts", new MonkeyBuiltin(Puts));
    }

    private static IMonkeyObject Len(List<IMonkeyObject> args)
    {
        if (args.Count != 1)
            return new MonkeyError($"Wrong number of arguments. Got {args.Count}, want 1");
        return args[0] switch
        {
            MonkeyString s => new MonkeyInteger(s.Value.Length),
            MonkeyArray a => new MonkeyInteger(a.Elements.Count),
            _ => new MonkeyError($"Argument to 'len' not supported. Got {args[0].Type}")
        };
    }

    private static IMonkeyObject First(List<IMonkeyObject> args)
    {
        if (args.Count != 1)
            return new MonkeyError($"Wrong number of arguments. Got {args.Count}, want 1");
        if (args[0] is MonkeyArray arr)
            return arr.Elements.Count > 0 ? arr.Elements[0] : Evaluator.Null;
        return new MonkeyError($"Argument to 'first' must be Array. Got {args[0].Type}");
    }

    private static IMonkeyObject Last(List<IMonkeyObject> args)
    {
        if (args.Count != 1)
            return new MonkeyError($"Wrong number of arguments. Got {args.Count}, want 1");
        if (args[0] is not MonkeyArray arr)
            return new MonkeyError($"Argument to 'last' must be Array. Got {args[0].Type}");
        var length = arr.Elements.Count;
        return length > 0 ? arr.Elements[length - 1] : Evaluator.Null;
    }

    private static IMonkeyObject Rest(List<IMonkeyObject> args)
    {
        if (args.Count != 1)
            return new MonkeyError($"Wrong number of arguments. Got {args.Count}, want 1");
        if (args[0] is not MonkeyArray arr)
            return new MonkeyError($"Argument to 'last' must be Array. Got {args[0].Type}");
        var length = arr.Elements.Count;
        if (length > 0)
            return new MonkeyArray(arr.Elements.Skip(1).ToList());
        return Evaluator.Null;
    }

    private static IMonkeyObject Push(List<IMonkeyObject> args)
    {
        if (args.Count != 2)
            return new MonkeyError($"Wrong number of arguments. Got {args.Count}, want 2");
        if (args[0] is not MonkeyArray arr)
            return new MonkeyError($"Argument to 'push' must be Array. Got {args[0].Type}");
        var newElements = arr.Elements.Skip(0).ToList();
        newElements.Add(args[1]);
        return new MonkeyArray(newElements);
    }

    private static IMonkeyObject Puts(List<IMonkeyObject> args)
    {
        foreach (var arg in args)
            Console.WriteLine(arg.Inspect());
        return Evaluator.Null;
    }
}