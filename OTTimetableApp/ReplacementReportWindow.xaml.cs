using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OTTimetableApp;

public partial class ReplacementReportWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private record MonthItem(int Month, string Name);

    public ReplacementReportWindow(IDbContextFactory<AppDbContext> dbFactory)
    {
        InitializeComponent();
        _dbFactory = dbFactory;
        LoadFilters();
    }

    private void LoadFilters()
    {
        using var db = _dbFactory.CreateDbContext();

        var calendars = db.Calendars
            .AsNoTracking()
            .OrderByDescending(c => c.Year)
            .ThenBy(c => c.Name)
            .Select(c => new CalendarOptionVM { Id = c.Id, Display = $"{c.Year} - {c.Name}" })
            .ToList();

        CalendarFilter.ItemsSource = calendars;
        if (calendars.Count > 0)
            CalendarFilter.SelectedIndex = 0;

        var months = Enumerable.Range(1, 12)
            .Select(m => new MonthItem(m, new DateTime(2026, m, 1).ToString("MMMM")))
            .ToList();

        MonthFilter.ItemsSource = months;
        MonthFilter.SelectedValue = DateTime.Today.Month;
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        if (CalendarFilter.SelectedValue is not int calId)
        {
            MessageBox.Show("Please select a calendar.", "No Calendar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MonthFilter.SelectedValue is not int month) return;

        try
        {
            using var db = _dbFactory.CreateDbContext();

            var calendar = db.Calendars.AsNoTracking().First(c => c.Id == calId);

            var raw = db.ShiftSlots
                .AsNoTracking()
                .Where(s =>
                    s.FillType == SlotFillType.Replacement &&
                    s.ShiftAssignment.CalendarDay.CalendarId == calId &&
                    s.ShiftAssignment.CalendarDay.Date.Month == month &&
                    s.ShiftAssignment.CalendarDay.Date.Year == calendar.Year)
                .Select(s => new
                {
                    Date = s.ShiftAssignment.CalendarDay.Date,
                    ShiftType = s.ShiftAssignment.ShiftType,
                    GroupName = s.ShiftAssignment.Group.Name,
                    s.SlotIndex,
                    ReplacedBy = s.ActualEmployee != null ? s.ActualEmployee.Name : "(Unknown)",
                    Replaces = s.ReplacedEmployee != null ? s.ReplacedEmployee.Name : "(Unknown)"
                })
                .OrderBy(x => x.Date)
                .ThenBy(x => x.ShiftType)
                .ThenBy(x => x.SlotIndex)
                .ToList();

            var rows = raw.Select(x => new ReplacementRowVM
            {
                Date = x.Date,
                Shift = x.ShiftType switch
                {
                    ShiftType.Night => "Night (22:00-07:00)",
                    ShiftType.Morning => "Morning (07:00-15:00)",
                    _ => "Evening (14:00-23:00)"
                },
                GroupName = x.GroupName,
                SlotIndex = x.SlotIndex,
                ReplacedBy = x.ReplacedBy,
                Replaces = x.Replaces
            }).ToList();

            ReportGrid.ItemsSource = rows;
            CountLabel.Text = $"{rows.Count} replacement(s)";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
