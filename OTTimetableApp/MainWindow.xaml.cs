using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
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
    private readonly PdfExportService _pdfSvc;

    public MainWindow(MonthViewerVM vm, SlotUpdateService slotSvc, PublicHolidayService phSvc, OtCalculatorService otSvc, ClaimPreviewWindow claimWin, IServiceProvider sp, PdfExportService pdfSvc)
    {
        InitializeComponent();
        _claimWin = claimWin;
        _vm = vm;
        _slotSvc = slotSvc;
        _phSvc = phSvc;
        _otSvc = otSvc;
        _pdfSvc = pdfSvc;

        DataContext = _vm;

        _vm.LoadCalendars();
        _vm.LoadMonth();
        _phSvc = phSvc;
        _sp = sp;

        // Scroll to today's date after initial load
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe immediately after first load to prevent memory leaks
        Loaded -= MainWindow_Loaded;
        ScrollToToday();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Closed -= MainWindow_Closed;
        _vm.Dispose();
    }
    private void CreateClaim_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<ClaimPreviewWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        var loginWin = _sp.GetRequiredService<AdminLoginWindow>();
        loginWin.Owner = this;
        loginWin.ShowDialog();

        if (!loginWin.Authenticated) return;

        var adminWin = _sp.GetRequiredService<AdminWindow>();
        adminWin.Owner = this;
        adminWin.ShowDialog();
    }

    private async void Calendar_Changed(object sender, SelectionChangedEventArgs e)
    {
        await _vm.LoadMonthAsync();
    }

    private async void Month_Changed(object sender, SelectionChangedEventArgs e)
    {
        await _vm.LoadMonthAsync();
    }

    private async void PH_Changed(object sender, RoutedEventArgs e)
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

        await _vm.LoadMonthAsync();
    }

    private async void PHName_LostFocus(object sender, RoutedEventArgs e)
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

        await _vm.LoadMonthAsync();
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

    private async void Slot_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not ShiftSlotVM slotVm) return;

        try
        {
            int? selected = cb.SelectedValue as int?;
            if (selected == 0) selected = null;

            _slotSvc.UpdateSlot(slotVm.ShiftSlotId, selected);

            await _vm.LoadMonthAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            await _vm.LoadMonthAsync();
        }
    }

    private async void ManageCalendars_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<CalendarManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // Reload calendars after closing manager
        _vm.LoadCalendars();
        await _vm.LoadMonthAsync();
    }

    private async void ManageEmployees_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<EmployeeManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // Employee names/options may have changed — force cache refresh
        _vm.InvalidateReferenceData();
        await _vm.LoadMonthAsync();
    }

    private async void ManageGroups_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<GroupManagerWindow>();
        win.Owner = this;
        win.ShowDialog();

        // Group membership may have changed — force cache refresh
        _vm.InvalidateReferenceData();
        await _vm.LoadMonthAsync();
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

            var claimResult = _otSvc.BuildMonthlyClaim(_vm.SelectedCalendarId, emp.Id, _vm.SelectedMonth);
            var lines = claimResult.ClaimLines;

            var totalHours = lines.Sum(x => x.Hours);
            var preview = string.Join("\n", lines.Take(8).Select(l =>
                $"{l.ClaimDate:dd/MM/yyyy} {l.From:HH\\:mm}-{l.To:HH\\:mm} {l.Band} {l.Category} {l.Hours}h"));

            MessageBox.Show(
                $"Employee: {emp.Name}\nLines: {lines.Count}\nTotal Hours: {totalHours}\nExcess Hours: {claimResult.ExcessWorkingHours:N2}\n\nFirst lines:\n{preview}",
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

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check if a calendar is selected
            if (_vm.SelectedCalendarId == 0)
            {
                MessageBox.Show("Please select a calendar first.", "No Calendar Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show date range picker dialog
            var dialog = _sp.GetRequiredService<DateRangePickerDialog>();
            dialog.Owner = this;

            if (dialog.ShowDialog() != true)
                return;

            // Show save file dialog
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = $"OT_Timetable_{dialog.StartDate:yyyy-MM-dd}_to_{dialog.EndDate:yyyy-MM-dd}.pdf"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            // Export to PDF
            _pdfSvc.ExportToPdf(_vm.SelectedCalendarId, dialog.StartDate, dialog.EndDate, saveDialog.FileName);

            MessageBox.Show($"PDF exported successfully to:\n{saveDialog.FileName}", "Export Successful",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Open the folder location
            var folderPath = System.IO.Path.GetDirectoryName(saveDialog.FileName);
            if (!string.IsNullOrEmpty(folderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export PDF:\n{ex.Message}", "Export Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.InvalidateReferenceData();
        await _vm.LoadMonthAsync();
    }

    private async void ResetMonth_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedCalendarId == 0)
        {
            MessageBox.Show("Select a calendar first.", "No Calendar Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Reset all slot assignments for {_vm.MonthTitle}?\n\n" +
            "• All replacements and fill-ins will be reverted to planned.\n" +
            "• Public holiday flags are kept.\n" +
            "• This cannot be undone.",
            "Confirm Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _vm.ResetMonthAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reset failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
