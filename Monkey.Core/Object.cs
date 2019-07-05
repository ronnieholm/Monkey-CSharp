using System;
using System.Collections.Generic;
using System.Text;

// This file holds the types of MonkeyObjects which may be produced during
// evaluation of Abstract Syntax Tree nodes. The MonkeyObjects are all fairly
// simple because we use the host language to represent such objects as
// interger, string, array, and hash in Monkey.

namespace Monkey.Core
{
    using BuiltinFunction = Func<List<IMonkeyObject>, IMonkeyObject>;

    // Called Object in the book, but renamed to MonkeyObject so as to not
    // confuse it with System.Object.
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
    // getter property on IMonkeyObject. Relying on Object.GetType() would also
    // render this enum redundant. Monkey error messages, however, convert
    // members of this enum to strings and include those in error messages.
    // Relying on Object.GetType(), details of the implementation type would
    // leak into user visible error messages. Hence we keep the Monkey types and
    // .NET types separate.
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
    public struct HashKey
    {
        // Scopes the hash to a particular object type.
        public ObjectType Type { get; set; }

        // Because hash key is an integer, we can compare keys using the ==
        // operator without the need to overload Object.GetHashCode() and
        // Object.Equals() with their intricacies.
        public ulong Value { get; set; }
    }

    public class MonkeyInteger : IMonkeyObject, IHashable
    {
        public ObjectType Type { get => ObjectType.Integer; }
        public long Value { get; set; }
        public string Inspect() => Value.ToString();

        public HashKey HashKey() =>
            new HashKey { Type = Type, Value = (ulong)Value };
    }

    public class MonkeyBoolean : IMonkeyObject, IHashable
    {
        public ObjectType Type { get => ObjectType.Boolean; }
        public bool Value { get; set; }
        public string Inspect() => Value.ToString();

        public HashKey HashKey()
        {
            var value = Value ? 1 : 0;
            return new HashKey { Type = Type, Value = (ulong)value };
        }
    }

    // MonkeyNull is a type just like MonkeyInteger and MonkeyBoolean except it
    // doesn't wrap any value. It represents the absence of any value.
    public class MonkeyNull : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Null; }
        public string Inspect() => "null";
    }

    // MonkeyReturnValue is a wrapper around another Monkey object.
    public class MonkeyReturnValue : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.ReturnValue; }
        public IMonkeyObject Value { get; set; }
        public string Inspect() => Value.Inspect();
    }

    // MonkeyError is a simple class which wraps a string error message. In a
    // production language, we'd want to attach stack trace and line and column
    // number to such error object.
    public class MonkeyError : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Error; }
        public string Message;
        public string Inspect() => $"Error: {Message}";
    }

    public class MonkeyFunction : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Function; }

        public List<Identifier> Parameters { get; set; }
        public BlockStatement Body { get; set; }

        // Functions in Monkey carry their own environment. This allows for
        // closures which "close over" the environment they're defined in and
        // allows the function to later access values within the closure.
        public MonkeyEnvironment Env { get; set; }

        public string Inspect()
        {
            var sb = new StringBuilder();
            var parameters = new List<string>();
            foreach (var p in Parameters)
                parameters.Add(p.String);                

            sb.Append("fn");
            sb.Append("(");
            sb.Append(string.Join(", ", parameters));
            sb.Append(") {\n");
            sb.Append(Body.String);
            sb.Append("\n}");
            return sb.ToString();
        }
    }

    public class MonkeyString : IMonkeyObject, IHashable
    {
        public string Value { get; set; }
        public string Inspect() => Value;
        public ObjectType Type { get => ObjectType.String; }

        public HashKey HashKey()
        {
            var s1 = Value.Substring(0, Value.Length / 2);
            var s2 = Value.Substring(Value.Length / 2);
            var hash = ((long)s1.GetHashCode()) << 32 | (long)s2.GetHashCode();
            return new HashKey { Type = Type, Value = (ulong)hash };
        }
    }

    public class MonkeyBuiltin : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Builtin; }
        public BuiltinFunction Fn { get; set; }
        public string Inspect() => "builtin function";
    }

    public class MonkeyArray : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Array; }

        public List<IMonkeyObject> Elements { get; set; }

        public string Inspect()
        {
            var sb = new StringBuilder();
            var elements = new List<string>();

            foreach (var e in Elements)
                elements.Add(e.Inspect());

            sb.Append("[");
            sb.Append(string.Join(", ", elements));
            sb.Append("]");
            return sb.ToString();
        }
    }

    public class HashPair
    {
        public IMonkeyObject Key { get; set; }
        public IMonkeyObject Value { get; set; }
    }

    public class MonkeyHash : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Hash; }

        public Dictionary<HashKey, HashPair> Pairs { get; set; }

        public string Inspect()
        {
            var sb = new StringBuilder();
            var pairs = new List<string>();

            foreach (var kv in Pairs)
                pairs.Add($"{kv.Value.Key.Inspect()}: {kv.Value.Value.Inspect()}");

            sb.Append("{");
            sb.Append(string.Join(", ", pairs));
            sb.Append("}");
            return sb.ToString();            
        }
    }
}