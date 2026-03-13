using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OTTimetableApp.Data;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OTTimetableApp;

public partial class MainWindow : Window
{
    private readonly MonthViewerVM _vm;
    private readonly SlotUpdateService _slotSvc;
    private readonly PublicHolidayService _phSvc;
    private readonly OtCalculatorService _otSvc;
    private readonly ClaimPreviewWindow _claimWin;
    private readonly IServiceProvider _sp;

    public MainWindow(MonthViewerVM vm, SlotUpdateService slotSvc, PublicHolidayService phSvc, OtCalculatorService otSvc, ClaimPreviewWindow claimWin, IServiceProvider sp)
    {
        InitializeComponent();
        _claimWin = claimWin;
        _vm = vm;
        _slotSvc = slotSvc;
        _phSvc = phSvc;
        _otSvc = otSvc;

        DataContext = _vm;

        _vm.LoadCalendars();
        _vm.LoadMonth();
        _phSvc = phSvc;
        _sp = sp;

        // Scroll to today's date after initial load
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ScrollToToday();
    }
    private void CreateClaim_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<ClaimPreviewWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void DatabaseSetup_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<DatabaseSetupWindow>();
        win.Owner = this;
        win.ShowDialog();

        if (win.Saved)
            MessageBox.Show("Database settings saved. Please restart the app to use the new connection.");
    }

    private void Calendar_Changed(object sender, SelectionChangedEventArgs e)
    {
        _vm.LoadMonth();
    }

    private void Month_Changed(object sender, SelectionChangedEventArgs e)
    {
        _vm.LoadMonth();
    }

    private void PH_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DayRowVM day) return;

        try
        {
            _phSvc.UpdatePH(day.CalendarDayId, day.IsPublicHoliday, day.PublicHolidayName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        _vm.LoadMonth();
    }

    private void PHName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DayRowVM day) return;

        try
        {
            _phSvc.UpdatePH(day.CalendarDayId, day.IsPublicHoliday, day.PublicHolidayName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        _vm.LoadMonth();
    }

    private void PH_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not DayRowVM day) return;

        try
        {
            _phSvc.UpdatePH(day.CalendarDayId, day.IsPublicHoliday, day.PublicHolidayName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Slot_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not ShiftSlotVM slotVm) return;

        try
        {
            int? selected = cb.SelectedValue as int?;
            if (selected == 0) selected = null;

            _slotSvc.UpdateSlot(slotVm.ShiftSlotId, selected);

            _vm.LoadMonth();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.LoadMonth();
        }
    }

    private void ManageCalendars_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<CalendarManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // Reload calendars after closing manager
        _vm.LoadCalendars();
        _vm.LoadMonth();
    }

    private void ManageEmployees_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<EmployeeManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // reload month view after employee edits (names/options might change)
        _vm.LoadMonth();
    }

    private void ManageGroups_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<GroupManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // Group membership changes affect new calendars only, but also affect dropdown options in timetable
        _vm.LoadMonth();
    }

    private void DebugClaim_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_vm.SelectedCalendarId == 0)
            {
                MessageBox.Show("Select a calendar first.");
                return;
            }

            // Pick first active employee for now (we'll add employee selector later)
            using var db = App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            var emp = db.Employees.AsNoTracking().FirstOrDefault(x => x.IsActive);

            if (emp == null)
            {
                MessageBox.Show("No active employee found.");
                return;
            }

            var lines = _otSvc.BuildMonthlyClaim(_vm.SelectedCalendarId, emp.Id, _vm.SelectedMonth);

            var totalHours = lines.Sum(x => x.Hours);
            var preview = string.Join("\n", lines.Take(8).Select(l =>
                $"{l.ClaimDate:dd/MM/yyyy} {l.From:HH\\:mm}-{l.To:HH\\:mm} {l.Band} {l.Category} {l.Hours}h"));

            MessageBox.Show(
                $"Employee: {emp.Name}\nLines: {lines.Count}\nTotal Hours: {totalHours}\n\nFirst lines:\n{preview}",
                "Debug Claim");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Debug Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ScrollToToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayRow = _vm.Days.FirstOrDefault(d => d.Date == today);

        if (todayRow != null)
        {
            TimetableGrid.ScrollIntoView(todayRow);
            TimetableGrid.SelectedItem = todayRow;
        }
    }
}