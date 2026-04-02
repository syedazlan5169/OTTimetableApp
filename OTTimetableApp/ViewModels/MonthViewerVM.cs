using CommunityToolkit.Mvvm.ComponentModel;
using OTTimetableApp.Services;
using System.Collections.ObjectModel;

namespace OTTimetableApp.ViewModels;

public partial class MonthViewerVM : ObservableObject
{
    private readonly MonthViewService _svc;

    public MonthViewerVM(MonthViewService svc)
    {
        _svc = svc;

        Months = new ObservableCollection<MonthOptionVM>(
            Enumerable.Range(1, 12)
                .Select(m => new MonthOptionVM
                {
                    Month = m,
                    Name = new DateTime(2026, m, 1).ToString("MMMM")
                })
                .ToList()
        );

        // Auto-select current month when app starts
        SelectedMonth = DateTime.Today.Month;
    }

    public ObservableCollection<CalendarOptionVM> Calendars { get; } = new();
    public ObservableCollection<MonthOptionVM> Months { get; }

    [ObservableProperty]
    private int selectedCalendarId;

    [ObservableProperty]
    private int selectedMonth;

    [ObservableProperty]
    private string monthTitle = "";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private int highlightedEmployeeId;

    [ObservableProperty]
    private string highlightedEmployeeName = "";

    [ObservableProperty]
    private int highlightedEmployeeCount;

    public ObservableCollection<EmployeeOptionVM> HighlighterEmployees { get; } = new();

    partial void OnHighlightedEmployeeIdChanged(int value)
    {
        HighlightedEmployeeName = HighlighterEmployees.FirstOrDefault(e => e.Id == value)?.Name ?? "";
        HighlightedEmployeeCount = CountHighlightedSlots(value);
    }

    public ObservableCollection<DayRowVM> Days { get; } = new();

    public void LoadCalendars()
    {
        Calendars.Clear();

        var list = _svc.ListCalendars();
        foreach (var c in list)
            Calendars.Add(c);

        // If selected calendar was deleted, reset selection safely
        if (SelectedCalendarId != 0 && !Calendars.Any(x => x.Id == SelectedCalendarId))
            SelectedCalendarId = 0;

        // Auto-select first calendar if none selected
        if (SelectedCalendarId == 0 && Calendars.Count > 0)
            SelectedCalendarId = Calendars[0].Id;

        // If still none, clear UI
        if (SelectedCalendarId == 0)
        {
            MonthTitle = "";
            Days.Clear();
        }
    }

    public void LoadMonth()
    {
        if (SelectedCalendarId == 0) return;

        var payload = _svc.LoadMonth(SelectedCalendarId, SelectedMonth);

        MonthTitle = payload.MonthTitle;

        Days.Clear();
        foreach (var r in payload.Rows)
            Days.Add(r);

        RebuildHighlighterEmployees();
    }

    public async Task LoadMonthAsync()
    {
        if (SelectedCalendarId == 0) return;

        IsLoading = true;
        try
        {
            var payload = await _svc.LoadMonthAsync(SelectedCalendarId, SelectedMonth);

            MonthTitle = payload.MonthTitle;
            Days.Clear();
            foreach (var r in payload.Rows)
                Days.Add(r);

            RebuildHighlighterEmployees();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void InvalidateReferenceData() => _svc.InvalidateReferenceCache();

    private void RebuildHighlighterEmployees()
    {
        var previousId = HighlightedEmployeeId;

        HighlighterEmployees.Clear();
        HighlighterEmployees.Add(new EmployeeOptionVM { Id = 0, Name = "(None)" });

        var allOptions = Days
            .SelectMany(d => new[] { d.Night, d.Morning, d.Evening })
            .SelectMany(s => s.Slots)
            .SelectMany(sl => sl.EmployeeOptions)
            .Where(o => o.Id > 0)
            .GroupBy(o => o.Id)
            .Select(g => g.First())
            .OrderBy(o => o.Name);

        foreach (var opt in allOptions)
            HighlighterEmployees.Add(new EmployeeOptionVM { Id = opt.Id, Name = opt.Name });

        if (!HighlighterEmployees.Any(e => e.Id == previousId))
            HighlightedEmployeeId = 0;

        HighlightedEmployeeCount = CountHighlightedSlots(HighlightedEmployeeId);
    }

    private int CountHighlightedSlots(int employeeId)
    {
        if (employeeId == 0) return 0;

        return Days
            .SelectMany(d => new[] { d.Night, d.Morning, d.Evening })
            .SelectMany(s => s.Slots)
            .Count(sl => sl.ActualEmployeeId == employeeId);
    }

    public async Task ResetMonthAsync()
    {
        if (SelectedCalendarId == 0) return;

        IsLoading = true;
        try
        {
            await Task.Run(() => _svc.ResetMonth(SelectedCalendarId, SelectedMonth));
            var payload = await _svc.LoadMonthAsync(SelectedCalendarId, SelectedMonth);

            MonthTitle = payload.MonthTitle;
            Days.Clear();
            foreach (var r in payload.Rows)
                Days.Add(r);

            RebuildHighlighterEmployees();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SavePH(int calendarDayId, bool isPh, string? phName)
    {
        _svc.SavePublicHoliday(calendarDayId, isPh, phName);
    }

    public void SaveSlot(int shiftSlotId, int? newActualEmployeeId)
    {
        _svc.SaveSlotChange(shiftSlotId, newActualEmployeeId);
    }

    public void Dispose()
    {
        Calendars.Clear();
        Days.Clear();
    }
}

public class MonthOptionVM
{
    public int Month { get; set; }
    public string Name { get; set; } = "";
}