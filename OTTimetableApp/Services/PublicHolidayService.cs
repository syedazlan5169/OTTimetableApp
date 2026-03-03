using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;

namespace OTTimetableApp.Services;

public class PublicHolidayService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PublicHolidayService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void UpdatePH(int calendarDayId, bool isPh, string? name)
    {
        using var db = _dbFactory.CreateDbContext();

        var day = db.CalendarDays.First(d => d.Id == calendarDayId);
        day.IsPublicHoliday = isPh;
        day.PublicHolidayName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        db.SaveChanges();
    }
}