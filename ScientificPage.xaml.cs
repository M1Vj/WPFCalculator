using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using System.Windows.Media;

namespace FinalCalcuEDP
{
    public partial class ScientificPage : Page, ICalculatorPage
    {
        private string _expression = "";           
        private bool _isNewEntry = true;            
        private bool _showingFinalResult = false;   
        private int _parenthesisCount = 0;       

        private bool _isRepeatedEquals = false;
        private string _lastOperand = "";
        private string _lastOperator = "";
        private int _equalsClickCount = 0;

        private enum AngleMode { DEG, RAD, GRAD }
        private AngleMode _currentAngleMode = AngleMode.DEG;
        private bool _isTrigSecondFunctionActive = false;
        private bool _isHyperbolicActive = false;  
        private bool _isScientificNotation = false; 

        private bool _expMode = false;        
        private bool _expPending = false;      
        private string _expBase = "0";       
        private Dictionary<Button, string> _originalButtonContent = new Dictionary<Button, string>();

        private bool _isMainSecondFunctionActive = false; 

        public ScientificPage()
        {
            InitializeComponent();
            this.Loaded += ScientificPage_Loaded; 
            this.Unloaded += ScientificPage_Unloaded;
            var mainWindow = GetMainWindow();
            if (mainWindow != null) mainWindow.MemoryChanged += (s, e) => UpdateMemoryButtonsState();
            UpdateMemoryButtonsState();
            UpdateTrigButtonLabels();
            ClearAll();
        }

        private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;
        private void UpdateDisplay(string text) => GetMainWindow()?.UpdateDisplay(text);
        private void UpdateExpression(string text) => GetMainWindow()?.UpdateExpression(text);
        private string GetDisplayText() => GetMainWindow()?.GetDisplayText() ?? "0";
        private string GetExpressionText() => GetMainWindow()?.GetExpressionText() ?? "";

        private void ScientificPage_Loaded(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.MemoryChanged += MainWindow_MemoryChanged;
            }
            UpdateMemoryButtonsState(); 
            UpdateTrigButtonLabels(); 
        }

