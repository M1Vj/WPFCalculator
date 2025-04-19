using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

namespace FinalCalcuEDP
{
   
    public class CalculatorHistoryItem
    {
        public string Expression { get; set; }
        public string Result { get; set; }
        public DateTime Timestamp { get; set; }
        private long _programmerMemoryValue = 0;
        private bool _isProgrammerMemorySet = false;

        public CalculatorHistoryItem(string expression, string result)
        {
            Expression = expression;
            Result = result;
            Timestamp = DateTime.Now;
        }
        public long ProgrammerMemoryValue
        {
            get => _programmerMemoryValue;
            set => _programmerMemoryValue = value;
        }

        public bool IsProgrammerMemorySet
        {
            get => _isProgrammerMemorySet;
            set
            {
                if (_isProgrammerMemorySet != value)
                {
                    _isProgrammerMemorySet = value;
                }
            }
        }
    }


    public class MemoryItem : System.ComponentModel.INotifyPropertyChanged
    {
        private double _value;
        public double Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }
        public DateTime Timestamp { get; set; }

        public MemoryItem(double value)
        {
            _value = value;
            Timestamp = DateTime.Now;
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, (Type pageType, string title)> _navigationMap;
        private bool _isFlyoutOpen = false;
        private bool _isHistoryPanelOpen = false;
        private bool _isMemoryPanelOpen = false;
        private ObservableCollection<MemoryItem> _memoryItems = new ObservableCollection<MemoryItem>();
        private ObservableCollection<CalculatorHistoryItem> _historyItems = new ObservableCollection<CalculatorHistoryItem>();
        public long _programmerMemoryValue = 0;
        public bool _isProgrammerMemorySet = false;

        public event EventHandler? MemoryChanged;

        public bool IsMemorySet => _memoryItems.Count > 0;

        public delegate void IterateDialogCallback(bool continueIteration);
        private IterateDialogCallback? _iterateCallback;

        public MainWindow()
        {
            InitializeComponent();

            _navigationMap = new Dictionary<string, (Type, string)>
            {
                { "Standard", (typeof(StandardPage), "Standard") },
                { "Scientific", (typeof(ScientificPage), "Scientific") },
                { "Programmer", (typeof(ProgrammerPage), "Programmer") },
                { "Date", (typeof(DatePage), "Date Calculation") }
            };

            HistoryItemsControl.ItemsSource = _historyItems;
            MemoryItemsControl.ItemsSource = _memoryItems; 

            MainFrame.Navigated += MainFrame_Navigated;
            this.Closing += Window_Closing;
            this.KeyDown += MainWindow_KeyDown;

            var initialPage = FindListViewItemByTag("Standard");
            if (initialPage != null)
            {
                NavigationListView.SelectedItem = initialPage;
            }
            else
            {
                NavigateToPage("Standard"); 
            }

            if (NavigationListView.SelectedItem is ListViewItem selectedItem && selectedItem.Tag?.ToString() == "Date")
            {
                Display.Visibility = Visibility.Collapsed;
                ExpressionDisplay.Visibility = Visibility.Collapsed;
                HistoryButton.Visibility = Visibility.Collapsed;
            }
            else if (NavigationListView.SelectedItem is ListViewItem selectedItemStdSci &&
                     (selectedItemStdSci.Tag?.ToString() == "Standard" || selectedItemStdSci.Tag?.ToString() == "Scientific"))
            {
                HistoryButton.Visibility = Visibility.Visible;
            }
            else
            {
                HistoryButton.Visibility = Visibility.Collapsed; 
            }

        }

        public void UpdateDisplay(string text) => Display.Text = text;
        public void UpdateExpression(string text) => ExpressionDisplay.Text = text;
        public string GetDisplayText() => Display.Text;
        public string GetExpressionText() => ExpressionDisplay.Text;

        private ListViewItem? FindListViewItemByTag(string tag)
        {
            foreach (var item in NavigationListView.Items)
            {
                if (item is ListViewItem listViewItem && listViewItem.Tag?.ToString() == tag)
                {
                    return listViewItem;
                }
            }
            return null;
        }

        private void MenuButton_Click(object? sender, RoutedEventArgs e) => ToggleFlyout();
        private void OverlayBorder_MouseDown(object? sender, MouseButtonEventArgs e) => CloseFlyoutIfOpen();

        private void CloseFlyoutIfOpen()
        {
            if (_isFlyoutOpen)
            {
                ToggleFlyout();
            }
        }

        private void ToggleFlyout()
        {
            var flyoutTransform = FlyoutBorder.RenderTransform as TranslateTransform ?? new TranslateTransform();
            FlyoutBorder.RenderTransform = flyoutTransform;

            DoubleAnimation animation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            if (!_isFlyoutOpen)
            {
                FlyoutBorder.Visibility = Visibility.Visible;
                OverlayBorder.Visibility = Visibility.Visible;
                animation.From = -FlyoutBorder.ActualWidth;
                animation.To = 0;
            }
            else
            {
                animation.From = 0;
                animation.To = -FlyoutBorder.ActualWidth; 
                animation.Completed += (s, e) => {
                    FlyoutBorder.Visibility = Visibility.Collapsed;
                    OverlayBorder.Visibility = Visibility.Collapsed;
                };
            }

            flyoutTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            _isFlyoutOpen = !_isFlyoutOpen;
        }


        private void NavigationListView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ListViewItem selectedItem)
            {
                string? tag = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    NavigateToPage(tag);
                    CloseFlyoutIfOpen();
                }
            }
        }

        private void NavigateToPage(string tag)
        {
            if (_navigationMap.TryGetValue(tag, out var pageInfo))
            {
                if (MainFrame.Content?.GetType() != pageInfo.pageType)
                {
                    try
                    {
                        object? instance = Activator.CreateInstance(pageInfo.pageType);
                        if (instance is Page pageInstance)
                        {
                            MainFrame.Navigate(pageInstance);

                            bool isDatePage = pageInfo.pageType == typeof(DatePage);
                            Display.Visibility = isDatePage ? Visibility.Collapsed : Visibility.Visible;
                            ExpressionDisplay.Visibility = isDatePage ? Visibility.Collapsed : Visibility.Visible;

                            bool showHistory = pageInfo.pageType == typeof(StandardPage) || pageInfo.pageType == typeof(ScientificPage);
                            HistoryButton.Visibility = showHistory ? Visibility.Visible : Visibility.Collapsed;

                            if (pageInstance is ICalculatorPage calcPage)
                            {
                                calcPage.ClearAll();
                            }
                            else
                            {
                                if (isDatePage)
                                {
                                    UpdateDisplay("0"); 
                                    UpdateExpression("");
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show($"Error creating page instance for '{pageInfo.title}'.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error navigating to page '{pageInfo.title}': {ex.Message}");
                    }
                }
                ModeTitleTextBlock.Text = pageInfo.title;
            }
            else
            {
                ModeTitleTextBlock.Text = "Unknown";
            }
        }


        private void MainFrame_Navigated(object? sender, NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                page.Opacity = 0;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
                page.BeginAnimation(Page.OpacityProperty, fadeIn);

                string? navigatedTag = FindTagByPageType(page.GetType());
                if (navigatedTag != null)
                {
                    ListViewItem? itemToSelect = FindListViewItemByTag(navigatedTag);
                    if (itemToSelect != null && !itemToSelect.IsSelected)
                    {
                        NavigationListView.SelectionChanged -= NavigationListView_SelectionChanged;
                        NavigationListView.SelectedItem = itemToSelect;
                        NavigationListView.SelectionChanged += NavigationListView_SelectionChanged;
                    }
                    Keyboard.Focus(MainFrame); 
                }
            }
        }


        private string? FindTagByPageType(Type pageType)
        {
            foreach (var kvp in _navigationMap)
            {
                if (kvp.Value.pageType == pageType) return kvp.Key;
            }
            return null;
        }

        private void HistoryButton_Click(object? sender, RoutedEventArgs e)
        {
            ToggleHistoryPanel();
        }

        private void ToggleHistoryPanel()
        {
            var historyTransform = HistoryPanelBorder.RenderTransform as TranslateTransform ?? new TranslateTransform();
            HistoryPanelBorder.RenderTransform = historyTransform;

            DoubleAnimation animation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            if (!_isHistoryPanelOpen)
            {
                HistoryPanelBorder.Visibility = Visibility.Visible;
                HistoryOverlayBorder.Visibility = Visibility.Visible;
                animation.From = HistoryPanelBorder.ActualHeight; 
                animation.To = 0;
            }
            else
            {
                animation.From = 0;
                animation.To = HistoryPanelBorder.ActualHeight; 
                animation.Completed += (s, e) => {
                    HistoryPanelBorder.Visibility = Visibility.Collapsed;
                    HistoryOverlayBorder.Visibility = Visibility.Collapsed;
                };
            }

            historyTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            _isHistoryPanelOpen = !_isHistoryPanelOpen;
        }


        private void CloseHistoryPanel()
        {
            if (_isHistoryPanelOpen)
            {
                ToggleHistoryPanel();
            }
        }

        private void HistoryOverlayBorder_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            CloseHistoryPanel();
        }

        private void CloseHistoryButton_Click(object? sender, RoutedEventArgs e)
        {
            CloseHistoryPanel();
        }

        private void ClearHistoryButton_Click(object? sender, RoutedEventArgs e)
        {
            _historyItems.Clear();
        }

        private void HistoryItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Control control)
            {
                control.Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)); 
            }
            else if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
            }
        }

        private void HistoryItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Control control)
            {
                control.Background = Brushes.Transparent;
            }
            else if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
            }
        }


        private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CalculatorHistoryItem historyItem)
            {
                UpdateDisplay(historyItem.Result);

                UpdateExpression(historyItem.Expression);

                CloseHistoryPanel();

                var currentPage = MainFrame.Content as ICalculatorPage;
                if (currentPage != null)
                {
                    currentPage.ClearEntry(); 
                }
            }
        }

        public void AddToHistory(string expression, string result)
        {
            if (string.IsNullOrWhiteSpace(expression) || expression.Trim() == "=" || result.StartsWith("Error"))
                return;

            if (!string.IsNullOrEmpty(expression) && !string.IsNullOrEmpty(result))
            {
                _historyItems.Insert(0, new CalculatorHistoryItem(expression, result));
          
            }
        }


        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox || e.OriginalSource is DatePicker || e.OriginalSource is PasswordBox)
            {
                if (e.Key == Key.Back && e.OriginalSource is TextBox) {  }
                else return;
            }


            object? currentPage = MainFrame.Content;
            ICalculatorPage? activeCalculator = currentPage as ICalculatorPage;

            if (activeCalculator == null && !(currentPage is DatePage)) 
                return; 

            string keyString = e.Key.ToString();
            bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            string? digit = null;
            string? op = null;

            if (keyString.StartsWith("D") && keyString.Length == 2 && char.IsDigit(keyString[1]) && !shiftPressed)
                digit = keyString[1].ToString();
            else if (keyString.StartsWith("NumPad") && keyString.Length > 6 && char.IsDigit(keyString[^1])) 
                digit = keyString[^1].ToString();
            else if ((e.Key == Key.Decimal || e.Key == Key.OemPeriod) && !(currentPage is ProgrammerPage)) 
                digit = ".";
            else if (e.Key == Key.Add || (e.Key == Key.OemPlus && shiftPressed))
                op = "+";
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                op = "−"; 
            else if (e.Key == Key.Multiply || (e.Key == Key.D8 && shiftPressed))
                op = "×"; 
            else if (e.Key == Key.Divide || e.Key == Key.OemQuestion || e.Key == Key.Oem2) 
                op = "÷";
            else if (e.Key == Key.D5 && shiftPressed && activeCalculator != null) 
                activeCalculator.InputSpecialOperator("%");
            else if (e.Key == Key.Enter || (e.Key == Key.OemPlus && !shiftPressed)) 
            {
                if (activeCalculator != null)
                {
                    activeCalculator.Calculate();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                if (activeCalculator != null) activeCalculator.Backspace();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (activeCalculator != null) activeCalculator.ClearAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (activeCalculator != null) activeCalculator.ClearEntry();
                e.Handled = true;
            }
            else if (e.Key == Key.F9) 
            {
                if (activeCalculator != null) activeCalculator.Negate();
                e.Handled = true;
            }
            else if (e.Key >= Key.A && e.Key <= Key.F && activeCalculator is ProgrammerPage programmerPage)
            {
                programmerPage.InputDigit(keyString); 
                e.Handled = true;
            }

            if (digit != null && activeCalculator != null)
            {
                activeCalculator.InputDigit(digit);
                e.Handled = true;
            }
            else if (op != null && activeCalculator != null)
            {
                activeCalculator.InputOperator(op);
                e.Handled = true;
            }
        }

        public void ToggleMemoryPanel()
        {
            var memoryTransform = MemoryPanelBorder.RenderTransform as TranslateTransform ?? new TranslateTransform();
            MemoryPanelBorder.RenderTransform = memoryTransform;

            DoubleAnimation animation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            if (!_isMemoryPanelOpen)
            {
                MemoryPanelBorder.Visibility = Visibility.Visible;
                MemoryOverlayBorder.Visibility = Visibility.Visible;
                animation.From = MemoryPanelBorder.ActualHeight; 
                animation.To = 0;
            }
            else
            {
                animation.From = 0;
                animation.To = MemoryPanelBorder.ActualHeight; 
                animation.Completed += (s, e) => {
                    MemoryPanelBorder.Visibility = Visibility.Collapsed;
                    MemoryOverlayBorder.Visibility = Visibility.Collapsed;
                };
            }

            memoryTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            _isMemoryPanelOpen = !_isMemoryPanelOpen;
        }


        private void CloseMemoryPanel()
        {
            if (_isMemoryPanelOpen)
            {
                ToggleMemoryPanel();
            }
        }

        private void MemoryOverlayBorder_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            CloseMemoryPanel();
        }

        private void CloseMemoryButton_Click(object? sender, RoutedEventArgs e)
        {
            CloseMemoryPanel();
        }

        private void ClearMemoryButton_Click(object? sender, RoutedEventArgs e)
        {
            ClearMemory();
        }

        public void ClearMemory()
        {
            _memoryItems.Clear();
            MemoryChanged?.Invoke(this, EventArgs.Empty); 
            (MainFrame.Content as ICalculatorPage)?.UpdateMemoryButtonState();
            Debug.WriteLine("MainWindow: Firing MemoryChanged."); 
            MemoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public double RecallMemory()
        {
            if (_memoryItems.Count > 0)
                return _memoryItems[0].Value; 
            return 0.0; 
        }

        public void MemoryAdd()
        {
            string displayTextValue = GetDisplayText();
            if (!double.TryParse(displayTextValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double displayValue)) 
                return;
            MemoryAdd(displayValue); 
        }


        public void MemorySubtract()
        {
            string displayTextValue = GetDisplayText();
            if (!double.TryParse(displayTextValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double displayValue)) 
                return;
            MemorySubtract(displayValue);
        }

        public void MemoryStore() 
        {
            string displayTextValue = GetDisplayText();
            if (!double.TryParse(displayTextValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double displayValue)) 
                return;
            AddToMemory(displayValue); 
        }

        public void AddToMemory(double value)
        {
            _memoryItems.Insert(0, new MemoryItem(value));

            const int maxMemoryItems = 10; 
            if (_memoryItems.Count > maxMemoryItems)
            {
                _memoryItems.RemoveAt(_memoryItems.Count - 1); 
            }

            MemoryChanged?.Invoke(this, EventArgs.Empty);
            (MainFrame.Content as ICalculatorPage)?.UpdateMemoryButtonState();
        }

        public void MemoryAdd(double value)
        {
            if (_memoryItems.Count > 0)
            {
                _memoryItems[0].Value += value; 
            }
            else
            {
                _memoryItems.Add(new MemoryItem(value)); 
            }
            MemoryChanged?.Invoke(this, EventArgs.Empty);
            (MainFrame.Content as ICalculatorPage)?.UpdateMemoryButtonState();
        }


        public void MemorySubtract(double value)
        {
            if (_memoryItems.Count > 0)
            {
                _memoryItems[0].Value -= value; 
            }
            else
            {
                _memoryItems.Add(new MemoryItem(-value)); 
            }
            MemoryChanged?.Invoke(this, EventArgs.Empty);
        }


        private void MemoryItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Control control)
            {
                control.Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
            }
            else if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
            }
        }

        private void MemoryItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Control control)
            {
                control.Background = Brushes.Transparent;
            }
            else if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
            }
        }

        private void MemoryItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MemoryItem memoryItem)
            {
                var currentPage = MainFrame.Content;

                if (currentPage is ProgrammerPage programmerPage) 
                {
                    programmerPage.RecallProgrammerMemoryValue(memoryItem.Value);
                }
                else if (currentPage is ICalculatorPage calculatorPage) 
                {
                    UpdateDisplay(memoryItem.Value.ToString(CultureInfo.InvariantCulture));
                    calculatorPage.ClearEntry(); 
                }
            }
        }

        private void MemoryItemAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MemoryItem memoryItem)
            {
                string displayTextValue = GetDisplayText();
                double valueToAdd = 0;
                bool parseSuccess = false;

                if (MainFrame.Content is ProgrammerPage progPage)
                {
                    try
                    {
                        long longVal = progPage.ConvertToLong(displayTextValue, progPage._currentBase); 
                        valueToAdd = (double)longVal; 
                        parseSuccess = true;
                    }
                    catch { parseSuccess = false; } 
                }
                else 
                {
                    parseSuccess = double.TryParse(displayTextValue, NumberStyles.Any, CultureInfo.InvariantCulture, out valueToAdd);
                }


                if (parseSuccess)
                {
                    memoryItem.Value += valueToAdd;
                }
            }
        }

        private void MemoryItemSubtract_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MemoryItem memoryItem)
            {
                string displayTextValue = GetDisplayText();
                double valueToSubtract = 0;
                bool parseSuccess = false;

                if (MainFrame.Content is ProgrammerPage progPage)
                {
                    try
                    {
                        long longVal = progPage.ConvertToLong(displayTextValue, progPage._currentBase); 
                        valueToSubtract = (double)longVal; 
                        parseSuccess = true;
                    }
                    catch { parseSuccess = false; }
                }
                else 
                {
                    parseSuccess = double.TryParse(displayTextValue, NumberStyles.Any, CultureInfo.InvariantCulture, out valueToSubtract);
                }

                if (parseSuccess)
                {
                    memoryItem.Value -= valueToSubtract;
                }
            }
        }

        private void MemoryItemClear_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MemoryItem memoryItem) 
            {
                _memoryItems.Remove(memoryItem);
                MemoryChanged?.Invoke(this, EventArgs.Empty); 
                (MainFrame.Content as ICalculatorPage)?.UpdateMemoryButtonState();
            }
        }

        public void ShowIterateDialog(string message, IterateDialogCallback callback)
        {
            _iterateCallback = callback;
            IterateMessageText.Text = message;
            IterateDialogOverlay.Visibility = Visibility.Visible;
        }

        private void IterateYesButton_Click(object sender, RoutedEventArgs e)
        {
            IterateDialogOverlay.Visibility = Visibility.Collapsed;
            _iterateCallback?.Invoke(true); 
            _iterateCallback = null; 
        }

        private void IterateNoButton_Click(object sender, RoutedEventArgs e)
        {
            IterateDialogOverlay.Visibility = Visibility.Collapsed;
            _iterateCallback?.Invoke(false); 
            _iterateCallback = null; 
        }

    }

    public interface ICalculatorPage
    {
        void InputDigit(string digit);
        void InputOperator(string op);
        void InputSpecialOperator(string op); 
        void Calculate(); 
        void ClearAll(); 
        void ClearEntry(); 
        void Backspace();
        void Negate(); 
        void UpdateMemoryButtonState();
    }
}