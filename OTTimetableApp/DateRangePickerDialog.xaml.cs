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
        
        // Default to current month
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        
        StartDatePicker.SelectedDate = firstDay.ToDateTime(TimeOnly.MinValue);
        EndDatePicker.SelectedDate = lastDay.ToDateTime(TimeOnly.MinValue);
        
        MonthRadio.IsChecked = true;
    }

    private void MonthRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (MonthPicker == null) return;
        
        MonthPicker.IsEnabled = true;
        StartDatePicker.IsEnabled = false;
        EndDatePicker.IsEnabled = false;
        
        IsMonthMode = true;
        UpdateDatesFromMonth();
    }

    private void CustomRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (MonthPicker == null) return;
        
        MonthPicker.IsEnabled = false;
        StartDatePicker.IsEnabled = true;
        EndDatePicker.IsEnabled = true;
        
        IsMonthMode = false;
    }

    private void MonthPicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MonthRadio?.IsChecked == true)
        {
            UpdateDatesFromMonth();
        }
    }

    private void UpdateDatesFromMonth()
    {
        if (MonthPicker.SelectedDate.HasValue)
        {
            var date = DateOnly.FromDateTime(MonthPicker.SelectedDate.Value);
            var firstDay = new DateOnly(date.Year, date.Month, 1);
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
}