        private void ScientificPage_Unloaded(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.MemoryChanged -= MainWindow_MemoryChanged;
            }
        }

        public void InputDigit(string? digit)
        {
            if (digit == null) return;

            if (_expMode && _expPending)
            {
                string currentDisplay = GetDisplayText();
                if (char.IsDigit(digit[0]) || (digit == "-" && currentDisplay.EndsWith("e")))
                {
                    if (digit == "-" && currentDisplay.Contains("e-")) return;

                    UpdateDisplay(currentDisplay + digit);
                    _isNewEntry = false; 
                    return;
                }
                else
                {
                    FinalizeExponentEntry();
                }
            }

            if (_isNewEntry || _showingFinalResult)
            {
                UpdateDisplay(digit == "." ? "0." : digit);
                _isNewEntry = false;
                if (_showingFinalResult) UpdateExpression(""); 
                _showingFinalResult = false;
                _isRepeatedEquals = false;
                _equalsClickCount = 0;
            }
            else
            {
                string cur = GetDisplayText();
                if (digit == "." && cur.Contains(".")) return;
                if (cur == "0" && digit != ".") UpdateDisplay(digit);
                else UpdateDisplay(cur + digit);
            }
        }

        public void InputOperator(string? op)
        {
            if (string.IsNullOrWhiteSpace(op)) return;

            string currentDisplay = GetDisplayText();
            FinalizeExponentEntry();

            if (_showingFinalResult)
            {
                _expression = ConvertIfSciNotation(currentDisplay); 
                _showingFinalResult = false;
            }
            else if (!_isNewEntry)
            {
                _expression += ConvertIfSciNotation(currentDisplay);
            }
            else if (!string.IsNullOrEmpty(_expression) && IsOperatorChar(_expression.Trim().LastOrDefault()))
            {
                int lastOpIndex = -1;
                string trimmedExpr = _expression.Trim();
                for (int i = trimmedExpr.Length - 1; i >= 0; i--)
                {
                    if (IsOperatorChar(trimmedExpr[i]) && (i == 0 || trimmedExpr[i - 1] == ' '))
                    {
                        lastOpIndex = i;
                        break;
                    }
                    if (!char.IsWhiteSpace(trimmedExpr[i])) break;
                }

                if (lastOpIndex >= 0)
                {
                    int startOfOp = trimmedExpr.LastIndexOf(' ', lastOpIndex) + 1;
                    if (startOfOp < 0) startOfOp = 0;
                    _expression = _expression.Substring(0, startOfOp); 
                }
                else if (_expression.Trim().EndsWith("(")) { }
                else if (string.IsNullOrWhiteSpace(_expression))
                {
                    _expression = ConvertIfSciNotation(currentDisplay);
                }
            }
            else if (!_isNewEntry)
            {
                _expression += ConvertIfSciNotation(currentDisplay);
            }
            else if (!string.IsNullOrWhiteSpace(_expression) && !IsOperatorChar(_expression.Trim().LastOrDefault()) && !_expression.Trim().EndsWith("("))
            {
            }
            else if (string.IsNullOrWhiteSpace(_expression)) 
            {
                _expression = "0";
            }

            string internalOp = op switch
            {
                "−" => "-",
                "×" => "*",
                "÷" => "/",
                "xʸ" => "^",
                "mod" => "%",
                _ => op
            };
            if (op == "ʸ√x") internalOp = " yroot ";
            else if (op == "logʸx") internalOp = " logbase ";
            else internalOp = $" {internalOp} ";

            _expression += internalOp;

            UpdateExpression(_expression);
            _isNewEntry = true; 
            _isRepeatedEquals = false; 
            _equalsClickCount = 0;
        }

        private bool IsOperatorChar(char c) => "+-*/%^".Contains(c);

        private string ConvertIfSciNotation(string displayValue)
        {
            if (!_isScientificNotation && displayValue.Contains('E', StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(displayValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    return val.ToString(CultureInfo.InvariantCulture);
                else
                    return displayValue; 
            }
            return displayValue;
        }

        public void InputSpecialOperator(string? op)
        {
            if (op == "%")
            {
                string currentDisplay = GetDisplayText();
                if (double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                {
                    FinalizeExponentEntry(); 
                    double result = number / 100.0;

                    if (!string.IsNullOrWhiteSpace(_expression) && _expression.Contains(' '))
                    {
                        var parts = _expression.Trim().Split(' ');
                        double baseValue = 0;
                        bool baseFound = false;
                        for (int i = parts.Length - 2; i >= 0; i--)
                        {
                            if (double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out baseValue))
                            {
                                if (i + 1 < parts.Length && IsOperatorChar(parts[i + 1].FirstOrDefault()))
                                {
                                    baseFound = true;
                                    break;
                                }
                            }
                        }

                        if (baseFound)
                        {
                            result = baseValue * (number / 100.0);
                        }
                    }

                    UpdateDisplay(FormatNumber(result));
                    UpdateExpression($"percent({currentDisplay})"); 
                    _isNewEntry = true;
                    _showingFinalResult = true; 
                    _expression = FormatNumber(result);
                    ResetRepeatState();
                }
                else HandleError("Error");
            }
            else if (op == "!")
            {
                InputUnaryFunction("n!");
            }
        }

        public void Calculate()
        {
            FinalizeExponentEntry();

            string currentDisplay = GetDisplayText();
            if (!_isNewEntry && !_showingFinalResult)
            {
                _expression += ConvertIfSciNotation(currentDisplay);
            }
            while (_parenthesisCount > 0)
            {
                _expression += ")";
                _parenthesisCount--;
            }

            if (_isRepeatedEquals && _equalsClickCount >= 2 && !string.IsNullOrEmpty(_lastOperator) && "+-*/%^".Contains(_lastOperator))
            {
                var mainWindow = GetMainWindow();
                if (mainWindow != null)
                {
                    mainWindow.ShowIterateDialog("Continue to iterate?", (continueIteration) =>
                    {
                        if (continueIteration) PerformCalculation(true);
                        else ResetRepeatState();
                    });
                    return;
                }
            }

            PerformCalculation(false);
        }

        private void PerformCalculation(bool isRepeat)
        {
            if ((string.IsNullOrWhiteSpace(_expression) || (_isNewEntry && IsOperatorChar(_expression.Trim().LastOrDefault()))) && !isRepeat)
                return;

            string expressionToEvaluate = _expression;
            string expressionForDisplay = _expression;

            try
            {
                if (isRepeat && !string.IsNullOrEmpty(_lastOperator) && !string.IsNullOrEmpty(_lastOperand))
                {
                    string currentResult = ConvertIfSciNotation(GetDisplayText());
                    expressionToEvaluate = $"{currentResult} {_lastOperator} {_lastOperand}";
                    expressionForDisplay = expressionToEvaluate;
                    UpdateExpression($"{expressionForDisplay} =");
                }
                else if (!isRepeat)
                {
                    string[] parts = _expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        _lastOperand = parts.Last();
                        _lastOperator = "";
                        for (int i = parts.Length - 2; i >= 0; i--)
                        {
                            if (IsOperatorChar(parts[i].FirstOrDefault()) || parts[i] == "yroot" || parts[i] == "logbase")
                            {
                                _lastOperator = parts[i];
                                break;
                            }
                        }
                    }
                    else
                    {
                        _lastOperand = "";
                        _lastOperator = "";
                    }
                    UpdateExpression($"{expressionForDisplay} =");
                }
                else
                {
                    ResetRepeatState();
                    return;
                }

                string preparedExpression = PrepareExpressionForCompute(expressionToEvaluate);
                if (string.IsNullOrEmpty(preparedExpression)) return;

                double result = ExpressionEvaluator.Evaluate(preparedExpression);

                string resultString = FormatNumber(result);
                UpdateDisplay(resultString);

                GetMainWindow()?.AddToHistory(expressionForDisplay + " =", resultString);

                _expression = resultString;
                _isNewEntry = true;
                _showingFinalResult = true;

                if (!isRepeat) _equalsClickCount = 1;
                else _equalsClickCount++;

                _isRepeatedEquals = !string.IsNullOrEmpty(_lastOperator) && !string.IsNullOrEmpty(_lastOperand);

            }
            catch (DivideByZeroException) { HandleError("Error: Divide by zero"); ResetRepeatState(); }
            catch (SyntaxErrorException) { HandleError("Error: Syntax"); ResetRepeatState(); }
            catch (EvaluateException) { HandleError("Error: Evaluation"); ResetRepeatState(); }
            catch (ArgumentException) { HandleError($"Error: Invalid argument"); ResetRepeatState(); }
            catch (OverflowException) { HandleError($"Error: Overflow"); ResetRepeatState(); }
            catch (Exception) { HandleError("Error"); ResetRepeatState(); }
        }

        private void ResetRepeatState()
        {
            _isRepeatedEquals = false;
            _equalsClickCount = 0;
            _lastOperator = "";
            _lastOperand = "";
        }

        public void ClearAll()
        {
            _expression = "";
            _parenthesisCount = 0;
            UpdateDisplay("0");
            UpdateExpression("");
            _isNewEntry = true;
            _showingFinalResult = false;
            ResetRepeatState();
            FinalizeExponentEntry();
            _isTrigSecondFunctionActive = false;
            _isHyperbolicActive = false;
            UpdateSecondFunctionVisuals();
            UpdateHyperbolicVisuals();
            UpdateTrigButtonLabels();
            if (this.FindName("FEButton") is ToggleButton feButton) feButton.IsChecked = _isScientificNotation;
            else if (this.FindName("FEButton") is Button feBtn) feBtn.Tag = _isScientificNotation ? "Active" : null;
        }

        public void ClearEntry()
        {
            UpdateDisplay("0");
            _isNewEntry = true;
            _showingFinalResult = false;
            FinalizeExponentEntry();
        }

        public void Backspace()
        {
            if (_expMode && _expPending)
            {
                string currentDisplay = GetDisplayText();
                if (currentDisplay.EndsWith("e", StringComparison.OrdinalIgnoreCase))
                {
                    FinalizeExponentEntry();
                    UpdateDisplay(_expBase);
                    _isNewEntry = false;
                }
                else if (currentDisplay.Length > 0)
                {
                    UpdateDisplay(currentDisplay.Substring(0, currentDisplay.Length - 1));
                    _expPending = GetDisplayText().EndsWith("e", StringComparison.OrdinalIgnoreCase) || GetDisplayText().EndsWith("e-", StringComparison.OrdinalIgnoreCase);
                }
                return;
            }

            if (_isNewEntry || _showingFinalResult) return;
            string cur = GetDisplayText();
            if (cur.Length > 1)
            {
                UpdateDisplay(cur.Substring(0, cur.Length - 1));
                if (GetDisplayText() == "-") { UpdateDisplay("0"); _isNewEntry = true; }
            }
            else
            {
                UpdateDisplay("0");
                _isNewEntry = true;
            }
        }

        public void Negate()
        {
            string currentDisplay = GetDisplayText();
            if (currentDisplay.StartsWith("Error")) return;

            if (_expMode && _expPending)
            {
                string expPart = "";
                int eIndexLower = currentDisplay.LastIndexOf('e');
                int eIndexUpper = currentDisplay.LastIndexOf('E');
                int eIndex = Math.Max(eIndexLower, eIndexUpper);

                if (eIndex >= 0)
                {
                    expPart = currentDisplay.Substring(eIndex + 1);
                    string basePart = currentDisplay.Substring(0, eIndex + 1);

                    if (expPart.StartsWith("-"))
                    {
                        UpdateDisplay(basePart + expPart.Substring(1));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(expPart))
                        {
                            UpdateDisplay(basePart + "-" + expPart);
                        }
                    }
                }
                return;
            }

            FinalizeExponentEntry();

            if (_showingFinalResult)
            {
                if (double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double res))
                {
                    UpdateDisplay(FormatNumber(-res));
                    _expression = FormatNumber(-res);
                    _showingFinalResult = false;
                    _isNewEntry = true;
                    ResetRepeatState();
                }
                return;
            }

            if (_isNewEntry && currentDisplay == "0") return;

            if (double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
            {
                UpdateDisplay(FormatNumber(-number));
                _isNewEntry = false;
            }
        }

        public void InputUnaryFunction(string? funcName)
        {
            if (funcName == null) return;
            string currentDisplay = GetDisplayText();
            if (!double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
            { HandleError("Error"); return; }

            FinalizeExponentEntry();

            double result = double.NaN;
            string exprFormat = "";

            try
            {
                switch (funcName)
                {
                    case "x²": result = Math.Pow(number, 2); exprFormat = $"sqr({currentDisplay})"; break;
                    case "x³": result = Math.Pow(number, 3); exprFormat = $"cube({currentDisplay})"; break;
                    case "¹/ₓ": if (number == 0) throw new DivideByZeroException(); result = 1.0 / number; exprFormat = $"1/({currentDisplay})"; break;
                    case "√x": if (number < 0) throw new ArgumentException("Invalid input"); result = Math.Sqrt(number); exprFormat = $"√({currentDisplay})"; break;
                    case "³√x": result = Math.Cbrt(number); exprFormat = $"cuberoot({currentDisplay})"; break;
                    case "n!": result = CalculateFactorial(number); exprFormat = $"fact({currentDisplay})"; break;
                    case "|x|": result = Math.Abs(number); exprFormat = $"abs({currentDisplay})"; break;
                    case "10ˣ": result = Math.Pow(10, number); exprFormat = $"10^({currentDisplay})"; break;
                    case "eˣ": result = Math.Exp(number); exprFormat = $"e^({currentDisplay})"; break;
                    case "2ˣ": result = Math.Pow(2, number); exprFormat = $"2^({currentDisplay})"; break;
                    case "log": if (number <= 0) throw new ArgumentException("Invalid input"); result = Math.Log10(number); exprFormat = $"log({currentDisplay})"; break;
                    case "ln": if (number <= 0) throw new ArgumentException("Invalid input"); result = Math.Log(number); exprFormat = $"ln({currentDisplay})"; break;
                    case "log₂x": if (number <= 0) throw new ArgumentException("Invalid input"); result = Math.Log2(number); exprFormat = $"log₂({currentDisplay})"; break;
                    case "floor": result = Math.Floor(number); exprFormat = $"floor({currentDisplay})"; break;
                    case "ceil": result = Math.Ceiling(number); exprFormat = $"ceil({currentDisplay})"; break;
                    case "rand": result = new Random().NextDouble(); exprFormat = $"rand()"; number = double.NaN; break;
                    default: return;
                }

                if (double.IsNaN(result) || double.IsInfinity(result))
                    throw new ArithmeticException("Result is invalid");

                UpdateDisplay(FormatNumber(result));
                if (funcName != "rand") UpdateExpression(exprFormat);
                else UpdateExpression("rand()");

                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = FormatNumber(result);
                ResetRepeatState();
            }
            catch (DivideByZeroException) { HandleError("Error: Divide by zero"); ResetRepeatState(); }
            catch (ArgumentException ex) { HandleError($"Error: {ex.Message}"); ResetRepeatState(); }
            catch (OverflowException) { HandleError("Error: Overflow"); ResetRepeatState(); }
            catch (ArithmeticException ex) { HandleError($"Error: {ex.Message}"); ResetRepeatState(); }
            catch (Exception) { HandleError("Error"); ResetRepeatState(); }
        }

        private double CalculateFactorial(double n)
        {
            if (n < 0 || n != Math.Floor(n)) throw new ArgumentException("Factorial requires non-negative integer");
            if (n > 170) throw new OverflowException("Factorial result too large for double");
            if (n == 0) return 1;
            double fact = 1;
            for (long i = 1; i <= (long)n; i++)
            {
                try
                {
                    fact = checked(fact * i);
                }
                catch (OverflowException)
                {
                    throw new OverflowException("Factorial calculation overflowed.");
                }
            }
            return fact;
        }

        private void PerformTrigFunction(string funcName)
        {
            string currentDisplay = GetDisplayText();
            if (!double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
            { HandleError("Error"); return; }

            FinalizeExponentEntry();

            double result = double.NaN;
            string exprPrefix = funcName;
            bool isInverse = funcName.StartsWith("a");

            try
            {
                double angleRad;
                if (isInverse)
                {
                    switch (funcName)
                    {
                        case "asin": if (number < -1 || number > 1) throw new ArgumentException("Domain error"); result = Math.Asin(number); break;
                        case "acos": if (number < -1 || number > 1) throw new ArgumentException("Domain error"); result = Math.Acos(number); break;
                        case "atan": result = Math.Atan(number); break;
                        case "asec": if (Math.Abs(number) < 1) throw new ArgumentException("Domain error"); result = Math.Acos(1.0 / number); break;
                        case "acsc": if (Math.Abs(number) < 1) throw new ArgumentException("Domain error"); result = Math.Asin(1.0 / number); break;
                        case "acot": result = Math.Atan(1.0 / number); break;
                        default: throw new ArgumentException("Unknown function");
                    }
                    if (!double.IsNaN(result))
                        result = ConvertAngle(result, AngleMode.RAD, _currentAngleMode);
                }
                else
                {
                    angleRad = ConvertAngle(number, _currentAngleMode, AngleMode.RAD);
                    switch (funcName)
                    {
                        case "sin": result = Math.Sin(angleRad); break;
                        case "cos": result = Math.Cos(angleRad); break;
                        case "tan":
                            double cosVal = Math.Cos(angleRad);
                            if (Math.Abs(cosVal) < 1e-15) throw new ArithmeticException("Invalid input for tan");
                            result = Math.Tan(angleRad); break;
                        case "sec":
                            double cosValSec = Math.Cos(angleRad);
                            if (Math.Abs(cosValSec) < 1e-15) throw new ArithmeticException("Invalid input for sec");
                            result = 1.0 / cosValSec; break;
                        case "csc":
                            double sinValCsc = Math.Sin(angleRad);
                            if (Math.Abs(sinValCsc) < 1e-15) throw new ArithmeticException("Invalid input for csc");
                            result = 1.0 / sinValCsc; break;
                        case "cot":
                            double sinValCot = Math.Sin(angleRad);
                            if (Math.Abs(sinValCot) < 1e-15) throw new ArithmeticException("Invalid input for cot");
                            result = Math.Cos(angleRad) / sinValCot;
                            break;
                        default: throw new ArgumentException("Unknown function");
                    }
                    if (Math.Abs(result) < 1e-15) result = 0;
                }

                if (double.IsNaN(result) || double.IsInfinity(result))
                    throw new ArithmeticException("Result is invalid");

                UpdateExpression($"{exprPrefix}({currentDisplay})");
                UpdateDisplay(FormatNumber(result));
                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = FormatNumber(result);
                ResetRepeatState();
            }
            catch (ArgumentException ex) { HandleError($"Error: {ex.Message}"); ResetRepeatState(); }
            catch (ArithmeticException ex) { HandleError($"Error: {ex.Message}"); ResetRepeatState(); }
            catch (Exception) { HandleError("Error"); ResetRepeatState(); }
        }

        private void PerformHyperbolicFunction(string funcName)
        {
            string currentDisplay = GetDisplayText();
            if (!double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
            { HandleError("Error"); return; }

            FinalizeExponentEntry();

            double result = double.NaN;
            string exprPrefix = funcName;

            try
            {
                switch (funcName)
                {
                    case "sinh": result = Math.Sinh(number); break;
                    case "cosh": result = Math.Cosh(number); break;
                    case "tanh": result = Math.Tanh(number); break;
                    case "sech": result = 1.0 / Math.Cosh(number); break;
                    case "csch":
                        if (number == 0) throw new ArithmeticException("Invalid input for csch");
                        result = 1.0 / Math.Sinh(number); break;
                    case "coth":
                        if (number == 0) throw new ArithmeticException("Invalid input for coth");
                        result = 1.0 / Math.Tanh(number); break;
                    case "asinh": result = Math.Asinh(number); break;
                    case "acosh": if (number < 1) throw new ArgumentException("Domain error"); result = Math.Acosh(number); break;
                    case "atanh": if (Math.Abs(number) >= 1) throw new ArgumentException("Domain error"); result = Math.Atanh(number); break;
                    case "asech": if (number <= 0 || number > 1) throw new ArgumentException("Domain error"); result = Math.Log((1 + Math.Sqrt(1 - number * number)) / number); break;
                    case "acsch": if (number == 0) throw new ArgumentException("Domain error"); result = Math.Log(1 / number + Math.Sqrt(1 / (number * number) + 1)); break;
                    case "acoth": if (Math.Abs(number) <= 1) throw new ArgumentException("Domain error"); result = 0.5 * Math.Log((number + 1) / (number - 1)); break;
                    default: throw new ArgumentException("Unknown function");
                }

                if (double.IsNaN(result) || double.IsInfinity(result))
                    throw new ArithmeticException("Result is invalid");

                UpdateExpression($"{exprPrefix}({currentDisplay})");
                UpdateDisplay(FormatNumber(result));
                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = FormatNumber(result);
                ResetRepeatState();
            }
            catch (ArgumentException ex) { HandleError($"Error: {ex.Message}"); ResetRepeatState(); }
            catch (ArithmeticException ex) { HandleError($"Error: {ex.Message}"); ResetRepeatState(); }
            catch (Exception) { HandleError("Error"); ResetRepeatState(); }
        }

        private double ConvertAngle(double angle, AngleMode from, AngleMode to)
        {
            if (from == to) return angle;

            double angleInRad = from switch
            {
                AngleMode.DEG => angle * (Math.PI / 180.0),
                AngleMode.GRAD => angle * (Math.PI / 200.0),
                AngleMode.RAD => angle,
                _ => angle
            };

            return to switch
            {
                AngleMode.DEG => angleInRad * (180.0 / Math.PI),
                AngleMode.GRAD => angleInRad * (200.0 / Math.PI),
                AngleMode.RAD => angleInRad,
                _ => angleInRad
            };
        }

        private void ToggleSecondFunction()
        {
            _isTrigSecondFunctionActive = !_isTrigSecondFunctionActive;
            UpdateSecondFunctionVisuals();
            UpdateTrigButtonLabels();
        }

        private void ToggleHyperbolic()
        {
            _isHyperbolicActive = !_isHyperbolicActive;
            UpdateHyperbolicVisuals();
            UpdateTrigButtonLabels();
        }

        private void UpdateSecondFunctionVisuals()
        {
            if (this.FindName("SecondFunctionButton") is Button secButton)
                secButton.Tag = _isTrigSecondFunctionActive ? "Active" : null;
        }
        private void UpdateHyperbolicVisuals()
        {
            if (this.FindName("HyperbolicButton") is Button hypButton)
                hypButton.Tag = _isHyperbolicActive ? "Active" : null;
        }

        private void UpdateTrigButtonLabels()
        {
            Button? sinBtn = this.FindName("SinButton") as Button;
            Button? cosBtn = this.FindName("CosButton") as Button;
            Button? tanBtn = this.FindName("TanButton") as Button;
            Button? secBtn = this.FindName("SecButton") as Button;
            Button? cscBtn = this.FindName("CscButton") as Button;
            Button? cotBtn = this.FindName("CotButton") as Button;

            if (sinBtn == null || cosBtn == null || tanBtn == null || secBtn == null || cscBtn == null || cotBtn == null)
                return;

            Action<Button, string, string, string, string> setContent =
                (btn, normal, hyp, inv, invHyp) => {
                    if (_isTrigSecondFunctionActive && _isHyperbolicActive) btn.Content = invHyp;
                    else if (_isTrigSecondFunctionActive) btn.Content = inv;
                    else if (_isHyperbolicActive) btn.Content = hyp;
                    else btn.Content = normal;
                };

            setContent(sinBtn, "sin", "sinh", "sin⁻¹", "sinh⁻¹");
            setContent(cosBtn, "cos", "cosh", "cos⁻¹", "cosh⁻¹");
            setContent(tanBtn, "tan", "tanh", "tan⁻¹", "tanh⁻¹");
            setContent(secBtn, "sec", "sech", "sec⁻¹", "sech⁻¹");
            setContent(cscBtn, "csc", "csch", "csc⁻¹", "csch⁻¹");
            setContent(cotBtn, "cot", "coth", "cot⁻¹", "coth⁻¹");
        }

        private void MemoryOperation(string? memOp)
        {
            if (memOp == null) return;

            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;

            FinalizeExponentEntry();

            if (memOp == "M∨" || memOp == "M⏷") 
            {
                mainWindow.ToggleMemoryPanel();
                return;
            }

            switch (memOp)
            {
                case "MC": 
                    mainWindow.ClearMemory();
                    ResetRepeatState(); 
                    break;

                case "MR": 
                    if (mainWindow.IsMemorySet) 
                    {
                        double memoryValue = mainWindow.RecallMemory();
                        UpdateDisplay(FormatNumber(memoryValue)); 
                        _isNewEntry = false;        
                        _showingFinalResult = false; 
                        ResetRepeatState();     
                        _expression = "";
                        UpdateExpression("");
                    }
                    break;

                case "MS": 
                    string displayForMS = GetDisplayText();
                    if (double.TryParse(displayForMS, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentNumberMS))
                    {
                        mainWindow.AddToMemory(currentNumberMS); 
                        _isNewEntry = true; 
                        _showingFinalResult = false;
                        ResetRepeatState();
                    }
                    break;

                case "M+": 
                    string displayForMPlus = GetDisplayText();
                    if (double.TryParse(displayForMPlus, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentNumberMPlus))
                    {
                        mainWindow.MemoryAdd(currentNumberMPlus);
                        _isNewEntry = true; 
                        _showingFinalResult = false;
                        ResetRepeatState();
                    }
                    break;

                case "M-": 
                    string displayForMMinus = GetDisplayText();
                    if (double.TryParse(displayForMMinus, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentNumberMMinus))
                    {
                        mainWindow.MemorySubtract(currentNumberMMinus);
                        _isNewEntry = true; 
                        _showingFinalResult = false;
                        ResetRepeatState();
                    }
                    break;
            }
            UpdateMemoryButtonsState();
        }

        private string FormatNumber(double value, bool useScientificNotation)
        {
            if (double.IsNaN(value)) return "Not a number";
            if (double.IsInfinity(value)) return "Infinity";

            if (useScientificNotation)
            {
                return value.ToString("0.###########E+0", CultureInfo.InvariantCulture);
            }
            else
            {
                return value.ToString("G15", CultureInfo.InvariantCulture);
            }
        }

        private void UpdateMemoryButtonsState()
        {
            var mainWindow = GetMainWindow();
            bool memorySet = mainWindow != null && mainWindow.IsMemorySet;

            if (this.FindName("MemoryDropdownButton") is Button mdButton)
            {
                mdButton.IsEnabled = memorySet;
            }

            foreach (var btn in StandardPage.FindVisualChildren<Button>(this))
            {
                if (btn == null || btn.Content == null) continue;

                if (btn.Name == "MemoryDropdownButton") continue;

                string? content = btn.Content.ToString();

                switch (content)
                {
                    case "MC":
                        btn.IsEnabled = memorySet;
                        break;
                    case "MR":
                        btn.IsEnabled = memorySet;
                        break;
                }
            }
        }

        public void InputConstant(string? constant)
        {
            if (constant == null) return;
            FinalizeExponentEntry();

            string valueStr = constant switch
            {
                "π" => Math.PI.ToString(CultureInfo.InvariantCulture),
                "e" => Math.E.ToString(CultureInfo.InvariantCulture),
                _ => ""
            };

            if (!string.IsNullOrEmpty(valueStr))
            {
                UpdateDisplay(FormatNumber(double.Parse(valueStr)));
                _isNewEntry = false;
                if (_showingFinalResult) UpdateExpression("");
                _showingFinalResult = false;
                ResetRepeatState();
            }
        }

        private void Exp_Click(object? sender, RoutedEventArgs e)
        {
            string currentDisplay = GetDisplayText();

            if (currentDisplay.StartsWith("Error") || _expMode ||
                currentDisplay.EndsWith("e", StringComparison.OrdinalIgnoreCase) ||
                currentDisplay.EndsWith("e+", StringComparison.OrdinalIgnoreCase) ||
                currentDisplay.EndsWith("e-", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                _expBase = currentDisplay;
                UpdateDisplay(currentDisplay + "e");
                _expMode = true;
                _expPending = true;
                _isNewEntry = false;
                _showingFinalResult = false;
                ResetRepeatState();
            }
        }

        private void FinalizeExponentEntry()
        {
            if (_expMode)
            {
                string currentDisplay = GetDisplayText();
                if (currentDisplay.EndsWith("e", StringComparison.OrdinalIgnoreCase))
                    UpdateDisplay(currentDisplay + "0");
                else if (currentDisplay.EndsWith("e-", StringComparison.OrdinalIgnoreCase))
                    UpdateDisplay(currentDisplay + "0");

                _expMode = false;
                _expPending = false;
            }
        }

        private void FE_Click(object? sender, RoutedEventArgs e)
        {
            _isScientificNotation = !_isScientificNotation;
            FinalizeExponentEntry();

            string currentDisplay = GetDisplayText();
            if (!currentDisplay.StartsWith("Error") && double.TryParse(currentDisplay, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            {
                UpdateDisplay(FormatNumber(value));
            }

            if (sender is ToggleButton feButton)
            {
                feButton.IsChecked = _isScientificNotation;
            }
            else if (sender is Button button)
            {
                button.Tag = _isScientificNotation ? "Active" : null;
            }
        }

        private string FormatNumber(double value)
        {
            if (double.IsNaN(value)) return "Not a number";
            if (double.IsInfinity(value)) return "Infinity";

            if (_isScientificNotation)
            {
                return value.ToString("0.###########E+0", CultureInfo.InvariantCulture);
            }
            else
            {
                string formatted = value.ToString("G15", CultureInfo.InvariantCulture);
                return formatted;
            }
        }

        private void ConvertToDms()
        {
            FinalizeExponentEntry();
            if (double.TryParse(GetDisplayText(), NumberStyles.Any, CultureInfo.InvariantCulture, out double deg))
            {
                int d = (int)Math.Truncate(deg);
                double remainingMinutes = Math.Abs(deg - d) * 60;
                int m = (int)Math.Truncate(remainingMinutes);
                double s = (remainingMinutes - m) * 60;

                if (Math.Abs(s - 60.0) < 1e-9)
                {
                    s = 0;
                    m++;
                    if (m == 60)
                    {
                        m = 0;
                        if (d >= 0) d++; else d--;
                    }
                }

                string outStr = $"{d}°{m}'{s:F2}''";

                UpdateDisplay(outStr);
                UpdateExpression($"dms({deg.ToString("G15", CultureInfo.InvariantCulture)})");
                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = "";
                ResetRepeatState();
            }
            else HandleError("Error");
        }

        private void ConvertToDeg()
        {
            FinalizeExponentEntry();
            string text = GetDisplayText();
            var match = Regex.Match(text, @"^(-?\d+)°(\d+)'(\d+(?:\.\d+)?)''$");

            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int dd) &&
                int.TryParse(match.Groups[2].Value, out int mm) &&
                double.TryParse(match.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double ss))
            {
                if (mm < 0 || mm >= 60 || ss < 0 || ss >= 60) { HandleError("Invalid DMS"); return; }

                double decimalDeg = Math.Abs(dd) + mm / 60.0 + ss / 3600.0;
                if (dd < 0) decimalDeg = -decimalDeg;

                UpdateDisplay(FormatNumber(decimalDeg));
                UpdateExpression($"deg({text})");
                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = FormatNumber(decimalDeg);
                ResetRepeatState();
            }
            else HandleError("Invalid DMS format");
        }

        private string PrepareExpressionForCompute(string expr)
        {
            expr = expr.Replace("−", "-").Replace("×", "*").Replace("÷", "/");
            expr = expr.Replace(" mod ", " % ");
            expr = expr.Replace(" ^ ", "^");

            expr = Regex.Replace(expr, @"(\S+)\s+yroot\s+(\S+)", m =>
            {
                string yStr = m.Groups[1].Value;
                string xStr = m.Groups[2].Value;
                return $"({xStr})^(1/({yStr}))";
            });

            expr = Regex.Replace(expr, @"(\S+)\s+logbase\s+(\S+)", m =>
            {
                string baseStr = m.Groups[1].Value;
                string xStr = m.Groups[2].Value;
                return $"ln({xStr})/ln({baseStr})";
            });

            expr = Regex.Replace(expr.Trim(), @"[\+\-\*\/\%\^\s]+$", "");

            return expr;
        }

        private void HandleError(string message)
        {
            UpdateDisplay(message);
            UpdateExpression("");
            _expression = "";
            _isNewEntry = true;
            _showingFinalResult = false;
            _parenthesisCount = 0;
            ResetRepeatState();
            FinalizeExponentEntry();
        }

        private void Number_Click(object? sender, RoutedEventArgs e) => InputDigit((sender as Button)?.Content?.ToString());
        private void Operation_Click(object? sender, RoutedEventArgs e) => InputOperator((sender as Button)?.Content?.ToString());
        private void Equals_Click(object? sender, RoutedEventArgs e) => Calculate();
        private void Clear_Click(object? sender, RoutedEventArgs e) => ClearAll();
        private void Backspace_Click(object? sender, RoutedEventArgs e) => Backspace();
        private void Negate_Click(object? sender, RoutedEventArgs e) => Negate();
        private void Memory_Click(object? sender, RoutedEventArgs e) => MemoryOperation((sender as Button)?.Content?.ToString());
        private void Constant_Click(object? sender, RoutedEventArgs e) => InputConstant((sender as Button)?.Content?.ToString());
        private void Parenthesis_Click(object? sender, RoutedEventArgs e)
        {
            string? parenthesis = (sender as Button)?.Content?.ToString();
            if (parenthesis == null) return;
            FinalizeExponentEntry();

            if (_showingFinalResult)
            {
                _expression = ""; UpdateExpression("");
                _parenthesisCount = 0;
                _showingFinalResult = false;
                _isNewEntry = true;
            }

            if (parenthesis == "(")
            {
                if (!_isNewEntry && !string.IsNullOrEmpty(_expression) && !IsOperatorChar(_expression.Trim().LastOrDefault()) && _expression.Trim().LastOrDefault() != '(')
                {
                    _expression += ConvertIfSciNotation(GetDisplayText()) + " * ";
                }
                else if (_expression.Trim().EndsWith(")"))
                {
                    _expression += " * ";
                }

                _expression += "( ";
                _parenthesisCount++;
                _isNewEntry = true;
            }
            else if (parenthesis == ")")
            {
                if (_parenthesisCount > 0)
                {
                    if (!_isNewEntry && !IsOperatorChar(_expression.Trim().LastOrDefault()))
                    {
                        _expression += ConvertIfSciNotation(GetDisplayText());
                    }
                    else if (_isNewEntry && (_expression.Trim().EndsWith("(") || IsOperatorChar(_expression.Trim().LastOrDefault())))
                    {
                    }

                    _expression += " )";
                    _parenthesisCount--;
                    _isNewEntry = false;
                }
            }
            UpdateExpression(_expression);
            ResetRepeatState();
        }

        private void SecondFunction_Click(object? sender, RoutedEventArgs e)
        {
            _isMainSecondFunctionActive = !_isMainSecondFunctionActive;
            UpdateMainGridSecondFunctions();
            if (sender is Button secButton)
            {
                secButton.Tag = _isMainSecondFunctionActive ? "Active" : null;
            }
        }

        private void HyperbolicButton_Click(object? sender, RoutedEventArgs e)
        {
            ToggleHyperbolic();
        }

        private void TrigButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.FindName("SecondFunctionToggle") is ToggleButton secToggle) secToggle.IsChecked = _isTrigSecondFunctionActive;
            if (this.FindName("HyperbolicToggle") is ToggleButton hypToggle) hypToggle.IsChecked = _isHyperbolicActive;
            UpdateTrigButtonLabels();

            TrigPopup.IsOpen = !TrigPopup.IsOpen;

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.IsEnabled = !TrigPopup.IsOpen;
                if (TrigPopup.IsOpen)
                {
                    EventHandler? closedHandler = null;
                    closedHandler = (s, args) => {
                        if (mainWindow != null) mainWindow.IsEnabled = true;
                        if (TrigPopup != null) TrigPopup.Closed -= closedHandler;
                    };
                    TrigPopup.Closed += closedHandler;
                }
            }
        }

        private void FunctionButton_Click(object? sender, RoutedEventArgs e)
        {
            FunctionPopup.IsOpen = !FunctionPopup.IsOpen;

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.IsEnabled = !FunctionPopup.IsOpen;
                if (FunctionPopup.IsOpen)
                {
                    EventHandler? closedHandler = null;
                    closedHandler = (s, args) => {
                        if (mainWindow != null) mainWindow.IsEnabled = true;
                        if (FunctionPopup != null) FunctionPopup.Closed -= closedHandler;
                    };
                    FunctionPopup.Closed += closedHandler;
                }
            }
        }

        private void SecondFunctionToggle_Click(object? sender, RoutedEventArgs e)
        {
            _isTrigSecondFunctionActive = (sender as ToggleButton)?.IsChecked ?? false;
            UpdateTrigButtonLabels();
            UpdateSecondFunctionVisuals();
        }

        private void HyperbolicToggle_Click(object? sender, RoutedEventArgs e)
        {
            _isHyperbolicActive = (sender as ToggleButton)?.IsChecked ?? false;
            UpdateTrigButtonLabels();
            UpdateHyperbolicVisuals();
        }

        private void TrigButton_ItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string baseTag)
            {
                string funcName = "";
                bool isHyp = _isHyperbolicActive;
                bool isInv = _isTrigSecondFunctionActive;

                funcName = (isInv, isHyp) switch
                {
                    (true, true) => baseTag switch { "sin" => "asinh", "cos" => "acosh", "tan" => "atanh", "sec" => "asech", "csc" => "acsch", "cot" => "acoth", _ => "" },
                    (true, false) => baseTag switch { "sin" => "asin", "cos" => "acos", "tan" => "atan", "sec" => "asec", "csc" => "acsc", "cot" => "acot", _ => "" },
                    (false, true) => baseTag switch { "sin" => "sinh", "cos" => "cosh", "tan" => "tanh", "sec" => "sech", "csc" => "csch", "cot" => "coth", _ => "" },
                    (false, false) => baseTag switch { "sin" => "sin", "cos" => "cos", "tan" => "tan", "sec" => "sec", "csc" => "csc", "cot" => "cot", _ => "" },
                };

                if (!string.IsNullOrEmpty(funcName))
                {
                    if (isHyp) PerformHyperbolicFunction(funcName);
                    else PerformTrigFunction(funcName);
                }
            }
            TrigPopup.IsOpen = false;

            var mainWindow = GetMainWindow();
            if (mainWindow != null) mainWindow.IsEnabled = true;
        }

        private void FunctionButton_ItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                switch (tag)
                {
                    case "abs": InputUnaryFunction("|x|"); break;
                    case "floor": InputUnaryFunction("floor"); break;
                    case "ceil": InputUnaryFunction("ceil"); break;
                    case "rand": InputUnaryFunction("rand"); break;
                    case "todms": ConvertToDms(); break;
                    case "todeg": ConvertToDeg(); break;
                    case "Factorial": InputUnaryFunction("n!"); break;
                    case "TenPowerX": InputUnaryFunction("10ˣ"); break;
                    case "Log10": InputUnaryFunction("log"); break;
                    case "Ln": InputUnaryFunction("ln"); break;
                }
            }
            FunctionPopup.IsOpen = false;

            var mainWindow = GetMainWindow();
            if (mainWindow != null) mainWindow.IsEnabled = true;
        }

        private void UnaryFunction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                string funcName = tag switch
                {
                    "Square" => _isMainSecondFunctionActive ? "x³" : "x²",
                    "Cube" => _isMainSecondFunctionActive ? "x³" : "x²",
                    "RootX" => _isMainSecondFunctionActive ? "³√x" : "√x",
                    "CubeRoot" => _isMainSecondFunctionActive ? "³√x" : "√x",
                    "Reciprocal" => "¹/ₓ",
                    "Abs" => "|x|",
                    "Factorial" => "n!",
                    "TenPowerX" => _isMainSecondFunctionActive ? "eˣ" : "10ˣ",
                    "EPowerX" => _isMainSecondFunctionActive ? "eˣ" : "10ˣ",
                    "Log10" => _isMainSecondFunctionActive ? "log₂x" : "log",
                    "LogBase2" => _isMainSecondFunctionActive ? "log₂x" : "log",
                    "Ln" => "ln",
                    _ => ""
                };

                if (tag == "Ln" && _isMainSecondFunctionActive)
                {
                    InputOperator("logʸx");
                    return;
                }

                if (!string.IsNullOrEmpty(funcName))
                {
                    InputUnaryFunction(funcName);
                }
            }
        }

        private void BinaryScientificOp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string? opContent = btn.Content?.ToString();
                string? opTag = btn.Tag?.ToString();
                string operation = "";

                if (opTag == "PowerY" || opContent == "xʸ" || opContent == "ʸ√x")
                {
                    operation = _isMainSecondFunctionActive ? "ʸ√x" : "xʸ";
                }
                else if (opTag == "LogBaseY" || opContent == "logʸx")
                {
                    operation = "logʸx";
                }
                else if (opContent == "mod")
                {
                    operation = "mod";
                }

                if (!string.IsNullOrEmpty(operation))
                {
                    InputOperator(operation);
                }
            }
        }

        private void AngleModeButton_Click(object sender, RoutedEventArgs e)
        {
            _currentAngleMode = _currentAngleMode switch
            {
                AngleMode.DEG => AngleMode.RAD,
                AngleMode.RAD => AngleMode.GRAD,
                AngleMode.GRAD => AngleMode.DEG,
                _ => AngleMode.DEG
            };

            if (sender is Button btn)
            {
                btn.Content = _currentAngleMode.ToString();
            }
        }

        private void UpdateMainGridSecondFunctions()
        {
            Button? squareButton = this.FindName("SquareButton") as Button;
            Button? sqrtButton = this.FindName("SqrtButton") as Button;
            Button? tenPowerXButton = this.FindName("TenPowerXButton") as Button;
            Button? log10Button = this.FindName("Log10Button") as Button;
            Button? lnButton = this.FindName("LnButton") as Button;
            Button? powerYButton = this.FindName("PowerYButton") as Button;

            var mappings = new Dictionary<Button?, (string Normal, string Second, string NormalTag, string SecondTag)>
            {
                { squareButton,    ("x²",   "x³",    "Square",   "Cube") },
                { sqrtButton,      ("√x",   "³√x",   "RootX",    "CubeRoot") },
                { tenPowerXButton, ("10ˣ",  "eˣ",    "TenPowerX","EPowerX") },
                { log10Button,     ("log",  "log₂x", "Log10",    "LogBase2") },
                { lnButton,        ("ln",   "logʸx", "Ln",       "LogBaseY") },
                { powerYButton,    ("xʸ",   "ʸ√x",   "PowerY",   "YRootX") }
            };

            foreach (var kvp in mappings)
            {
                Button? btn = kvp.Key;
                if (btn != null)
                {
                    (string normalContent, string secondContent, string normalTag, string secondTag) = kvp.Value;
                    if (_isMainSecondFunctionActive)
                    {
                        btn.Content = secondContent;
                        btn.Tag = secondTag;
                    }
                    else
                    {
                        btn.Content = normalContent;
                        btn.Tag = normalTag;
                    }
                }
            }
        }

        public void UpdateMemoryButtonState()
        {
            UpdateMemoryButtonsState();
        }
        private void MainWindow_MemoryChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine($"Page ({this.GetType().Name}): Received MemoryChanged event.");
            UpdateMemoryButtonsState();
        }
    }
}
