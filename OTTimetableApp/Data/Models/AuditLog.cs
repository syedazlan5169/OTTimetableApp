namespace OTTimetableApp.Data.Models;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? CalendarId { get; set; }
    public string? CalendarName { get; set; }
}
