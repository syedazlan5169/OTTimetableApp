using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class PublicHolidayService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly AuditLogService _auditSvc;

    public PublicHolidayService(IDbContextFactory<AppDbContext> dbFactory, AuditLogService auditSvc)
    {
        _dbFactory = dbFactory;
        _auditSvc = auditSvc;
    }

    public void UpdatePH(int calendarDayId, bool isPh, string? name)
    {
        using var db = _dbFactory.CreateDbContext();

        var day = db.CalendarDays
            .Include(d => d.Calendar)
            .First(d => d.Id == calendarDayId);

        bool oldIsPh = day.IsPublicHoliday;
        string? oldName = day.PublicHolidayName;

        string? trimmedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        day.IsPublicHoliday = isPh;
        day.PublicHolidayName = trimmedName;

        db.SaveChanges();

        // Only log if something actually changed
        if (oldIsPh == isPh && oldName == trimmedName)
            return;

        string dateStr = day.Date.ToString("dd/MM/yyyy");
        string description = isPh
            ? $"{dateStr} marked as Public Holiday: {trimmedName ?? "(no name)"}"
            : $"{dateStr} Public Holiday removed (was: {(oldIsPh ? oldName ?? "(no name)" : "not a PH")})";

        _auditSvc.Log("PublicHolidayUpdated", description, day.CalendarId, day.Calendar.Name);
    }

    public (int imported, int skipped, List<string> errors) BulkImportPH(int calendarId, List<(DateOnly date, string name)> entries)
    {
        using var db = _dbFactory.CreateDbContext();

        var calendar = db.Calendars.AsNoTracking().First(c => c.Id == calendarId);

        var allDays = db.CalendarDays
            .Where(d => d.CalendarId == calendarId)
            .ToDictionary(d => d.Date);

        int imported = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var (date, name) in entries)
        {
            if (!allDays.TryGetValue(date, out var day))
            {
                errors.Add($"{date:dd/MM/yyyy}: not found in calendar");
                skipped++;
                continue;
            }

            string? trimmedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

            if (day.IsPublicHoliday && day.PublicHolidayName == trimmedName)
            {
                skipped++;
                continue;
            }

            day.IsPublicHoliday = true;
            day.PublicHolidayName = trimmedName;
            imported++;
        }

        if (imported > 0)
        {
            db.SaveChanges();
            _auditSvc.Log("PHBulkImport",
                $"Bulk imported {imported} public holiday(s) into '{calendar.Name}' ({calendar.Year})",
                calendarId, calendar.Name);
        }

        return (imported, skipped, errors);
    }
}