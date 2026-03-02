using System.Collections.ObjectModel;
using OTTimetableApp.Data;
using OTTimetableApp.Services;

namespace OTTimetableApp.ViewModels;

public class MonthViewerVM
{
    public int SelectedCalendarId { get; set; }
    public int SelectedMonth { get; set; } = 1;

    public string MonthTitle { get; set; } = "";

    public ObservableCollection<DayRowVM> Days { get; } = new();

    public void Load()
    {
        using var db = new AppDbContext(AppDbContext.BuildOptions());
        var svc = new MonthViewService(db);

        var cal = db.Calendars.First(c => c.Id == SelectedCalendarId);
        MonthTitle = new DateTime(cal.Year, SelectedMonth, 1).ToString("MMMM yyyy");

        Days.Clear();
        foreach (var r in svc.LoadMonth(SelectedCalendarId, SelectedMonth))
            Days.Add(r);
    }
}