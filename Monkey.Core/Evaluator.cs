using System;
using System.Collections.Generic;

// An alternative, perhaps more object oriented, approach to evaluation would be
// adding an "IMonkeyObject Eval(Evaluator e)" method to each AST node. The
// Evaluator would make shared state available to each AST node through the
// Evaluator argument. The Evaluator would kick of evaluation by looping through
// each statement, dynamically dispatching to each AST's Eval() method, and so
// would would the Eval() method on each AST node. For an example of this
// approach, see Browser hacking: Let's build a JavaScript engine for
// SerenityOS! (https://www.youtube.com/watch?v=byNwCHc_IIM).

namespace Monkey.Core
{
    public static class Evaluator
    {
        // As there's only ever a need for a single instance of each of these
        // values, we optimize by pre-creating instances to return during
        // evaluation.
        public static readonly MonkeyBoolean True = new() { Value = true };
        public static readonly MonkeyBoolean False = new() { Value = false };
        public static readonly MonkeyNull Null = new();

        public static IMonkeyObject Eval(INode node, MonkeyEnvironment env)
        {
            switch (node)
            {
                // Statements
                case Program p:
                    return EvalProgram(p.Statements, env);
                case ExpressionStatement es:
                    return Eval(es.Expression, env);
                case BlockStatement bs:
                    return EvalBlockStatement(bs.Statements, env);
                case ReturnStatement rs:
                {
                    var val = Eval(rs.ReturnValue, env);

                    // Check for errors whenever Eval is called inside Eval in
                    // order to stop errors from being passed around and
                    // bubbling up far from their origin.
                    return IsError(val) ? val : new MonkeyReturnValue(val);
                }
                case LetStatement ls:
                {
                    var val = Eval(ls.Value, env);
                    return IsError(val) ? val : env.Set(ls.Name.Value, val);
                }

                // Expressions
                case IntegerLiteral il:
                    return new MonkeyInteger(il.Value);
                case Boolean_ b:
                    return NativeBoolToBooleanObject(b.Value);
                case PrefixExpression pe:
                {
                    var right = Eval(pe.Right, env);
                    return IsError(right) ? right : EvalPrefixExpression(pe.Operator, right);
                }
                case InfixExpression ie:
                {
                    var left = Eval(ie.Left, env);
                    if (IsError(left))
                        return left;
                    var right = Eval(ie.Right, env);
                    return IsError(right) ? right : EvalInfixExpression(ie.Operator, left, right);
                }
                case IfExpression ife:
                    return EvalIfExpression(ife, env);
                case Identifier i:
                    return EvalIdentifier(i, env);
                case FunctionLiteral fl:
                    return new MonkeyFunction(fl.Parameters, fl.Body, env);
                case CallExpression ce:
                {
                    var function = Eval(ce.Function, env);
                    if (IsError(function))
                        return function;

                    var args = EvalExpressions(ce.Arguments, env);
                    if (args.Count == 1 && IsError(args[0]))
                        return args[0];
                    return ApplyFunction(function, args);
                }
                case ArrayLiteral al:
                {
                    var elements = EvalExpressions(al.Elements, env);
                    if (elements.Count == 1 && IsError(elements[0]))
                        return elements[0];
                    return new MonkeyArray(elements);
                }
                case IndexExpression ide:
                {
                    var left = Eval(ide.Left, env);
                    if (IsError(left))
                        return left;

                    var index = Eval(ide.Index, env);
                    return IsError(index) ? index : EvalIndexExpression(left, index);
                }
                case StringLiteral sl:
                    return new MonkeyString(sl.Value);
                case HashLiteral hl:
                    return EvalHashLiteral(hl, env);
                default:
                    throw new Exception($"Invalid node type: {node.GetType()}");
            }
        }

        private static IMonkeyObject EvalProgram(List<Statement> statements, MonkeyEnvironment env)
        {
            IMonkeyObject result = Null;
            foreach (var stmt in statements)
            {
                result = Eval(stmt, env);

                // Prevents further evaluation if the result of the evaluation
                // is a return statement. Note how we don't return MReturnValue
                // directly, but unwrap its value. MReturnValue is an internal
                // detail to allow Eval() to signal to its caller that it
                // encountered and evaluated a return statement.
                if (result is MonkeyReturnValue rv)
                    return rv.Value;
                if (result is MonkeyError e)
                    return e;
            }
            return result;
        }

