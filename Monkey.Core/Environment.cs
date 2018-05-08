using System;
using System.Collections.Generic;

namespace Monkey.Core
{
    // We call the class Monkey Environment to not get confused by the existing
    // System.Environment.
    public class MonkeyEnvironment
    {
        public Dictionary<string, IMonkeyObject> Store { get; set; }
        public MonkeyEnvironment Outer { get; set; }

        public MonkeyEnvironment()
        {
            Store = new Dictionary<string, IMonkeyObject>();
        }

        public static MonkeyEnvironment NewEnvironment()
        {
            return new MonkeyEnvironment { Store = new Dictionary<string, IMonkeyObject>(), Outer = null };
        }

        public static MonkeyEnvironment NewEnclosedEnvironment(MonkeyEnvironment outer)
        {
            var env = NewEnvironment();
            env.Outer = outer;
            return env;
        }

        public (IMonkeyObject, bool) Get(string name)
        {
            IMonkeyObject value;
            var ok = Store.TryGetValue(name, out value);

            // If the current environment doesn't have a value associated with
            // the given name, recursively call Get of the enclosing environment
            // (that the current environment is extending) until either name is
            // found or we can issue a "ERROR: unknown identifier: foobar"
            // message.
            if (!ok && Outer != null)
            {
                return Outer.Get(name);
            }

            return (value, ok);
        }

        public IMonkeyObject Set(string name, IMonkeyObject val)
        {
            Store[name] = val;
            return val;
        }
    }
}