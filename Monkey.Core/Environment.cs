using System.Collections.Generic;

namespace Monkey.Core
{
    // We call it MonkeyEnvironment to avoid confusion with System.Environment.
    public class MonkeyEnvironment
    {
        private Dictionary<string, IMonkeyObject> Store { get; set; }
        private MonkeyEnvironment? Outer { get; set; }

        public MonkeyEnvironment() => 
            Store = new Dictionary<string, IMonkeyObject>();

        private static MonkeyEnvironment NewEnvironment() =>
            new MonkeyEnvironment { Store = new Dictionary<string, IMonkeyObject>(), Outer = null };

        public static MonkeyEnvironment NewEnclosedEnvironment(MonkeyEnvironment outer)
        {
            var env = NewEnvironment();
            env.Outer = outer;
            return env;
        }

        // TODO: Why return a tuple and not simply null if not found?
        public (IMonkeyObject, bool) Get(string name)
        {
            var ok = Store.TryGetValue(name, out IMonkeyObject value);

            // If current environment doesn't have a value associated with a
            // name, we recursively call Get on enclosing environment (which the
            // current environment is extending) until either name is found or
            // caller can issue a "ERROR: Unknown identifier: foobar" error.
            if (!ok && Outer != null)
                return Outer.Get(name);
            return (value, ok);
        }

        public IMonkeyObject Set(string name, IMonkeyObject val)
        {
            Store[name] = val;
            return val;
        }
    }
}