        private static IMonkeyObject EvalBlockStatement(List<Statement> statements, MonkeyEnvironment env)
        {
            IMonkeyObject result = Null;
            foreach (var stmt in statements)
            {
                result = Eval(stmt, env);
                var rt = result.Type;
                if (rt == ObjectType.ReturnValue || rt == ObjectType.Error)
                {
                    // Compared to EvalProgram(), we don't unwrap the return
                    // value. Instead when an ReturnValue is encountered as the
                    // result of evaluating a statement, we return it to
                    // EvalProgram() for unwrapping. This halts outer block
                    // evaluation and bubbles up the result.
                    return result;
                }
            }
            return result;
        }

        private static IMonkeyObject EvalPrefixExpression(string op, IMonkeyObject right)
        {
            return op switch
            {
                "!" => EvalBangOperatorExpression(right),
                "-" => EvalMinusPrefixOperatorExpression(right),
                _ => new MonkeyError($"Unknown operator: {op}{right.Type}")
            };
        }

        private static IMonkeyObject EvalBangOperatorExpression(IMonkeyObject right)
        {
            if (right == True)
                return False;
            if (right == False)
                return True;
            if (right == Null)
                return True;
            return False;
        }

        private static IMonkeyObject EvalMinusPrefixOperatorExpression(IMonkeyObject right)
        {
            if (right.Type != ObjectType.Integer)
                return new MonkeyError($"Unknown operator: -{right.Type}");
            var value = ((MonkeyInteger)right).Value;
            return new MonkeyInteger(-value);
        }

        private static IMonkeyObject EvalInfixExpression(string op, IMonkeyObject left, IMonkeyObject right)
        {
            if (left.Type == ObjectType.Integer && right.Type == ObjectType.Integer)
                return EvalIntegerInfixExpression(op, left, right);
            if (left.Type == ObjectType.String && right.Type == ObjectType.String)
                return EvalStringInfixExpression(op, left, right);
            // For MonkeyBooleans we can use reference comparison to check for
            // equality. It works because of our singleton True and False
            // instances but wouldn't work for MonkeyIntegers since they aren't
            // singletons. 5 == 5 would be false when comparing references. To
            // compare MonkeyIntegers we must unwrap the integer stored inside
            // each MonkeyInteger object and compare their values.
            if (op == "==")
                return NativeBoolToBooleanObject(left == right);
            if (op == "!=")
                return NativeBoolToBooleanObject(left != right);
            if (left.Type != right.Type)
                return new MonkeyError($"Type mismatch: {left.Type} {op} {right.Type}");
            return new MonkeyError($"Unknown operator: {left.Type} {op} {right.Type}");
        }

        private static IMonkeyObject EvalIntegerInfixExpression(string op, IMonkeyObject left, IMonkeyObject right)
        {
            var leftVal = ((MonkeyInteger)left).Value;
            var rightVal = ((MonkeyInteger)right).Value;

            return op switch
            {
                "+" => new MonkeyInteger(leftVal + rightVal),
                "-" => new MonkeyInteger(leftVal - rightVal),
                "*" => new MonkeyInteger(leftVal * rightVal),
                "/" => new MonkeyInteger(leftVal / rightVal),
                "<" => NativeBoolToBooleanObject(leftVal < rightVal),
                ">" => NativeBoolToBooleanObject(leftVal > rightVal),
                "==" => NativeBoolToBooleanObject(leftVal == rightVal),
                "!=" => NativeBoolToBooleanObject(leftVal != rightVal),
                _ => new MonkeyError($"Unknown operator: {left.Type} {op} {right.Type}")
            };
        }

        private static IMonkeyObject EvalStringInfixExpression(string op, IMonkeyObject left, IMonkeyObject right)
        {
            if (op != "+")
                return new MonkeyError($"Unknown operator: {left.Type} {op} {right.Type}");

            var leftVal = ((MonkeyString)left).Value;
            var rightVal = ((MonkeyString)right).Value;
            return new MonkeyString(leftVal + rightVal);
        }

        private static IMonkeyObject EvalIfExpression(IfExpression ie, MonkeyEnvironment env)
        {
            var condition = Eval(ie.Condition, env);
            if (IsError(condition))
                return condition;
            if (IsTruthy(condition))
                return Eval(ie.Consequence, env);
            if (ie.Alternative != null)
                return Eval(ie.Alternative, env);
            return Null;
        }

        private static bool IsTruthy(IMonkeyObject obj)
        {
            if (obj == Null)
                return false;
            if (obj == True)
                return true;
            if (obj == False)
                return false;
            return true;
        }

        private static MonkeyBoolean NativeBoolToBooleanObject(bool value) => value ? True : False;

