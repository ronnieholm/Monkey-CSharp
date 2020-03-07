using System.Collections.Generic;
using Xunit;
using Monkey.Core;

namespace Monkey.Tests
{
    public class EvaluatorTests
    {
        [Theory]
        [InlineData("5", 5)]
        [InlineData("10", 10)]
        [InlineData("-5", -5)]
        [InlineData("-10", -10)]
        [InlineData("5 + 5 + 5 + 5 - 10", 10)]
        [InlineData("2 * 2 * 2 * 2 * 2", 32)]
        [InlineData("-50 + 100 + -50", 0)]
        [InlineData("5 * 2 + 10", 20)]
        [InlineData("5 + 2 * 10", 25)]
        [InlineData("20 + 2 * -10", 0)]
        [InlineData("50 / 2 * 2 + 10", 60)]
        [InlineData("2 * (5 + 10)", 30)]
        [InlineData("3 * 3 * 3 + 10", 37)]
        [InlineData("3 * (3 * 3) + 10", 37)]
        [InlineData("(5 + 10 * 2 + 15 / 3) * 2 + -10", 50)]
        public void TestEvalIntegerExpression(string source, long expected)
        {
            var evaluated = TestEval(source);
            TestIntegerObject(evaluated, expected);            
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("1 < 2", true)]
        [InlineData("1 > 2", false)]
        [InlineData("1 < 1", false)]
        [InlineData("1 > 1", false)]
        [InlineData("1 == 1", true)]
        [InlineData("1 != 1", false)]
        [InlineData("1 == 2", false)]
        [InlineData("1 != 2", true)]
        [InlineData("true == true", true)]
        [InlineData("false == false", true)]
        [InlineData("true == false", false)]
        [InlineData("true != false", true)]
        [InlineData("false != true", true)]
        [InlineData("(1 < 2) == true", true)]
        [InlineData("(1 < 2) == false", false)]
        [InlineData("(1 > 2) == true", false)]
        [InlineData("(1 > 2) == false", true)]
        public void TestEvalBooleanExpression(string source, bool expected)
        {
            var evaluated = TestEval(source);
            TestBooleanObject(evaluated, expected);
        }

        [Theory]
        [InlineData("!true", false)]
        [InlineData("!false", true)]
        [InlineData("!5", false)]
        [InlineData("!!true", true)]
        [InlineData("!!false", false)]
        [InlineData("!!5", true)]
        public void TestBangOperator(string source, bool expected)
        {
            var evaluated = TestEval(source);
            TestBooleanObject(evaluated, expected);
        }

