using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions; 

namespace FinalCalcuEDP
{
    public static class ExpressionEvaluator
    {
        private static readonly string[] Operators = { "+", "-", "*", "/", "%", "^" };
        private static readonly string[] Functions = { "sin", "cos", "tan", "asin", "acos", "atan", "sqrt", "log", "ln", "abs", "floor", "ceil", "sinh", "cosh", "tanh", "asinh", "acosh", "atanh", "log2" }; // Added more functions
        private static readonly string[] LeftAssociativeOps = { "+", "-", "*", "/", "%" };
        private static readonly string[] RightAssociativeOps = { "^" }; 
        public static double Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Expression cannot be empty.");

            Queue<string> outputQueue = ConvertToRPN(expression);
            double result = EvaluateRPN(outputQueue);
            return result;
        }

        private static Queue<string> ConvertToRPN(string expression)
        {
            var outputQueue = new Queue<string>();
            var operatorStack = new Stack<string>();
            var tokens = Tokenize(expression);

            foreach (var token in tokens)
            {
                if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    outputQueue.Enqueue(token);
                }
                else if (Functions.Contains(token))
                {
                    operatorStack.Push(token);
                }
                else if (Operators.Contains(token))
                {
                    while (operatorStack.Count > 0 && operatorStack.Peek() != "(")
                    {
                        string topOp = operatorStack.Peek();
                        if (Functions.Contains(topOp) ||
                           (Operators.Contains(topOp) && Precedence(topOp) > Precedence(token)) ||
                           (Operators.Contains(topOp) && Precedence(topOp) == Precedence(token) && LeftAssociativeOps.Contains(topOp)))
                        {
                            outputQueue.Enqueue(operatorStack.Pop());
                        }
                        else
                        {
                            break;
                        }
                    }
                    operatorStack.Push(token);
                }
                else if (token == "(")
                {
                    operatorStack.Push(token);
                }
                else if (token == ")")
                {
                    while (operatorStack.Count > 0 && operatorStack.Peek() != "(")
                    {
                        outputQueue.Enqueue(operatorStack.Pop());
                    }
                    if (operatorStack.Count == 0)
                        throw new SyntaxErrorException("Mismatched parentheses: No matching left parenthesis.");
                    operatorStack.Pop(); 
                    if (operatorStack.Count > 0 && Functions.Contains(operatorStack.Peek()))
                    {
                        outputQueue.Enqueue(operatorStack.Pop());
                    }
                }
                else
                {
                    throw new FormatException($"Unknown token: {token}");
                }
            }

            while (operatorStack.Count > 0)
            {
                string op = operatorStack.Pop();
                if (op == "(") 
                    throw new SyntaxErrorException("Mismatched parentheses: Extra left parenthesis.");
                outputQueue.Enqueue(op);
            }

            return outputQueue;
        }

        private static double EvaluateRPN(Queue<string> rpnQueue)
        {
            var evaluationStack = new Stack<double>();

            while (rpnQueue.Count > 0)
            {
                var token = rpnQueue.Dequeue();

                if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                {
                    evaluationStack.Push(number);
                }
                else if (Operators.Contains(token))
                {
                    if (evaluationStack.Count < 2)
                        throw new InvalidOperationException($"Insufficient operands for operator '{token}'.");
                    var operand2 = evaluationStack.Pop();
                    var operand1 = evaluationStack.Pop();
                    evaluationStack.Push(ApplyOperator(operand1, operand2, token));
                }
                else if (Functions.Contains(token))
                {
                    if (evaluationStack.Count < 1)
                        throw new InvalidOperationException($"Insufficient operands for function '{token}'.");
                    var operand = evaluationStack.Pop();
                    evaluationStack.Push(ApplyFunction(token, operand));
                }
            }

            if (evaluationStack.Count != 1)
                throw new InvalidOperationException("Invalid expression: Evaluation stack did not resolve to a single value.");

            return evaluationStack.Pop();
        }

        private static List<string> Tokenize(string expression)
        {
            expression = expression.Replace("(-", "(0-");
            if (expression.StartsWith("-")) expression = "0" + expression;


            var tokens = new List<string>();
            var regex = new Regex(@"(\d+\.?\d*(?:[eE][+-]?\d+)?)|([\+\-\*\/\%\^])|([a-zA-Z_][a-zA-Z0-9_]*)|([\(\)])"); 

            MatchCollection matches = regex.Matches(expression);
            int lastPos = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastPos)
                {
                    string gap = expression.Substring(lastPos, match.Index - lastPos);
                    if (!string.IsNullOrWhiteSpace(gap))
                        throw new FormatException($"Unexpected characters in expression: '{gap}'");
                }

                tokens.Add(match.Value);
                lastPos = match.Index + match.Length;
            }
            if (lastPos < expression.Length && !string.IsNullOrWhiteSpace(expression.Substring(lastPos)))
            {
                throw new FormatException($"Unexpected trailing characters in expression: '{expression.Substring(lastPos)}'");
            }

           
            return tokens;
        }


        private static int Precedence(string op) => op switch
        {
            "+" or "-" => 1,
            "*" or "/" or "%" => 2,
            "^" => 3,
            _ => 0 
        };

        private static double ApplyOperator(double operand1, double operand2, string op) => op switch
        {
            "+" => operand1 + operand2,
            "-" => operand1 - operand2,
            "*" => operand1 * operand2,
            "/" => (operand2 == 0) ? throw new DivideByZeroException() : operand1 / operand2,
            "%" => (operand2 == 0) ? throw new DivideByZeroException() : operand1 % operand2,
            "^" => Math.Pow(operand1, operand2),
            _ => throw new ArgumentException($"Unknown operator: {op}")
        };

        private static double ApplyFunction(string func, double operand)
        {
            try
            {
                return func switch
                {
                    "sin" => Math.Sin(operand), 
                    "cos" => Math.Cos(operand),
                    "tan" => Math.Tan(operand),
                    "asin" => (operand < -1 || operand > 1) ? throw new ArgumentException("Domain error for asin") : Math.Asin(operand),
                    "acos" => (operand < -1 || operand > 1) ? throw new ArgumentException("Domain error for acos") : Math.Acos(operand),
                    "atan" => Math.Atan(operand),
                    "sinh" => Math.Sinh(operand),
                    "cosh" => Math.Cosh(operand),
                    "tanh" => Math.Tanh(operand),
                    "asinh" => Math.Asinh(operand),
                    "acosh" => (operand < 1) ? throw new ArgumentException("Domain error for acosh") : Math.Acosh(operand),
                    "atanh" => (Math.Abs(operand) >= 1) ? throw new ArgumentException("Domain error for atanh") : Math.Atanh(operand),
                    "sqrt" => (operand < 0) ? throw new ArgumentException("Cannot sqrt negative number") : Math.Sqrt(operand),
                    "log" => (operand <= 0) ? throw new ArgumentException("Domain error for log10") : Math.Log10(operand), 
                    "log2" => (operand <= 0) ? throw new ArgumentException("Domain error for log2") : Math.Log2(operand), 
                    "abs" => Math.Abs(operand),
                    "floor" => Math.Floor(operand),
                    "ceil" => Math.Ceiling(operand),
                    _ => throw new ArgumentException($"Unknown function: {func}")
                };
            }
            catch (OverflowException) 
            {
                throw new OverflowException($"Result of function '{func}' is too large.");
            }
        }
    }
}