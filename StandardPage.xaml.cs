using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FinalCalcuEDP
{
    public partial class StandardPage : Page, ICalculatorPage
    {
        private string _expression = "";   
        private bool _isNewEntry = true;    
        private bool _showingFinalResult = false; 

        public StandardPage()
        {
            InitializeComponent();
            this.Loaded += StandardPage_Loaded;
            this.Unloaded += StandardPage_Unloaded; 
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.MemoryChanged += (s, e) => UpdateMemoryButtonsState();
            }
            UpdateMemoryButtonsState();

            ClearAll();
        }

        private void StandardPage_Loaded(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.MemoryChanged += MainWindow_MemoryChanged;
            }
            UpdateMemoryButtonsState(); 
        }

        private void StandardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.MemoryChanged -= MainWindow_MemoryChanged;
            }
        }

        private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;

        private void UpdateDisplay(string text) => GetMainWindow()?.UpdateDisplay(text);

        private void UpdateExpression(string text) => GetMainWindow()?.UpdateExpression(text);

        private string GetDisplayText() => GetMainWindow()?.GetDisplayText() ?? "0";

        private string GetExpressionText() => GetMainWindow()?.GetExpressionText() ?? "";

        public void InputDigit(string? digit)
        {
            if (digit == null) return;

            if (_isNewEntry || _showingFinalResult)
            {
                UpdateDisplay(digit == "." ? "0." : digit);
                _isNewEntry = false;
                _showingFinalResult = false; 
                if (_showingFinalResult) UpdateExpression("");
            }
            else
            {
                string currentDisplay = GetDisplayText();
                if (digit == "." && currentDisplay.Contains(".")) return;
               
                if (currentDisplay == "0" && digit != ".")
                {
                    UpdateDisplay(digit);
                }
                else
                {
                    UpdateDisplay(currentDisplay + digit);
                }
            }
        }

        public void InputOperator(string? op)
        {
            if (string.IsNullOrWhiteSpace(op)) return;

            string currentDisplay = GetDisplayText();

            if (_showingFinalResult)
            {
                _expression = currentDisplay;
                _showingFinalResult = false;
            }
            else if (!_isNewEntry)
            {
                _expression += currentDisplay;
            }
            else if (!string.IsNullOrEmpty(_expression) && "+−×÷".Contains(_expression.Trim().Last()))
            {
                _expression = _expression.Trim().Substring(0, _expression.Trim().Length - 1).Trim();
            }


            string internalOp = op switch
            {
                "−" => "-",
                "×" => "*",
                "÷" => "/",
                _ => op
            };
            _expression += $" {internalOp} "; 

            UpdateExpression(_expression); 
            _isNewEntry = true; 
        }

        public void InputSpecialOperator(string? op)
        {
            if (op == "%")
            {
                PerformPercent();
            }
        }

        public void Calculate()
        {
            if (_isNewEntry && !string.IsNullOrEmpty(_expression) && "+-*/".Contains(_expression.Trim().Last()))
                return;

            if (!_isNewEntry)
            {
                _expression += GetDisplayText();
            }

            if (string.IsNullOrWhiteSpace(_expression)) return;


            try
            {
                double result = ExpressionEvaluator.Evaluate(_expression);

                string resultString = result.ToString(CultureInfo.InvariantCulture);

                UpdateDisplay(resultString);
                UpdateExpression($"{_expression} ="); 

                GetMainWindow()?.AddToHistory(GetExpressionText(), GetDisplayText());

                _expression = resultString; 
                _isNewEntry = true;
                _showingFinalResult = true; 
            }
            catch (DivideByZeroException)
            {
                HandleError("Error: Divide by zero");
            }
            catch (Exception ex) 
            {
                HandleError("Error");
            }
        }

        public void ClearAll()
        {
            _expression = "";
            UpdateDisplay("0");
            UpdateExpression("");
            _isNewEntry = true;
            _showingFinalResult = false;
        }

        public void ClearEntry()
        {
            UpdateDisplay("0");
            _isNewEntry = true;
            _showingFinalResult = false; 
        }

        public void Backspace()
        {
            if (_isNewEntry || _showingFinalResult) return;

            string currentDisplay = GetDisplayText();
            if (currentDisplay.Length > 1)
            {
                UpdateDisplay(currentDisplay.Substring(0, currentDisplay.Length - 1));
                if (GetDisplayText() == "-")
                {
                    UpdateDisplay("0");
                    _isNewEntry = true;
                }
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

            if (_showingFinalResult)
            {
                if (double.TryParse(currentDisplay, out double res))
                {
                    UpdateDisplay((-res).ToString(CultureInfo.InvariantCulture));
                    _expression = GetDisplayText(); 
                    _showingFinalResult = false; 
                    _isNewEntry = true; 
                }
                return;
            }

            if (_isNewEntry && currentDisplay == "0") return; 

            if (double.TryParse(currentDisplay, out double number))
            {
                UpdateDisplay((-number).ToString(CultureInfo.InvariantCulture));
                _isNewEntry = false; 
            }
        }


        private void PerformReciprocal()
        {
            string currentDisplay = GetDisplayText();
            if (double.TryParse(currentDisplay, out double number))
            {
                if (number == 0)
                {
                    HandleError("Error: Divide by zero");
                    return;
                }
                double result = 1.0 / number;
                UpdateDisplay(result.ToString(CultureInfo.InvariantCulture));
                UpdateExpression($"1/({currentDisplay})"); 
                _isNewEntry = true; 
                _showingFinalResult = true; 
                _expression = result.ToString(); 
            }
            else HandleError("Error");
        }

        private void PerformSquare()
        {
            string currentDisplay = GetDisplayText();
            if (double.TryParse(currentDisplay, out double number))
            {
                double result = Math.Pow(number, 2);
                UpdateDisplay(result.ToString(CultureInfo.InvariantCulture));
                UpdateExpression($"sqr({currentDisplay})");
                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = result.ToString();
            }
            else HandleError("Error");
        }

        private void PerformSquareRoot()
        {
            string currentDisplay = GetDisplayText();
            if (double.TryParse(currentDisplay, out double number))
            {
                if (number < 0)
                {
                    HandleError("Error: Invalid input");
                    return;
                }
                double result = Math.Sqrt(number);
                UpdateDisplay(result.ToString(CultureInfo.InvariantCulture));
                UpdateExpression($"√({currentDisplay})");
                _isNewEntry = true;
                _showingFinalResult = true;
                _expression = result.ToString();
            }
            else HandleError("Error");
        }

        private void PerformPercent()
        {
            string currentDisplay = GetDisplayText();
            if (double.TryParse(currentDisplay, out double number))
            {
                double result = number / 100.0;


                UpdateDisplay(result.ToString(CultureInfo.InvariantCulture));
                UpdateExpression($"percent({currentDisplay})"); 
                _isNewEntry = true;
                _showingFinalResult = true; 
                _expression = result.ToString();
            }
            else HandleError("Error");
        }

        private void MemoryOperation(string? memOp)
        {
            if (memOp == null) return;

            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;

            if (memOp == "M∨" || memOp == "M⏷") 
            {
                mainWindow.ToggleMemoryPanel();
                return;
            }

            switch (memOp)
            {
                case "MC": 
                    mainWindow.ClearMemory();
                    break;

                case "MR": 
                    if (mainWindow.IsMemorySet) 
                    {
                        double memoryValue = mainWindow.RecallMemory();
                        UpdateDisplay(memoryValue.ToString(CultureInfo.InvariantCulture)); 
                        _isNewEntry = false;        
                        _showingFinalResult = false; 
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
                    }
                    break;

                case "M+":
                    string displayForMPlus = GetDisplayText();
                    if (double.TryParse(displayForMPlus, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentNumberMPlus))
                    {
                        mainWindow.MemoryAdd(currentNumberMPlus);
                        _isNewEntry = true; 
                        _showingFinalResult = false;
                    }
                    break;

                case "M-": 
                    string displayForMMinus = GetDisplayText();
                    if (double.TryParse(displayForMMinus, NumberStyles.Any, CultureInfo.InvariantCulture, out double currentNumberMMinus))
                    {
                        mainWindow.MemorySubtract(currentNumberMMinus);
                        _isNewEntry = true; 
                        _showingFinalResult = false;
                    }
                    break;
            }
            UpdateMemoryButtonsState();
        }

        private void HandleError(string message)
        {
            UpdateDisplay(message);
            UpdateExpression(""); 
            _expression = "";
            _isNewEntry = true;
            _showingFinalResult = false; 
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

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t) yield return t;
                    foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }

        private void Number_Click(object? sender, RoutedEventArgs e) => InputDigit((sender as Button)?.Content?.ToString());
        private void Operation_Click(object? sender, RoutedEventArgs e) => InputOperator((sender as Button)?.Content?.ToString());
        private void Equals_Click(object? sender, RoutedEventArgs e) => Calculate();
        private void Clear_Click(object? sender, RoutedEventArgs e) => ClearAll();
        private void ClearEntry_Click(object? sender, RoutedEventArgs e) => ClearEntry();
        private void Backspace_Click(object? sender, RoutedEventArgs e) => Backspace();
        private void Negate_Click(object? sender, RoutedEventArgs e) => Negate();
        private void Percent_Click(object? sender, RoutedEventArgs e) => PerformPercent();
        private void Reciprocal_Click(object? sender, RoutedEventArgs e) => PerformReciprocal();
        private void Square_Click(object? sender, RoutedEventArgs e) => PerformSquare();
        private void SquareRoot_Click(object? sender, RoutedEventArgs e) => PerformSquareRoot();
        private void Memory_Click(object? sender, RoutedEventArgs e) => MemoryOperation((sender as Button)?.Content?.ToString());

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
