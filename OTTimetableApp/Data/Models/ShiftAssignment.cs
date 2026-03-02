namespace OTTimetableApp.Data.Models;

public class ShiftAssignment
{
    public int Id { get; set; }

    public int CalendarDayId { get; set; }
    public CalendarDay CalendarDay { get; set; } = null!;

    public ShiftType ShiftType { get; set; }

    // Locked: which group owns this shift (from CalendarDay)
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
}