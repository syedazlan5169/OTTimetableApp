using System.Windows;

namespace OTTimetableApp;

public partial class DateRangePickerDialog : Window
{
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsMonthMode { get; private set; }

    public DateRangePickerDialog()
    {
        InitializeComponent();

        // Populate month list (12 months for current year)
        PopulateMonthList();

        // Default to current month
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        StartDatePicker.SelectedDate = firstDay.ToDateTime(TimeOnly.MinValue);
        EndDatePicker.SelectedDate = lastDay.ToDateTime(TimeOnly.MinValue);

        MonthRadio.IsChecked = true;

        // Select current month in combo box
        SelectCurrentMonth();
    }

    private void PopulateMonthList()
    {
        var today = DateTime.Today;
        var currentYear = today.Year;

        var monthItems = new List<MonthItem>();

        for (int month = 1; month <= 12; month++)
        {
            var date = new DateTime(currentYear, month, 1);
            monthItems.Add(new MonthItem
            {
                DisplayName = date.ToString("MMMM"),
                Year = currentYear,
                Month = month
            });
        }

        MonthComboBox.ItemsSource = monthItems;
        MonthComboBox.DisplayMemberPath = nameof(MonthItem.DisplayName);
    }

    private void SelectCurrentMonth()
    {
        var today = DateTime.Today;
        var currentMonth = MonthComboBox.ItemsSource.Cast<MonthItem>()
            .FirstOrDefault(m => m.Month == today.Month);

        if (currentMonth != null)
            MonthComboBox.SelectedItem = currentMonth;
    }

    private void MonthRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (MonthComboBox == null) return;

        MonthComboBox.IsEnabled = true;
        StartDatePicker.IsEnabled = false;
        EndDatePicker.IsEnabled = false;

        IsMonthMode = true;
        UpdateDatesFromMonth();
    }

    private void CustomRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (MonthComboBox == null) return;

        MonthComboBox.IsEnabled = false;
        StartDatePicker.IsEnabled = true;
        EndDatePicker.IsEnabled = true;

        IsMonthMode = false;
    }

    private void MonthComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MonthRadio?.IsChecked == true)
        {
            UpdateDatesFromMonth();
        }
    }

    private void UpdateDatesFromMonth()
    {
        if (MonthComboBox.SelectedItem is MonthItem monthItem)
        {
            var firstDay = new DateOnly(monthItem.Year, monthItem.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            StartDatePicker.SelectedDate = firstDay.ToDateTime(TimeOnly.MinValue);
            EndDatePicker.SelectedDate = lastDay.ToDateTime(TimeOnly.MinValue);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Please select a valid date range.", "Invalid Date", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StartDate = DateOnly.FromDateTime(StartDatePicker.SelectedDate.Value);
        EndDate = DateOnly.FromDateTime(EndDatePicker.SelectedDate.Value);

        if (StartDate > EndDate)
        {
            MessageBox.Show("Start date must be before or equal to end date.", "Invalid Date Range", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private class MonthItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