        private static bool IsError(IMonkeyObject obj) => obj.Type == ObjectType.Error;

        private static IMonkeyObject EvalIdentifier(Identifier node, MonkeyEnvironment env)
        {
            var (val, inCurrentEnvironment) = env.Get(node.Value);
            if (inCurrentEnvironment)
                return val;

            var inBuiltinEnvironment = MonkeyBuiltins.Builtins.TryGetValue(node.Value, out MonkeyBuiltin fn);
            if (inBuiltinEnvironment)
                return fn;
            return new MonkeyError($"Identifier not found: {node.Value}");
        }

        private static List<IMonkeyObject> EvalExpressions(List<Expression> exps, MonkeyEnvironment env)
        {
            var result = new List<IMonkeyObject>();

            // By definition arguments are evaluated left to right. Since the
            // side effect of evaluating one argument might be relied on during
            // evaluation of the next, defining an explicit evaluation order is
            // important.
            foreach (var e in exps)
            {
                var evaluated = Eval(e, env);
                if (IsError(evaluated))
                    return new List<IMonkeyObject> { evaluated };
                result.Add(evaluated);
            }
            return result;
        }

        private static IMonkeyObject ApplyFunction(IMonkeyObject fn, List<IMonkeyObject> args)
        {
            if (fn is MonkeyFunction f)
            {
                var extendedEnv = ExtendFunctionEnv(f, args);
                var evaluated = Eval(f.Body, extendedEnv);
                return UnwrapReturnValue(evaluated);
            }
            if (fn is MonkeyBuiltin b)
                return b.Fn(args);
            return new MonkeyError($"Not a function: {fn.Type}");
        }

        private static MonkeyEnvironment ExtendFunctionEnv(MonkeyFunction fn, List<IMonkeyObject> args)
        {
            var env = MonkeyEnvironment.NewEnclosedEnvironment(fn.Env);
            for (var i = 0; i < fn.Parameters.Count; i++)
                env.Set(fn.Parameters[i].Value, args[i]);
            return env;
        }

        private static IMonkeyObject UnwrapReturnValue(IMonkeyObject obj)
        {
            // Unwrapping prevents a return statement from bubbling up through
            // several functions and stopping evaluation in all of them. We only
            // want to stop the evaluation of the last called function's body.
            // Otherwise, EvalBlockStatement() would stop evaluating statements
            // in outer functions.
            if (obj is MonkeyReturnValue rv)
                return rv.Value;
            return obj;
        }

        private static IMonkeyObject EvalIndexExpression(IMonkeyObject left, IMonkeyObject index)
        {
            return left.Type switch
            {
                ObjectType.Array when index.Type == ObjectType.Integer => EvalArrayIndexExpression(left, index),
                ObjectType.Hash => EvalHashIndexExpression(left, index),
                _ => new MonkeyError($"Index operator not supported {left.Type}")
            };
        }

        private static IMonkeyObject EvalArrayIndexExpression(IMonkeyObject array, IMonkeyObject index)
        {
            var arrayObject = (MonkeyArray)array;
            var idx = ((MonkeyInteger)index).Value;
            var max = arrayObject.Elements.Count - 1;

            if (idx < 0 || idx > max)
            {
                // Some languages throw an exception when the index is out of
                // bounds. In Monkey by definition we return Null as the result.
                return Null;
            }
            return arrayObject.Elements[(int)idx];
        }

        private static IMonkeyObject EvalHashIndexExpression(IMonkeyObject hash, IMonkeyObject index)
        {
            var hashObject = (MonkeyHash)hash;
            if (index is IHashable key)
            {
                var found = hashObject.Pairs.TryGetValue(key.HashKey(), out HashPair pair);
                if (!found)
                    return Null;
                return pair.Value;
            }
            return new MonkeyError($"Unusable as hash key: {index.Type}");
        }

        private static IMonkeyObject EvalHashLiteral(HashLiteral node, MonkeyEnvironment env)
        {
            var pairs = new Dictionary<HashKey, HashPair>();
            foreach (var kv in node.Pairs)
            {
                var key = Eval(kv.Key, env);
                if (IsError(key))
                    return key;
                if (key is IHashable k)
                {
                    var value = Eval(kv.Value, env);
                    if (IsError(value))
                        return value;

                    var hashKey = k.HashKey();
                    var hashPair = new HashPair(key, value);
                    pairs.Add(hashKey, hashPair);
                }
                else
                    return new MonkeyError($"Unusable as hash key: {key.GetType()}");
            }
            return new MonkeyHash(pairs);
        }
    }
}
