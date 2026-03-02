namespace OTTimetableApp.Data.Models;

public class CalendarDay
{
    public int Id { get; set; }

    public int CalendarId { get; set; }
    public Calendar Calendar { get; set; } = null!;

    public DateOnly Date { get; set; }

    // Shift group assignments for this date (LOCKED once generated)
    public int NightGroupId { get; set; }    // 22-07
    public int MorningGroupId { get; set; }  // 07-15
    public int EveningGroupId { get; set; }  // 14-23

    // Off group is derived, but we store it for convenience & faster queries
    public int OffGroupId { get; set; }

    // Public Holiday flag + name (editable later)
    public bool IsPublicHoliday { get; set; } = false;
    public string? PublicHolidayName { get; set; }
}