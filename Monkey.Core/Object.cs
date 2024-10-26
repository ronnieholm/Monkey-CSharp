using System;
using System.Collections.Generic;
using System.Linq;

// This file holds the types of MonkeyObjects which may be produced during
// evaluation of Abstract Syntax Tree nodes. The MonkeyObjects are all fairly
// simple because we use the host language to represent such objects as
// integer, string, array, and hash in Monkey.

namespace Monkey.Core;

using BuiltinFunction = Func<List<IMonkeyObject>, IMonkeyObject>;

// Called Object in the book, but renamed to MonkeyObject to not confuse it
// with System.Object.
public interface IMonkeyObject
{
    ObjectType Type { get; }
    string Inspect();
}

public interface IHashable
{
    HashKey HashKey();
}

// Within each IMonkeyObject derived class, we could call Object.GetType()
// to return its .NET type for comparison, thereby getting rid of the Type
// getter property on IMonkeyObject. Relying on Object.GetType() would
// render this enum redundant. Monkey error messages, however, include
// members of this enum in error messages. Relying on Object.GetType(),
// details of the underlying implementation would leak into user error
// messages. So we keep the Monkey types and .NET types separate.
public enum ObjectType
{
    None = 0,
    Integer,
    Boolean,
    Null,
    ReturnValue,
    Error,
    Function,
    String,
    Builtin,
    Array,
    Hash
}

// Making it a struct and not a class makes the type readily usable as the
// key within a dictionary and comparable within tests using Assert.Equals.
// Because both the Type and Value within the struct are both value types,
// they're easily comparable and in combination are used as key for a
// Dictionary<,>.
public record HashKey(
    // Scopes the hash to a particular object type.
    ObjectType Type,

    // Because key is an integer, we can compare keys using the == operator
    // without the need to overload Object.GetHashCode() and
    // Object.Equals().
    ulong Value);

public record MonkeyInteger(long Value) : IMonkeyObject, IHashable
{
    public ObjectType Type => ObjectType.Integer;
    public string Inspect() => Value.ToString();
    public HashKey HashKey() => new(Type, (ulong)Value);
}

public record MonkeyBoolean(bool Value) : IMonkeyObject, IHashable
{
    public ObjectType Type => ObjectType.Boolean;
    public string Inspect() => Value.ToString();
    public HashKey HashKey() => new(Type, (ulong) (Value ? 1 : 0));
}

// MonkeyNull is a type like MonkeyInteger and MonkeyBoolean except it
// doesn't wrap a value. It represents the absence of a value.
public record MonkeyNull : IMonkeyObject
{
    public ObjectType Type => ObjectType.Null;
    public string Inspect() => "null";
}

// MonkeyReturnValue is a wrapper around another Monkey object.
public record MonkeyReturnValue(IMonkeyObject Value) : IMonkeyObject
{
    public ObjectType Type => ObjectType.ReturnValue;
    public string Inspect() => Value.Inspect();
}

// MonkeyError wraps a string error message. In a production language, we'd
// want to attach stack trace and line and column numbers to such error
// object.
public record MonkeyError(string Message) : IMonkeyObject
{
    public ObjectType Type => ObjectType.Error;
    public string Inspect() => $"Error: {Message}";
}

public record MonkeyFunction(List<Identifier> Parameters, BlockStatement Body, MonkeyEnvironment Env) : IMonkeyObject
{
    // Functions carry their own environment. This allows for closures to
    // "close over" the environment they're defined in and allows the
    // function to later access values within the closure.

    public ObjectType Type => ObjectType.Function;

    public string Inspect()
    {
        var parameters = Parameters.Select(p => p.String);
        return $"fn({string.Join(", ", parameters)}) {{\n{Body.String}\n}}";
    }
}

public record MonkeyString(string Value) : IMonkeyObject, IHashable
{
    public ObjectType Type => ObjectType.String;
    public string Inspect() => Value;

    public HashKey HashKey()
    {
        var s1 = Value[..(Value.Length / 2)];
        var s2 = Value[(Value.Length / 2)..];
        var hash = (long)s1.GetHashCode() << 32 | (uint)s2.GetHashCode();
        return new HashKey(Type, (ulong)hash);
    }
}

public record MonkeyBuiltin(BuiltinFunction Fn) : IMonkeyObject
{
    public ObjectType Type => ObjectType.Builtin;
    public string Inspect() => "builtin function";
}

public record MonkeyArray(List<IMonkeyObject> Elements) : IMonkeyObject
{
    public ObjectType Type => ObjectType.Array;

    public string Inspect()
    {
        var elements = Elements.Select(e => e.Inspect());
        return $"[{string.Join(", ", elements)}]";
    }
}

public record HashPair(IMonkeyObject Key, IMonkeyObject Value);

public record MonkeyHash(Dictionary<HashKey, HashPair> Pairs) : IMonkeyObject
{
    public ObjectType Type => ObjectType.Hash;

    public string Inspect()
    {
        var pairs = Pairs.Select(kv => $"{kv.Value.Key.Inspect()}: {kv.Value.Value.Inspect()}");
        return $"{{{string.Join(", ", pairs)}}}";
    }
}