using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OTTimetableApp;

public partial class AuditLogWindow : Window
{
    private readonly AuditLogService _auditSvc;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private static readonly List<string> ActionOptions =
    [
        "(All Actions)",
        "SlotAssigned",
        "SlotCleared",
        "SlotFilled",
        "SlotReplaced",
        "PublicHolidayUpdated",
        "CalendarCreated",
        "CalendarDeleted"
    ];

    public AuditLogWindow(AuditLogService auditSvc, IDbContextFactory<AppDbContext> dbFactory)
    {
        InitializeComponent();
        _auditSvc = auditSvc;
        _dbFactory = dbFactory;

        LoadFilters();
        RefreshLogs();
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

        calendars.Insert(0, new CalendarOptionVM { Id = 0, Display = "(All Calendars)" });
        CalendarFilter.ItemsSource = calendars;
        CalendarFilter.SelectedIndex = 0;

        ActionFilter.ItemsSource = ActionOptions;
        ActionFilter.SelectedIndex = 0;
    }

    private void RefreshLogs()
    {
        int? calendarId = CalendarFilter.SelectedValue is int id && id != 0 ? id : null;
        string? action = ActionFilter.SelectedItem is string s && s != "(All Actions)" ? s : null;

        var logs = _auditSvc.GetLogs(calendarId, action);
        LogGrid.ItemsSource = logs;
        CountLabel.Text = $"{logs.Count} record(s)";
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => RefreshLogs();

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLogs();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