        [Theory]
        [InlineData("if (true) { 10 }", 10L)]
        [InlineData("if (false) { 10 }", null)]
        [InlineData("if (1) { 10 }", 10L)]
        [InlineData("if (1 < 2) { 10 }", 10L)]
        [InlineData("if (1 > 2) { 10 }", null)]
        [InlineData("if (1 > 2) { 10 } else { 20 }", 20L)]
        [InlineData("if (1 < 2) { 10 } else { 20 }", 10L)]
        [InlineData(@"
            if (10 > 1) {
                if (10 > 1) {
                    return 10;
                }
                return 1;
            }", 10L)]
        public void TestIfElseExpressions(string source, object expected)
        {
            var evaluated = TestEval(source);
            if (expected == null)
                TestNullObject(evaluated);
            else
                TestIntegerObject(evaluated, (long)expected);
        }

        [Theory]
        [InlineData("return 10;", 10)]
        [InlineData("return 10; 9;", 10)]
        [InlineData("return 2 * 5; 9;", 10)]
        [InlineData("9; return 2 * 5; 9;", 10)]
        [InlineData("if (10 > 1) { return 10; }", 10)]
        [InlineData(@"
            if (10 > 1) {
                if (10 > 1) {
                    return 10;
                }
                return 1;
            }", 10)]
        [InlineData(@"
            let f = fn(x) {
                return x;
                x + 10;
            };
            f(10);", 10)]   
        [InlineData(@"
            let f = fn(x) {
                let result = x + 10;
                return result;
                return 10;
            };
            f(10);", 20)]                        
        public void TestReturnStatements(string source, long expected)
        {
            var evaluated = TestEval(source);
            TestIntegerObject(evaluated, expected);
        }

        [Theory]
        [InlineData("5 + true", "Type mismatch: Integer + Boolean")]
        [InlineData("5 + true; 5;", "Type mismatch: Integer + Boolean")]
        [InlineData("-true", "Unknown operator: -Boolean")]
        [InlineData("true + false", "Unknown operator: Boolean + Boolean")]
        [InlineData("5; true + false; 5;", "Unknown operator: Boolean + Boolean")]
        [InlineData("if (10 > 1) { true + false; }", "Unknown operator: Boolean + Boolean")]
        [InlineData(@"
            if (10 > 1) {
                if (19 > 1) {
                    return true + false;
                }
                return 1;
            }", "Unknown operator: Boolean + Boolean")]        
        [InlineData("foobar", "Identifier not found: foobar")]
        [InlineData("\"Hello\" - \"World\"", "Unknown operator: String - String")]
        [InlineData(@"{""name"": ""Monkey""}[fn(x) { x }];", "Unusable as hash key: Function")]
        public void TestErrorHandling(string source, string expected)
        {
            var evaluated = TestEval(source);
            Assert.IsType<MonkeyError>(evaluated);
            var errObj = (MonkeyError)evaluated;
            Assert.Equal(expected, errObj.Message);            
        }

        [Theory]
        [InlineData("let a = 5; a;", 5)]
        [InlineData("let a = 5 * 5; a;", 25)]
        [InlineData("let a = 5; let b = a; b;", 5)]
        [InlineData("let a = 5; let b = a; let c = a + b + 5; c;", 15)]
        public void TestLetStatements(string source, long expected)
        {
            TestIntegerObject(TestEval(source), expected);
        }

        [Fact]
        public void TestFunctionObject()
        {
            var source = "fn(x) { x + 2; };";
            var evaluated = TestEval(source);
            
            Assert.IsType<MonkeyFunction>(evaluated);
            var fn = (MonkeyFunction)evaluated;
            Assert.Single(fn.Parameters);
            Assert.Equal("x", fn.Parameters[0].String);

            var expectedBody = "(x + 2)";
            Assert.Equal(expectedBody, fn.Body.String);
        }

        [Theory]
        [InlineData("let identity = fn(x) { x; }; identity(5);", 5)]
        [InlineData("let identity = fn(x) { return x; }; identity(5);", 5)]
        [InlineData("let double = fn(x) { x * 2; }; double(5);", 10)]
        [InlineData("let add = fn(x, y) { x + y; }; add (5, 5);", 10)]
        [InlineData("let add = fn(x, y) { x + y; }; add(5 + 5, add(5, 5));", 20)]
        [InlineData("fn(x) { x; }(5)", 5)]
        public void TestFunctionApplication(string source, long expected)
        {
            TestIntegerObject(TestEval(source), expected);
        }

        [Theory]
        [InlineData(@"
            let newAdder = fn(x) {
                fn(y) { x + y };
            };

            let addTwo = newAdder(2);
            addTwo(2);", 4)]
        public void TestClosures(string source, long expected)
        {
            TestIntegerObject(TestEval(source), expected);
        }

        [Theory]
        [InlineData("\"Hello World!\"", "Hello World!")]
        public void TestStringLiteral(string source, string expected)
        {
            var evaluated = TestEval(source);
            Assert.IsType<MonkeyString>(evaluated);
            var str = (MonkeyString)evaluated;
            Assert.Equal(expected, str.Value);
        }

        [Theory]
        [InlineData("\"Hello\" + \" \" + \"World!\"", "Hello World!")]
        public void TestStringConcatenation(string source, string expected)
        {
            var evaluated = TestEval(source);
            Assert.IsType<MonkeyString>(evaluated);
            var str = (MonkeyString)evaluated;
            Assert.Equal(expected, str.Value);
        }

        [Theory]
        [InlineData("len(\"\")", 0L)]
        [InlineData("len(\"four\")", 4L)]
        [InlineData("len(\"hello world\")", 11L)]
        [InlineData("len(1)", "Argument to 'len' not supported. Got Integer")]
        [InlineData("len(\"one\", \"two\")", "Wrong number of arguments. Got=2, want=1")]
        [InlineData("len([1, 2, 3])", 3L)]
        [InlineData("len([])", 0L)]
        [InlineData("first([1, 2, 3])", 1L)]
        [InlineData("first([])", null)]
        [InlineData("first(1)", "Argument to 'first' must be Array. Got Integer")]
        [InlineData("last([1, 2, 3])", 3L)]
        [InlineData("last([])", null)]
        [InlineData("last(1)", "Argument to 'last' must be Array. Got Integer")]
        [InlineData("rest([1, 2, 3])", new[] { 2L, 3L })]
        [InlineData("rest([])", null)]
        [InlineData("push([], 1)", new[] { 1L })]
        [InlineData("push(1, 1)", "Argument to 'push' must be Array. Got Integer")]
        public void TestBuiltinFunctions(string source, object expected)
        {
            var evaluated = TestEval(source);
            if (expected is int i)
                TestIntegerObject(evaluated, (long)expected);
            else if (expected is string s)
            {
                if (evaluated is MonkeyError e)
                    Assert.Equal(s, e.Message);
            }
            else if (expected is null)
                TestNullObject(evaluated);
            else if (expected is int[] a)
            {
                Assert.IsType<MonkeyArray>(evaluated);
                var array = (MonkeyArray)evaluated;
                Assert.Equal(a.Length, array.Elements.Count);
                for (var idx = 0; idx < a.Length; idx++)
                    TestIntegerObject(array.Elements[idx], a[idx]);
            }
        }

        [Fact]
        public void TestArrayLiterals()
        {
            const string source = "[1, 2 * 2, 3 + 3]";
            var evaluated = TestEval(source);

            Assert.IsType<MonkeyArray>(evaluated);
            var result = (MonkeyArray)evaluated;

            Assert.Equal(3, result.Elements.Count);
            TestIntegerObject(result.Elements[0], 1);
            TestIntegerObject(result.Elements[1], 4);
            TestIntegerObject(result.Elements[2], 6);
        }

        [Theory]
        [InlineData("[1, 2, 3][0]", 1L)]
        [InlineData("[1, 2, 3][1]", 2L)]
        [InlineData("[1, 2, 3][2]", 3L)]
        [InlineData("let i = 0; [1][i];", 1L)]
        [InlineData("[1, 2, 3][1 + 1];", 3L)]
        [InlineData("let myArray = [1, 2, 3]; myArray[2];", 3L)]
        [InlineData("let myArray = [1, 2, 3]; myArray[0] + myArray[1] + myArray[2]", 6L)]
        [InlineData("let myArray = [1, 2, 3]; let i = myArray[0]; myArray[1]", 2L)]
        [InlineData("[1, 2, 3][3]", null)]
        [InlineData("[1, 2, 3][-1]", null)]
        public void TestArrayIndexExpressions(string source, object expected)
        {
            var evaluated = TestEval(source);
            if (expected is long l)
                TestIntegerObject(evaluated, l);
            else
                TestNullObject(evaluated);
        }

        [Fact]
        public void TestHashLiterals()
        {
            var source = @"let two = ""two"";
	                       {
                               ""one"": 10 - 9,
                               two: 1 + 1,
                               ""thr"" + ""ee"": 6 / 2,
                               4: 4,
                               true: 5,
                               false: 6
        	               }";

            var evaluated = TestEval(source);
            Assert.IsType<MonkeyHash>(evaluated);
            var result = (MonkeyHash)evaluated;

            var expected = new Dictionary<HashKey, long>
            {
                { new MonkeyString("one").HashKey(), 1 },
                { new MonkeyString("two").HashKey(), 2 },
                { new MonkeyString("three").HashKey(), 3 },
                { new MonkeyInteger(4).HashKey(), 4 },
                { Evaluator.True.HashKey(), 5 },
                { Evaluator.False.HashKey(), 6 }
            };

            Assert.Equal(expected.Count, result.Pairs.Count);
            foreach (var kv in result.Pairs)
            {
                var pair = result.Pairs[kv.Key];
                var value = ((MonkeyInteger)kv.Value.Value).Value;
                TestIntegerObject(pair.Value, value);
            }
        }

        [Theory]
        [InlineData(@"{""foo"": 5}[""foo""]", 5L)]
        [InlineData(@"{""foo"": 5}[""bar""]", null)]
        [InlineData(@"let key = ""foo""; {""foo"": 5}[key]", 5L)]
        [InlineData(@"{}[""foo""]", null)] 
        [InlineData("{5: 5}[5]", 5L)]
        [InlineData("{true: 5}[true]", 5L)]
        [InlineData("{false: 5}[false]", 5L)]
        public void TestHashIndexExpressions(string source, object expected)
        {
            var evaluated = TestEval(source);            
            if (expected is long l)
                TestIntegerObject(evaluated, l);
            else
                TestNullObject(evaluated);
        }

        private void TestNullObject(IMonkeyObject obj) => Assert.Equal(Evaluator.Null, obj);

        private IMonkeyObject TestEval(string source)
        {
            var lexer = new Lexer(source);
            var parser = new Parser(lexer, false);
            var program = parser.ParseProgram();        
            var env = new MonkeyEnvironment();
            return Evaluator.Eval(program, env);
        }

        private void TestIntegerObject(IMonkeyObject obj, long expected)
        {
            Assert.IsType<MonkeyInteger>(obj);
            var result = (MonkeyInteger)obj;
            Assert.Equal(expected, result.Value);
        }

        private void TestBooleanObject(IMonkeyObject obj, bool expected)
        {
            Assert.IsType<MonkeyBoolean>(obj);
            var result = (MonkeyBoolean)obj;
            Assert.Equal(expected, result.Value);
        }
    }
}