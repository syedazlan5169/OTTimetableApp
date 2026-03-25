using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class AuditLogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public AuditLogService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void Log(string action, string description, int? calendarId = null, string? calendarName = null)
    {
        using var db = _dbFactory.CreateDbContext();
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.Now,
            Action = action,
            Description = description,
            CalendarId = calendarId,
            CalendarName = calendarName
        });
        db.SaveChanges();
    }

    public List<AuditLog> GetLogs(int? calendarId = null, string? action = null)
    {
        using var db = _dbFactory.CreateDbContext();
        IQueryable<AuditLog> query = db.AuditLogs.AsNoTracking().OrderByDescending(l => l.Timestamp);

        if (calendarId.HasValue)
            query = query.Where(l => l.CalendarId == calendarId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);

        return query.Take(500).ToList();
    }

    public int PurgeOlderThan(int days)
    {
        var cutoff = DateTime.Now.AddDays(-days);
        using var db = _dbFactory.CreateDbContext();
        var old = db.AuditLogs.Where(l => l.Timestamp < cutoff).ToList();
        db.AuditLogs.RemoveRange(old);
        db.SaveChanges();
        return old.Count;
    }
}
