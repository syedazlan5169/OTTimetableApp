namespace OTTimetableApp.Data.Models;

public class GroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;

    // 1..SlotCount (keeps the “warrant slot position”)
    public int SlotIndex { get; set; }

    // null means EMPTY warrant slot
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}