using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FinalCalcuEDP
{
    public partial class DatePage : Page // No ICalculatorPage needed
    {
        private enum DateCalcMode { Difference, AddSubtract }
        private DateCalcMode _currentMode = DateCalcMode.Difference;
        private bool _isLoaded = false;

        public DatePage()
        {
            InitializeComponent();
            this.Loaded += DatePage_Loaded;
            // Set initial state for the mode button/popup
            DateCalcTypeButton.Content = "Difference between dates"; // Initial mode text
            DateCalcTypePopup.IsOpen = false;
        }

        private void DatePage_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure DatePickers have default values or placeholders
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today;

            // Attach event handlers
            FromDatePicker.SelectedDateChanged += DifferenceDate_Changed;
            ToDatePicker.SelectedDateChanged += DifferenceDate_Changed;
            StartDatePicker.SelectedDateChanged += AddSubtractInput_Changed;
            AddRadio.Checked += AddSubtractInput_Changed;
            SubtractRadio.Checked += AddSubtractInput_Changed;
            YearsTextBox.TextChanged += AddSubtractInput_Changed;
            MonthsTextBox.TextChanged += AddSubtractInput_Changed;
            DaysTextBox.TextChanged += AddSubtractInput_Changed;

            // Input validation for text boxes
            YearsTextBox.PreviewTextInput += NumberValidationTextBox;
            MonthsTextBox.PreviewTextInput += NumberValidationTextBox;
            DaysTextBox.PreviewTextInput += NumberValidationTextBox;
            // Prevent pasting non-numeric text
            DataObject.AddPastingHandler(YearsTextBox, OnPaste);
            DataObject.AddPastingHandler(MonthsTextBox, OnPaste);
            DataObject.AddPastingHandler(DaysTextBox, OnPaste);


            _isLoaded = true;
            UpdateUIVisibility(); // Set initial visibility based on _currentMode
            CalculateCurrentMode(); // Perform initial calculation
        }


        // --- Mode Switching ---

        private void DateCalcTypeButton_Click(object sender, RoutedEventArgs e) // Opens Mode Popup
        {
            if (sender is Button btn)
            {
                DateCalcTypePopup.PlacementTarget = btn;
                DateCalcTypePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                DateCalcTypePopup.IsOpen = true;
            }
        }

        private void DateCalcTypeItem_Click(object sender, RoutedEventArgs e) // Handles selection in Mode Popup
        {
            if (sender is Button button && button.Tag is string tag)
            {
                DateCalcMode newMode = (tag == "AddSubtract") ? DateCalcMode.AddSubtract : DateCalcMode.Difference;

                if (newMode != _currentMode) // Only update if mode changed
                {
                    _currentMode = newMode;
                    DateCalcTypeButton.Content = (_currentMode == DateCalcMode.AddSubtract)
                        ? "Add or subtract days"
                        : "Difference between dates";

                    UpdateUIVisibility();
                    CalculateCurrentMode();
                }
            }
            DateCalcTypePopup.IsOpen = false; // Close popup
        }


        private void UpdateUIVisibility()
        {
            if (!_isLoaded) return;
            // Show/hide the relevant sections based on the current mode
            DifferenceSection.Visibility = (_currentMode == DateCalcMode.Difference) ? Visibility.Visible : Visibility.Collapsed;
            AddSubtractSection.Visibility = (_currentMode == DateCalcMode.AddSubtract) ? Visibility.Visible : Visibility.Collapsed;
        }


        // --- Calculation Logic ---

        private void CalculateCurrentMode()
        {
            if (!_isLoaded) return;

            if (_currentMode == DateCalcMode.Difference)
                CalculateDateDifference();
            else // _currentMode == DateCalcMode.AddSubtract
                CalculateAddSubtractDays();
        }

        // --- Difference Calculation ---

        private void DifferenceDate_Changed(object? sender, SelectionChangedEventArgs e)
        {
            // Recalculate only if in Difference mode
            if (_isLoaded && _currentMode == DateCalcMode.Difference)
                CalculateDateDifference();
        }

        private void CalculateDateDifference()
        {
            if (!_isLoaded || ResultDisplayDifference == null || FromDatePicker == null || ToDatePicker == null) return;

            DateTime? fromDate = FromDatePicker.SelectedDate;
            DateTime? toDate = ToDatePicker.SelectedDate;

            // Ensure both dates are selected
            if (!fromDate.HasValue || !toDate.HasValue)
            {
                ResultDisplayDifference.Text = "Select both dates";
                return;
            }

            DateTime start = fromDate.Value.Date; // Use .Date to ignore time part
            DateTime end = toDate.Value.Date;

            // Handle same dates
            if (start == end)
            {
                ResultDisplayDifference.Text = "Same dates";
                return;
            }

            // Determine earlier and later dates
            DateTime earlierDate = (start < end) ? start : end;
            DateTime laterDate = (start > end) ? start : end;

            // Calculate difference in Years, Months, Days
            int years = 0;
            int months = 0;
            int days = 0;

            DateTime tempDate = earlierDate;

            // Calculate full years
            while (tempDate.AddYears(1) <= laterDate)
            {
                years++;
                tempDate = tempDate.AddYears(1);
            }

            // Calculate full months
            while (tempDate.AddMonths(1) <= laterDate)
            {
                months++;
                tempDate = tempDate.AddMonths(1);
            }

            // Remaining days
            days = (laterDate - tempDate).Days;

            // Build the result string (e.g., "1 year, 2 months, 3 days")
            List<string> parts = new List<string>();
            if (years > 0) parts.Add($"{years} year{(years > 1 ? "s" : "")}");
            if (months > 0) parts.Add($"{months} month{(months > 1 ? "s" : "")}");
            if (days > 0) parts.Add($"{days} day{(days > 1 ? "s" : "")}");

            string structuredResult = string.Join(", ", parts);
            if (string.IsNullOrEmpty(structuredResult)) structuredResult = "0 days"; // Should not happen if start != end

            // Calculate total difference in days
            TimeSpan totalDifference = laterDate - earlierDate;
            string totalDaysString = $"{totalDifference.TotalDays:N0} day{(totalDifference.TotalDays != 1 ? "s" : "")}";

            // Combine results for display
            if (parts.Count > 1) // If result includes years/months/days
            {
                ResultDisplayDifference.Text = $"{structuredResult}\nTotal: {totalDaysString}";
            }
            else // If result is only days (or only years, only months)
            {
                ResultDisplayDifference.Text = $"{structuredResult}";
                // Optionally add total days if different from structured result (e.g. 31 days vs 1 month 0 days)
                if (structuredResult != totalDaysString && parts.Count == 1 && days > 0)
                {
                    ResultDisplayDifference.Text += $"\nTotal: {totalDaysString}";
                }
            }
        }


        // --- Add/Subtract Calculation ---

        private void AddSubtractInput_Changed(object? sender, RoutedEventArgs e)
        {
            if (_isLoaded && _currentMode == DateCalcMode.AddSubtract) CalculateAddSubtractDays();
        }
        private void AddSubtractInput_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded && _currentMode == DateCalcMode.AddSubtract) CalculateAddSubtractDays();
        }
        private void AddSubtractInput_Changed(object? sender, TextChangedEventArgs e)
        {
            if (_isLoaded && _currentMode == DateCalcMode.AddSubtract) CalculateAddSubtractDays();
        }


        private void CalculateAddSubtractDays()
        {
            if (!_isLoaded || _currentMode != DateCalcMode.AddSubtract || StartDatePicker == null || ResultDisplayAddSubtract == null) return;

            DateTime? startDate = StartDatePicker.SelectedDate;
            if (!startDate.HasValue)
            {
                ResultDisplayAddSubtract.Text = "Select start date";
                return;
            }

            // Get values from text boxes, default to 0 if empty or invalid
            int years = int.TryParse(YearsTextBox?.Text, out int y) ? y : 0;
            int months = int.TryParse(MonthsTextBox?.Text, out int m) ? m : 0;
            int days = int.TryParse(DaysTextBox?.Text, out int d) ? d : 0;

            // Check if subtracting
            bool subtract = SubtractRadio?.IsChecked == true;
            if (subtract)
            {
                years = -years;
                months = -months;
                days = -days;
            }

            // Perform calculation
            try
            {
                // Prevent excessively large values (optional)
                if (Math.Abs(years) > 10000 || Math.Abs(months) > 120000 || Math.Abs(days) > 3650000)
                {
                    throw new ArgumentOutOfRangeException("Input values are too large.");
                }


                DateTime resultDate = startDate.Value.AddYears(years).AddMonths(months).AddDays(days);

                // Check if result date is within valid range (DateTime limits)
                if (resultDate < DateTime.MinValue || resultDate > DateTime.MaxValue)
                {
                    throw new ArgumentOutOfRangeException("Result date is out of valid range.");
                }

                // Format the result date clearly
                ResultDisplayAddSubtract.Text = resultDate.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                ResultDisplayAddSubtract.Text = "Result date is invalid"; // More specific error?
            }
            catch (Exception) // Catch unexpected errors
            {
                ResultDisplayAddSubtract.Text = "Error calculating date";
            }
        }


        // --- Input Validation ---

        // Allow only digits in the TextBoxes
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // Regex to check if the input text is NOT a digit
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text); // Handle (block) the input if it's not a digit
        }

        // Prevent pasting non-numeric text
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand(); // Cancel paste if it contains non-digits
                }
            }
            else
            {
                e.CancelCommand(); // Cancel paste if data is not string
            }
        }

        // Helper for paste validation
        private static bool IsTextAllowed(string text)
        {
            // Check if all characters in the string are digits
            return text.All(char.IsDigit);
        }
    }
}