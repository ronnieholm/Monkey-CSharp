using System;
using System.Collections.Generic;
using System.Text;

// This file holds the types of objects which may be produced by evaluation of
// AST nodes. The objects are all fairly simple because we use the host language
// to represent such objects as interger, string, array, and hashmap in Monkey.

namespace Monkey.Core
{
    using BuiltinFunction = Func<List<IMonkeyObject>, IMonkeyObject>;

    // Called Object in the book, but renamed to MonkeyObject so it's not
    // confused with System.Object.
    public interface IMonkeyObject
    {
        ObjectType Type { get; }
        string Inspect();
    }

    public interface IHashable
    {
        HashKey HashKey();
    }

    // For each IMonkeyObject derivative we could call Object.GetType() to
    // return the .NET type, thereby getting rid of the Type get property of
    // IMonkeyObject. This would render this enum redundant. Monkey error
    // messages, however, contain members of this enum and enum members are used
    // in type comparison (we could've opted for the .NET types there). Rather
    // than leaking implementation type names into user visible error messages
    // we keep the Monkey types and .NET types separate.
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

    // Making it a struct over a class makes it readily usable as a key within a
    // dictionary as well as directly comparable within tests using
    // Assert.Equals and the like (both type and value is used to generator is
    // used as part of key for Dictionary<,>).
    public struct HashKey
    {
        // Scopes the hash to a particular object type.
        public ObjectType Type { get; set; }

        // Because hash key is an integer, we can compare keys using the ==
        // operator.
        public ulong Value { get; set; }
    }

    public class MonkeyInteger : IMonkeyObject, IHashable
    {
        public ObjectType Type { get => ObjectType.Integer; }
        public long Value { get; set; }
        public string Inspect() => Value.ToString();

        public HashKey HashKey()
        {
            return new HashKey { Type = Type, Value = (ulong)Value };
        }
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

    // MNull is a type just like MInteger and MBoolean except that it
    // doesn't wrap any value. It represents the absence of any value.
    public class MonkeyNull : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.Null; }
        public string Inspect() => "null";
    }

    // This Monkey object is a wrapper around another Monkey object.
    public class MonkeyReturnValue : IMonkeyObject
    {
        public ObjectType Type { get => ObjectType.ReturnValue; }
        public IMonkeyObject Value { get; set; }
        public string Inspect() => Value.Inspect();
    }

    // MError is a simple class which wraps a string to serve as the error
    // message. In a production language, we'd want to attach a stack trace
    // and line and column number to such an error object as well.
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

        // Functions in Monkey carry their own environment with them. This
        // allows for closures which "close over" the environment they're
        // defined in and means the function can later access it.
        public MonkeyEnvironment Env { get; set; }

        public string Inspect()
        {
            var sb = new StringBuilder();
            var parameters = new List<string>();
            foreach (var p in Parameters)
            {
                parameters.Add(p.String);                
            }

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
            {
                elements.Add(e.Inspect());
            }

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
            {
                pairs.Add($"{kv.Value.Key.Inspect()}: {kv.Value.Value.Inspect()}");
            }

            sb.Append("{");
            sb.Append(string.Join(", ", pairs));
            sb.Append("}");
            return sb.ToString();            
        }
    }
}