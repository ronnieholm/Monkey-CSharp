using System;
using System.Collections.Generic;

namespace Monkey.Core
{
    public class Evaluator
    {
        // There's only ever a need for one instance of these values so as an
        // optimization we create a single instance of each to return during
        // evaluation.
        public static MonkeyBoolean True = new MonkeyBoolean { Value = true };
        public static MonkeyBoolean False = new MonkeyBoolean { Value = false };
        public static MonkeyNull Null = new MonkeyNull();

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
                    if (IsError(val))
                    {
                        return val;
                    }
                    return new MonkeyReturnValue { Value = val };
                }
                case LetStatement ls:
                {
                    var val = Eval(ls.Value, env);
                    if (IsError(val))
                    {
                        return val;
                    }
                    return env.Set(ls.Name.Value, val);
                }

                // Expressions
                case IntegerLiteral il:
                    return new MonkeyInteger { Value = il.Value };
                case Boolean_ b:
                    return NativeBoolToBooleanObject(b.Value);
                case PrefixExpression pe:
                {
                    var right = Eval(pe.Right, env);
                    if (IsError(right))
                    {
                        return right;
                    }
                    return EvalPrefixExpression(pe.Operator, right);
                }
                case InfixExpression ie:
                {
                    var left = Eval(ie.Left, env);
                    if (IsError(left))
                    {
                        return left;
                    }

                    var right = Eval(ie.Right, env);
                    if (IsError(right))
                    {
                        return right;
                    }

                    return EvalInfixExpression(ie.Operator, left, right);
                }
                case IfExpression ife:
                    return EvalIfExpression(ife, env);
                case Identifier i:
                    return EvalIdentifier(i, env);
                case FunctionLiteral fl:
                {
                    var parameters = fl.Parameters;   
                    var body = fl.Body;
                    return new MonkeyFunction { Parameters = parameters, Env = env, Body = fl.Body };
                }
                case CallExpression ce:
                {
                    var function = Eval(ce.Function, env);
                    if (IsError(function))
                    {
                        return function;
                    }
                    
                    var args = EvalExpressions(ce.Arguments, env);
                    if (args.Count == 1 && IsError(args[0]))
                    {
                        return args[0];
                    }

                    return ApplyFunction(function, args);
                }         
                case ArrayLiteral al:
                {
                    var elements = EvalExpressions(al.Elements, env);
                    if (elements.Count == 1 && IsError(elements[0]))
                    {
                        return elements[0];
                    }
                    return new MonkeyArray { Elements = elements };
                }       
                case IndexExpression ide:
                {
                    var left = Eval(ide.Left, env);
                    if (IsError(left))
                    {
                        return left;
                    }

                    var index = Eval(ide.Index, env);
                    if (IsError(index))
                    {
                        return index;
                    }

                    return EvalIndexExpression(left, index);
                }
                case StringLiteral sl:
                    return new MonkeyString { Value = sl.Value };
                case HashLiteral hl:
                    return EvalHashLiteral(hl, env);
                default:
                    throw new Exception($"Invalid node type: {node.GetType()}");                
            }
        }

        // Helper used within Evaluator and MonkeyBuiltins which is why it's
        // public and static.
        public static MonkeyError NewError(string message) => new MonkeyError { Message = message };

        private static IMonkeyObject EvalProgram(List<IStatement> statements, MonkeyEnvironment env)
        {
            IMonkeyObject result = null;
            foreach (var stmt in statements)
            {
                result = Eval(stmt, env);

                // Prevents further evaluation if the result of the evaluation
                // is a return statement. Note how we don't return MReturnValue
                // directly, but unwraps its value. The MReturnValue is an
                // internal detail to allow Eval() to signal to its caller that
                // it encountered and evaluated a return statement.
                if (result is MonkeyReturnValue rv)
                {
                    return rv.Value;
                }
                else if (result is MonkeyError e)
                {
                    return e;
                }
            }
            return result;
        }

        private static IMonkeyObject EvalBlockStatement(List<IStatement> statements, MonkeyEnvironment env)
        {
            IMonkeyObject result = null;
            foreach (var stmt in statements)
            {
                result = Eval(stmt, env);
                var rt = result.Type;
                if (rt == ObjectType.ReturnValue || rt == ObjectType.Error)
                {
                    // Compared to EvalProgram()), we don't unwrap the return
                    // value. Instead when an ReturnValueObj is encountered as
                    // the result of evaluating a statement, we return it to
                    // EvalProgram() for unwrapping. This makes evaluation stop
                    // in a possibly outer block statement and bubbles up the
                    // result.
                    return result;
                }
            }
            return result;
        }

        private static IMonkeyObject EvalPrefixExpression(string op, IMonkeyObject right)
        {
            switch (op)
            {
                case "!": 
                    return EvalBangOperatorExpression(right);
                case "-":
                    return EvalMinusPrefixOperatorExpression(right);
                default: 
                    return NewError($"Unknown operator: {op}{right.Type}");
            }
        }

        private static IMonkeyObject EvalBangOperatorExpression(IMonkeyObject right)
        {
            if (right == True)
            {
                return False;
            }
            else if (right == False)
            {
                return True;
            }
            else if (right == Null)
            {
                return True;
            }

            return False;
        }

        private static IMonkeyObject EvalMinusPrefixOperatorExpression(IMonkeyObject right)
        {
            if (right.Type != ObjectType.Integer)
            {
                return NewError($"Unknown operator: -{right.Type}");
            }

            var value = ((MonkeyInteger)right).Value;
            return new MonkeyInteger { Value = -value };
        }

        private static IMonkeyObject EvalInfixExpression(string op, IMonkeyObject left, IMonkeyObject right)
        {
            if (left.Type == ObjectType.Integer && right.Type == ObjectType.Integer)
            {
                return EvalIntegerInfixExpression(op, left, right);
            }
            else if (left.Type == ObjectType.String && right.Type == ObjectType.String)
            {
                return EvalStringInfixExpression(op, left, right);
            }
            // Observe how for MonkeyBooleans we use reference comparison to
            // check for equality. This works only because the references are to
            // our singleton True and False instances. This wouldn't work for
            // MonkeyIntegers since those aren't singletons. 5 == 5 would be
            // false with reference equals. To compare MonkeyIntegers we must
            // unwrap the integers stored inside the MonkeyIntegers and compare
            // their values.
            else if (op == "==")
            {
                return NativeBoolToBooleanObject(left == right);
            }
            else if (op == "!=")
            {
                return NativeBoolToBooleanObject(left != right);
            }
            else if (left.Type != right.Type)
            {
                return NewError($"Type mismatch: {left.Type} {op} {right.Type}");
            }

            return NewError($"Unknown operator: {left.Type} {op} {right.Type}");
        }

        private static IMonkeyObject EvalIntegerInfixExpression(string op, IMonkeyObject left, IMonkeyObject right)
        {
            var leftVal = ((MonkeyInteger)left).Value;
            var rightVal = ((MonkeyInteger)right).Value;
            
            switch (op)
            {
                case "+":
                    return new MonkeyInteger { Value = leftVal + rightVal };
                case "-":
                    return new MonkeyInteger { Value = leftVal - rightVal };
                case "*":
                    return new MonkeyInteger { Value = leftVal * rightVal };
                case "/":
                    return new MonkeyInteger { Value = leftVal / rightVal };
                case "<":
                    return NativeBoolToBooleanObject(leftVal < rightVal);
                case ">":
                    return NativeBoolToBooleanObject(leftVal > rightVal);
                case "==":
                    return NativeBoolToBooleanObject(leftVal == rightVal);
                case "!=":
                    return NativeBoolToBooleanObject(leftVal != rightVal);
                default:
                    return NewError($"Unknown operator: {left.Type} {op} {right.Type}");
            }
        }

        private static IMonkeyObject EvalStringInfixExpression(string op, IMonkeyObject left, IMonkeyObject right)
        {
            if (op != "+")
            {
                return NewError($"Unknown operator: {left.Type} {op} {right.Type}");
            }

            var leftVal = ((MonkeyString)left).Value;
            var rightVal = ((MonkeyString)right).Value;          
            return new MonkeyString { Value = leftVal + rightVal };
        }

        private static IMonkeyObject EvalIfExpression(IfExpression ie, MonkeyEnvironment env)
        {
            var condition = Eval(ie.Condition, env);
            if (IsError(condition))
            {
                return condition;
            }

            if (IsTruthy(condition))
            {
                return Eval(ie.Consequence, env);
            }
            else if (ie.Alternative != null)
            {
                return Eval(ie.Alternative, env);
            }

            return Null;
        }

        private static bool IsTruthy(IMonkeyObject obj)
        {
            if (obj == Null)
            {
                return false;
            }
            else if (obj == True)
            {
                return true;
            }
            else if (obj == False)
            {
                return false;
            }

            return true;
        } 

        private static MonkeyBoolean NativeBoolToBooleanObject(bool input) => input ? True : False;

        private static bool IsError(IMonkeyObject obj) => 
            obj != null ? obj.Type == ObjectType.Error : false;

        private static IMonkeyObject EvalIdentifier(Identifier node, MonkeyEnvironment env)
        {
            var (val, inCurrentEnvironment) = env.Get(node.Value);
            if (inCurrentEnvironment)
            {
                return val;
            }

            MonkeyBuiltin fn;
            var inBuiltinEnvironment = MonkeyBuiltins.Builtins.TryGetValue(node.Value, out fn);
            if (inBuiltinEnvironment)
            {
                return fn;
            }

            return NewError($"Identifier not found: {node.Value}");
        }

        private static List<IMonkeyObject> EvalExpressions(List<IExpression> exps, MonkeyEnvironment env)
        {
            var result = new List<IMonkeyObject>();

            // Observe how, by definition, the arguments are evaluated left to
            // right. Since the side effect of evaluating one argument might be
            // relied on during the evaluation of the next, defining an explicit
            // evaluation order is important.
            foreach (var e in exps)
            {
                var evaluated = Eval(e, env);
                if (IsError(evaluated))
                {
                    return new List<IMonkeyObject> { evaluated };
                }
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
            else if (fn is MonkeyBuiltin b)
            {
                return b.Fn(args);
            }
            else
            {
                return NewError($"Not a function: {fn.Type}");
            }
        }

        private static MonkeyEnvironment ExtendFunctionEnv(MonkeyFunction fn, List<IMonkeyObject> args)
        {
            var env = MonkeyEnvironment.NewEnclosedEnvironment(fn.Env);
            for (var i = 0; i < fn.Parameters.Count; i++)
            {
                env.Set(fn.Parameters[i].Value, args[i]);
            }

            return env;
        }

        private static IMonkeyObject UnwrapReturnValue(IMonkeyObject obj)
        {
            // Unwrapping is necessary because otherwise a return statement
            // would bubble up through several functions and stop the evaluation
            // in all of them. We only want to stop the evaluation of the last
            // called function's body. Otherwise, EvalBlockStatement() would
            // stop evaluating statements in the "outer" functions.
            if (obj is MonkeyReturnValue rv)
            {
                return rv.Value;
            }
            return obj;
        }

        private static IMonkeyObject EvalIndexExpression(IMonkeyObject left, IMonkeyObject index)
        {
            if (left.Type == ObjectType.Array && index.Type == ObjectType.Integer)
            {
                return EvalArrayIndexExpression(left, index);
            }
            else if (left.Type == ObjectType.Hash)
            {
                return EvalHashIndexExpression(left, index);
            }
            return NewError($"Index operator not supported {left.Type}");
        }

        private static IMonkeyObject EvalArrayIndexExpression(IMonkeyObject array, IMonkeyObject index)
        {
            var arrayObject = (MonkeyArray)array;
            var idx = ((MonkeyInteger)index).Value;
            var max = arrayObject.Elements.Count - 1;

            if (idx < 0 || idx > max)
            {
                // Some languages throw an exception when the index is out of
                // bounds. In Monkey by definition we return null as the result.
                return Null;
            }            
            return arrayObject.Elements[(int)idx];
        }

        public static IMonkeyObject EvalHashIndexExpression(IMonkeyObject hash, IMonkeyObject index)
        {
            var hashObject = (MonkeyHash)hash;
            if (index is IHashable key)
            {
                HashPair pair;
                var ok = hashObject.Pairs.TryGetValue(key.HashKey(), out pair);
                if (!ok)
                {
                    return Null;
                }

                return pair.Value;
            }
            else
            {
                return NewError($"Unusable as hash key: {index.Type}");
            };
        }

        private static IMonkeyObject EvalHashLiteral(HashLiteral node, MonkeyEnvironment env)
        {
            var pairs = new Dictionary<HashKey, HashPair>();

            foreach (var kv in node.Pairs)
            {
                var key = Eval(kv.Key, env);
                if (IsError(key))
                {
                    return key;
                }

                if (key is IHashable k)
                {
                    var value = Eval(kv.Value, env);
                    if (IsError(value))
                    {
                        return value;
                    }

                    var hashKey = k.HashKey();
                    var hashPair = new HashPair { Key = key, Value = value };
                    pairs.Add(hashKey, hashPair);
                }
                else
                {
                    return NewError($"Unusable as hash key: {key.GetType()}");
                }
            }

            return new MonkeyHash { Pairs = pairs };
        }
    }
}