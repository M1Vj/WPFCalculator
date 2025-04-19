using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FinalCalcuEDP
{
    public partial class ProgrammerPage : Page, ICalculatorPage
    {
        public enum BaseMode { HEX, DEC, OCT, BIN }
        public BaseMode _currentBase = BaseMode.DEC;

        private long _currentValue = 0;
        private long _operand1 = 0;
        private string _pendingOperation = string.Empty;
        private bool _isNewEntry = true;
        private bool _showingFinalResult = false;
        private string _expression = string.Empty;

        private string _currentBitShiftType = "ARITHMETIC";
        private string _currentWordSize = "QWORD";
        private bool _carryFlag = false;

        private readonly Dictionary<BaseMode, string> _validDigits = new Dictionary<BaseMode, string> {
            { BaseMode.BIN, "01" }, { BaseMode.OCT, "01234567" },
            { BaseMode.DEC, "0123456789" }, { BaseMode.HEX, "0123456789ABCDEF" }
        };

        public ProgrammerPage()
        {
            InitializeComponent();
            this.Loaded += ProgrammerPage_Loaded;
            this.Unloaded += ProgrammerPage_Unloaded;
            UpdateBaseModeUI();
            UpdateAllValueDisplays();
            UpdateMemoryButtonsState();
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                UpdateMemoryButtonsState();
            }
            else
            {
            }
            BitWisePopup.IsOpen = false;
            BitShiftPopup.IsOpen = false;
        }

        private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;
        private void UpdateDisplay(string text) => GetMainWindow()?.UpdateDisplay(text);
        private void UpdateExpression(string text) => GetMainWindow()?.UpdateExpression(text);
        private string GetDisplayText() => GetMainWindow()?.GetDisplayText() ?? "0";
        private string GetExpressionText() => GetMainWindow()?.GetExpressionText() ?? "";

        private void ProgrammerPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBaseModeUI();
            UpdateAllValueDisplays();
            UpdateMemoryButtonsState();
        }

        private void ProgrammerPage_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        public void InputDigit(string? digit)
        {
            if (digit == null) return;
            string upperDigit = digit.ToUpperInvariant();
            if (!_validDigits[_currentBase].Contains(upperDigit)) return;
            if (_isNewEntry || _showingFinalResult)
            {
                UpdateDisplay(upperDigit);
                _isNewEntry = false;
                if (_showingFinalResult) UpdateExpression("");
                _showingFinalResult = false;
            }
            else
            {
                string currentDisplay = GetDisplayText();
                if (currentDisplay == "0")
                {
                    UpdateDisplay(upperDigit);
                }
                else
                {
                    UpdateDisplay(currentDisplay + upperDigit);
                }
            }
            _currentValue = ConvertToLong(GetDisplayText(), _currentBase);
            UpdateAllValueDisplays();
        }

        public void InputOperator(string? op)
        {
            if (string.IsNullOrWhiteSpace(op)) return;
            if (op == "NOT")
            {
                PerformNotOperation();
                return;
            }
            if (_showingFinalResult)
            {
                _operand1 = _currentValue;
                _expression = ConvertToString(_operand1, _currentBase);
                _showingFinalResult = false;
            }
            else if (!_isNewEntry && !string.IsNullOrEmpty(_pendingOperation))
            {
                PerformCalculation();
                _operand1 = _currentValue;
                _expression = ConvertToString(_operand1, _currentBase);
            }
            else if (!_isNewEntry)
            {
                _operand1 = ConvertToLong(GetDisplayText(), _currentBase);
                _expression = ConvertToString(_operand1, _currentBase);
            }
            else if (!string.IsNullOrEmpty(_pendingOperation))
            {
                int lastOpIndex = _expression.Trim().LastIndexOf(' ');
                if (lastOpIndex > 0)
                    _expression = _expression.Substring(0, lastOpIndex).Trim();
                else
                    _expression = ConvertToString(_operand1, _currentBase);
            }
            else
            {
                _operand1 = _currentValue;
                _expression = ConvertToString(_operand1, _currentBase);
            }
            _pendingOperation = op;
            _expression += $" {op} ";
            UpdateExpression(_expression);
            _isNewEntry = true;
        }

        public void Calculate()
        {
            PerformCalculation();
            if (!GetDisplayText().StartsWith("Error"))
            {
                GetMainWindow()?.AddToHistory(GetExpressionText(), GetDisplayText());
                _expression = ConvertToString(_currentValue, _currentBase);
            }
            else
            {
                _expression = "";
            }
            _isNewEntry = true;
            _showingFinalResult = true;
            _pendingOperation = string.Empty;
        }

        private void PerformCalculation()
        {
            if (string.IsNullOrEmpty(_pendingOperation)) return;
            if (_isNewEntry && !_showingFinalResult)
            {
                return;
            }
            long operand2 = _currentValue;
            try
            {
                long result = 0;
                int shiftAmount = (int)(operand2 & 0x3F);
                switch (_pendingOperation)
                {
                    case "+": result = checked(_operand1 + operand2); break;
                    case "−": result = checked(_operand1 - operand2); break;
                    case "×": result = checked(_operand1 * operand2); break;
                    case "÷": if (operand2 == 0) throw new DivideByZeroException(); result = _operand1 / operand2; break;
                    case "%": if (operand2 == 0) throw new DivideByZeroException(); result = _operand1 % operand2; break;
                    case "AND": result = _operand1 & operand2; break;
                    case "OR": result = _operand1 | operand2; break;
                    case "XOR": result = _operand1 ^ operand2; break;
                    case "NAND": result = ~(_operand1 & operand2); break;
                    case "NOR": result = ~(_operand1 | operand2); break;
                    case "<<": result = _operand1 << shiftAmount; break;
                    case ">>": result = _operand1 >> shiftAmount; break;
                    case "LOGICAL_LEFT": result = _operand1 << shiftAmount; break;
                    case "LOGICAL_RIGHT": result = LogicalRightShift(_operand1, shiftAmount); break;
                    case "ROL": result = RotateLeft(_operand1, shiftAmount); break;
                    case "ROR": result = RotateRight(_operand1, shiftAmount); break;
                    case "RCL": result = RotateLeftWithCarry(_operand1, shiftAmount); break;
                    case "RCR": result = RotateRightWithCarry(_operand1, shiftAmount); break;
                    default: return;
                }
                _currentValue = ApplyWordSizeLimit(result);
                _operand1 = _currentValue;
                if (!_expression.TrimEnd().EndsWith("="))
                {
                    _expression += $"{ConvertToString(operand2, _currentBase)} =";
                    UpdateExpression(_expression);
                }
                UpdateAllValueDisplays();
            }
            catch (DivideByZeroException) { HandleError("Error: Division by zero"); }
            catch (OverflowException) { HandleError("Error: Overflow"); }
            catch (Exception) { HandleError("Error"); }
        }

        public void ClearAll()
        {
            _currentValue = 0; _operand1 = 0; _pendingOperation = string.Empty;
            _expression = string.Empty; _carryFlag = false;
            UpdateExpression(""); UpdateDisplay("0");
            _isNewEntry = true; _showingFinalResult = false;
            UpdateAllValueDisplays();
        }

        public void ClearEntry()
        {
            _currentValue = 0;
            UpdateDisplay("0");
            _isNewEntry = true;
            _showingFinalResult = false;
            UpdateAllValueDisplays();
        }

        public void Backspace()
        {
            if (_isNewEntry || _showingFinalResult) return;
            string currentDisplay = GetDisplayText();
            string newValueStr = "0";
            if (currentDisplay.Length > 1)
                newValueStr = currentDisplay.Substring(0, currentDisplay.Length - 1);
            UpdateDisplay(newValueStr);
            _currentValue = ConvertToLong(newValueStr, _currentBase);
            UpdateAllValueDisplays();
            if (newValueStr == "0") _isNewEntry = true;
        }

        public void Negate()
        {
            _currentValue = ApplyWordSizeLimit(-_currentValue);
            UpdateAllValueDisplays();
            _isNewEntry = false;
            if (_showingFinalResult)
            {
                _expression = ConvertToString(_currentValue, _currentBase);
                UpdateExpression("");
                _showingFinalResult = false;
            }
            else
            {
                UpdateDisplay(ConvertToString(_currentValue, _currentBase));
            }
        }

        private void PerformNotOperation()
        {
            long valueToNegate = _isNewEntry ? _operand1 : _currentValue;
            _currentValue = ApplyWordSizeLimit(~valueToNegate);
            UpdateAllValueDisplays();
            string operandStr = ConvertToString(valueToNegate, _currentBase);
            UpdateExpression($"NOT({operandStr}) =");
            _isNewEntry = true;
            _showingFinalResult = true;
            _pendingOperation = string.Empty;
            _expression = ConvertToString(_currentValue, _currentBase);
            _operand1 = _currentValue;
        }

        private void BaseMode_Checked(object? sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rbtn && rbtn.IsChecked == true && rbtn.Tag is string tag)
            {
                if (Enum.TryParse<BaseMode>(tag, true, out BaseMode newBase))
                {
                    _currentBase = newBase;
                    UpdateBaseModeUI();
                    UpdateAllValueDisplays();
                }
            }
        }

        private void WordSizeButton_Click(object sender, RoutedEventArgs e)
        {
            _currentWordSize = _currentWordSize switch
            {
                "QWORD" => "DWORD",
                "DWORD" => "WORD",
                "WORD" => "BYTE",
                "BYTE" => "QWORD",
                _ => "QWORD"
            };
            if (WordSizeButton != null) WordSizeButton.Content = _currentWordSize;
            _currentValue = ApplyWordSizeLimit(_currentValue);
            _operand1 = ApplyWordSizeLimit(_operand1);
            UpdateAllValueDisplays();
        }

        private void BitShiftButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                BitShiftPopup.PlacementTarget = btn;
                BitShiftPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                BitShiftPopup.IsOpen = true;
                DisableMainWindowInteraction(true);
            }
        }

        private void BitShift_RadioChecked(object? sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.IsChecked == true && radioButton.Tag is string shiftType)
            {
                _currentBitShiftType = shiftType;
                string buttonText = shiftType switch
                {
                    "ARITHMETIC" => "Lsh/Rsh",
                    "LOGICAL" => "Logical",
                    "ROTATE" => "Rotate",
                    "ROTATE_CARRY" => "Rotate Carry",
                    _ => "Bit Shift"
                };
                if (this.FindName("BitShiftButton") is Button mainBtn)
                {
                    mainBtn.Content = buttonText + " ▼";
                }
            }
            BitShiftPopup.IsOpen = false;
            DisableMainWindowInteraction(false);
        }

        private void BitShift_Click(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || string.IsNullOrEmpty(button.Content?.ToString()))
                return;
            string shiftDirection = button.Content.ToString();
            string operation = (_currentBitShiftType, shiftDirection) switch
            {
                ("ARITHMETIC", "<<") => "<<",
                ("ARITHMETIC", ">>") => ">>",
                ("LOGICAL", "<<") => "LOGICAL_LEFT",
                ("LOGICAL", ">>") => "LOGICAL_RIGHT",
                ("ROTATE", "<<") => "ROL",
                ("ROTATE", ">>") => "ROR",
                ("ROTATE_CARRY", "<<") => "RCL",
                ("ROTATE_CARRY", ">>") => "RCR",
                (_, _) => shiftDirection
            };
            InputOperator(operation);
        }

        private void BitWiseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                BitWisePopup.PlacementTarget = btn;
                BitWisePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                BitWisePopup.IsOpen = true;
                DisableMainWindowInteraction(true);
            }
        }

        private void BitWiseButton_ItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string op)
            {
                InputOperator(op);
            }
            BitWisePopup.IsOpen = false;
            DisableMainWindowInteraction(false);
        }

        private void DisableMainWindowInteraction(bool disable)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.IsEnabled = !disable;
                if (disable)
                {
                    if (BitWisePopup.IsOpen)
                    {
                        EventHandler? closedHandler = null;
                        closedHandler = (s, args) => {
                            if (mainWindow != null) mainWindow.IsEnabled = true;
                            BitWisePopup.Closed -= closedHandler;
                        };
                        BitWisePopup.Closed += closedHandler;
                    }
                    if (BitShiftPopup.IsOpen)
                    {
                        EventHandler? closedHandler = null;
                        closedHandler = (s, args) => {
                            if (mainWindow != null) mainWindow.IsEnabled = true;
                            BitShiftPopup.Closed -= closedHandler;
                        };
                        BitShiftPopup.Closed += closedHandler;
                    }
                }
            }
        }

        private void MemoryProgrammer_Click(object? sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;
            if (sender is Button button && button.Content is string memOp)
            {
                switch (memOp)
                {
                    case "MS":
                        mainWindow._programmerMemoryValue = _currentValue;
                        mainWindow._isProgrammerMemorySet = true;
                        _isNewEntry = true;
                        _showingFinalResult = false;
                        break;
                    case "M∨":
                        mainWindow.ToggleMemoryPanel();
                        break;
                }
            }
        }

        public void RecallProgrammerMemoryValue(double valueFromPanel)
        {
            try
            {
                long recalledLong = Convert.ToInt64(Math.Round(valueFromPanel));
                _currentValue = ApplyWordSizeLimit(recalledLong);
            }
            catch (OverflowException)
            {
                HandleError("Error: Memory value too large");
                return;
            }
            catch
            {
                HandleError("Error: Invalid memory value");
                return;
            }
            UpdateAllValueDisplays();
            _isNewEntry = false;
            _showingFinalResult = false;
            _expression = "";
            UpdateExpression("");
            _pendingOperation = string.Empty;
            _operand1 = 0;
        }

        private void UpdateMemoryButtonsState()
        {
            var mainWindow = GetMainWindow();
            bool memorySet = mainWindow?._isProgrammerMemorySet ?? false;
            if (this.FindName("MemoryDropdownButton") is Button mdButton)
            {
                mdButton.IsEnabled = memorySet;
            }
        }

        private long ApplyWordSizeLimit(long value)
        {
            return _currentWordSize switch
            {
                "BYTE" => (sbyte)value,
                "WORD" => (short)value,
                "DWORD" => (int)value,
                "QWORD" => value,
                _ => value
            };
        }

        public long ConvertToLong(string value, BaseMode fromBase)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            try
            {
                long val = fromBase switch
                {
                    BaseMode.BIN => Convert.ToInt64(value, 2),
                    BaseMode.OCT => Convert.ToInt64(value, 8),
                    BaseMode.DEC => Convert.ToInt64(value, 10),
                    BaseMode.HEX => Convert.ToInt64(value, 16),
                    _ => 0
                };
                return val;
            }
            catch { return 0; }
        }

        private string ConvertToString(long value, BaseMode toBase)
        {
            try
            {
                long displayValue = ApplyWordSizeLimit(value);
                if (toBase == BaseMode.DEC)
                    return displayValue.ToString();
                if (displayValue == 0)
                    return "0";
                string rawStr;
                int groupSize;
                switch (toBase)
                {
                    case BaseMode.BIN:
                        rawStr = Convert.ToString(displayValue, 2);
                        groupSize = 4;
                        break;
                    case BaseMode.OCT:
                        rawStr = Convert.ToString(displayValue, 8);
                        groupSize = 3;
                        break;
                    case BaseMode.HEX:
                        rawStr = displayValue.ToString("X");
                        groupSize = 4;
                        break;
                    default: return "Error";
                }
                List<string> groups = new List<string>();
                for (int i = rawStr.Length; i > 0; i -= groupSize)
                {
                    int start = Math.Max(0, i - groupSize);
                    groups.Insert(0, rawStr.Substring(start, i - start));
                }
                return string.Join(" ", groups);
            }
            catch { return "Error"; }
        }

        private string FormatForSideDisplay(long value, BaseMode toBase)
        {
            try
            {
                long displayValue = ApplyWordSizeLimit(value);
                int bits = GetBitCount();
                string formattedStr = "";
                int groupSize = 4;
                switch (toBase)
                {
                    case BaseMode.BIN:
                        ulong ulongBinVal = (ulong)displayValue;
                        formattedStr = Convert.ToString((long)ulongBinVal, 2).PadLeft(bits, '0');
                        groupSize = 4;
                        break;
                    case BaseMode.OCT:
                        int octDigits = (int)Math.Ceiling((double)bits / 3.0);
                        ulong ulongOctVal = (ulong)displayValue;
                        ulong octMask = (bits == 64) ? ulong.MaxValue : (1UL << bits) - 1;
                        ulongOctVal &= octMask;
                        formattedStr = Convert.ToString((long)ulongOctVal, 8).PadLeft(octDigits, '0');
                        groupSize = 3;
                        break;
                    case BaseMode.DEC:
                        return displayValue.ToString();
                    case BaseMode.HEX:
                        int hexDigits = bits / 4;
                        formattedStr = displayValue.ToString("X").PadLeft(hexDigits, '0');
                        groupSize = 4;
                        break;
                    default: return "Error";
                }
                if (string.IsNullOrEmpty(formattedStr)) return "0";
                List<string> groups = new List<string>();
                for (int i = formattedStr.Length; i > 0; i -= groupSize)
                {
                    int start = Math.Max(0, i - groupSize);
                    groups.Insert(0, formattedStr.Substring(start, i - start));
                }
                return string.Join(" ", groups);
            }
            catch { return "Error"; }
        }

        private int GetBitCount() => _currentWordSize switch
        {
            "BYTE" => 8,
            "WORD" => 16,
            "DWORD" => 32,
            "QWORD" => 64,
            _ => 64
        };
        private int GetHexDigitCount() => GetBitCount() / 4;

        private void UpdateAllValueDisplays()
        {
            UpdateDisplay(ConvertToString(_currentValue, _currentBase));
            if (HexValue != null) HexValue.Text = ConvertToString(_currentValue, BaseMode.HEX);
            if (DecValue != null) DecValue.Text = ConvertToString(_currentValue, BaseMode.DEC);
            if (OctValue != null) OctValue.Text = ConvertToString(_currentValue, BaseMode.OCT);
            if (BinValue != null) BinValue.Text = ConvertToString(_currentValue, BaseMode.BIN);
        }

        private void UpdateBaseModeUI()
        {
            if (!this.IsLoaded) return;
            string allowedDigits = _validDigits[_currentBase];
            Brush? enabledFG = TryFindResource("TextColor") as Brush ?? Brushes.Black;
            Brush? disabledFG = TryFindResource("TextDisabledColor") as Brush ?? Brushes.Gray;
            foreach (var btn in StandardPage.FindVisualChildren<Button>(this))
            {
                if (btn == null) continue;
                string? content = btn.Content?.ToString()?.ToUpperInvariant();
                if (content == null || content.Length != 1) continue;
                if (content == "C" && btn.GetValue(Grid.RowProperty) is int row && btn.GetValue(Grid.ColumnProperty) is int col && row == 0 && col == 3) continue;
                bool isHexLetter = "ABCDEF".Contains(content);
                bool isDecDigit = "0123456789".Contains(content);
                if (isHexLetter || isDecDigit)
                {
                    bool isEnabled = allowedDigits.Contains(content);
                    btn.IsEnabled = isEnabled;
                    btn.Foreground = isEnabled ? enabledFG : disabledFG;
                }
                if (content == ".")
                {
                    btn.IsEnabled = false;
                    btn.Foreground = disabledFG;
                }
            }
            foreach (var rbtn in StandardPage.FindVisualChildren<RadioButton>(this))
            {
                if (rbtn?.GroupName == "BaseMode")
                {
                    TextBlock? tb = StandardPage.FindVisualChildren<TextBlock>(rbtn).FirstOrDefault();
                    if (tb != null)
                    {
                        tb.FontWeight = rbtn.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
                    }
                }
            }
        }

        private void HandleError(string message)
        {
            UpdateDisplay(message); UpdateExpression("");
            _expression = ""; _pendingOperation = string.Empty;
            _operand1 = 0; _currentValue = 0;
            _isNewEntry = true; _showingFinalResult = false;
            UpdateAllValueDisplays();
        }

        private long LogicalRightShift(long value, int count)
        {
            int bits = GetBitCount();
            count &= (bits - 1);
            if (count == 0) return value;
            ulong uval = (ulong)value;
            ulong shifted = uval >> count;
            return (long)shifted;
        }

        private long RotateLeft(long value, int count)
        {
            int bits = GetBitCount();
            count &= (bits - 1);
            if (count == 0) return value;
            ulong uval = (ulong)value;
            ulong result = (uval << count) | (uval >> (bits - count));
            ulong mask = (bits == 64) ? ulong.MaxValue : (1UL << bits) - 1;
            result &= mask;
            return (long)result;
        }

        private long RotateRight(long value, int count)
        {
            int bits = GetBitCount();
            count &= (bits - 1);
            if (count == 0) return value;
            ulong uval = (ulong)value;
            ulong result = (uval >> count) | (uval << (bits - count));
            ulong mask = (bits == 64) ? ulong.MaxValue : (1UL << bits) - 1;
            result &= mask;
            return (long)result;
        }

        private long RotateLeftWithCarry(long value, int count)
        {
            int bits = GetBitCount();
            ulong uval = (ulong)value;
            ulong currentCarry = _carryFlag ? 1UL : 0UL;
            ulong mask = (bits == 64) ? ulong.MaxValue : (1UL << bits) - 1;
            count &= (bits - 1);
            for (int i = 0; i < count; i++)
            {
                ulong highBit = (uval >> (bits - 1)) & 1UL;
                uval <<= 1;
                uval |= currentCarry;
                uval &= mask;
                currentCarry = highBit;
            }
            _carryFlag = (currentCarry != 0);
            return (long)uval;
        }

        private long RotateRightWithCarry(long value, int count)
        {
            int bits = GetBitCount();
            ulong uval = (ulong)value;
            ulong carryMask = (1UL << (bits - 1));
            ulong currentCarry = _carryFlag ? carryMask : 0UL;
            ulong lowBit = 0;
            ulong mask = (bits == 64) ? ulong.MaxValue : (1UL << bits) - 1;
            count &= (bits - 1);
            for (int i = 0; i < count; i++)
            {
                lowBit = uval & 1UL;
                uval >>= 1;
                uval |= currentCarry;
                uval &= mask;
                currentCarry = (lowBit != 0) ? carryMask : 0UL;
            }
            _carryFlag = (lowBit != 0);
            return (long)uval;
        }

        private void BitToggle_Click(object? sender, RoutedEventArgs e) { MessageBox.Show("Bit Toggles Not Implemented"); }
        private void FullKeyboard_Click(object? sender, RoutedEventArgs e) { MessageBox.Show("Full Keyboard Mode Not Implemented"); }
        private void Parenthesis_Click(object? sender, RoutedEventArgs e) { }
        public void InputSpecialOperator(string? op) { if (op == "%") InputOperator("%"); }
        private void Digit_Click(object? sender, RoutedEventArgs e) => InputDigit((sender as Button)?.Content?.ToString());
        private void Operator_Click(object? sender, RoutedEventArgs e) => InputOperator((sender as Button)?.Content?.ToString());
        private void Equals_Click(object? sender, RoutedEventArgs e) => Calculate();
        private void Clear_Click(object? sender, RoutedEventArgs e) => ClearAll();
        private void Backspace_Click(object? sender, RoutedEventArgs e) => Backspace();
        private void Negate_Click(object? sender, RoutedEventArgs e) => Negate();
        public void UpdateMemoryButtonState()
        {
            UpdateMemoryButtonsState();
        }
    }
}