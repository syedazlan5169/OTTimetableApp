namespace OTTimetableApp.Data.Models;

public class ShiftSlot
{
    public int Id { get; set; }

    public int ShiftAssignmentId { get; set; }
    public ShiftAssignment ShiftAssignment { get; set; } = null!;

    // 1..5 (warrant slot)
    public int SlotIndex { get; set; }

    // Planned employee (from base group), can be null if empty warrant
    public int? PlannedEmployeeId { get; set; }
    public Employee? PlannedEmployee { get; set; }

    // Actual employee assigned (user edits this)
    public int? ActualEmployeeId { get; set; }
    public Employee? ActualEmployee { get; set; }

    // Audit: if replacement, who got replaced
    public int? ReplacedEmployeeId { get; set; }
    public Employee? ReplacedEmployee { get; set; }

    public SlotFillType FillType { get; set; } = SlotFillType.Planned;
}