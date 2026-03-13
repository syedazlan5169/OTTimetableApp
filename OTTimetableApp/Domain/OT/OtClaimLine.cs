namespace OTTimetableApp.Domain.OT;

public enum OtCategory
{
    WorkingDay,
    KelepasanGiliran,
    KelepasanAm,
    KelepasanAmGantian
}

public enum RateBand
{
    Day,
    Night
}

public class OtClaimLine
{
    public DateOnly ClaimDate { get; set; }          // the date shown on claim
    public TimeOnly From { get; set; }
    public TimeOnly To { get; set; }

    public OtCategory Category { get; set; }
    public RateBand Band { get; set; }

    public decimal Hours { get; set; }              // already split day/night
    public decimal Rate { get; set; }               // 1.125, 1.25, etc

    public string Notes { get; set; } = "";         // optional debug/audit

    // UI-only: identify which original timetable shift this line came from.
    // This enables claim preview grouping/merging back into a single row per shift assignment.
    public int UiShiftAssignmentId { get; set; }
    public DateOnly UiShiftDate { get; set; }        // timetable row date (clock-out date for night shift)
    public TimeOnly UiShiftFrom { get; set; }
    public TimeOnly UiShiftTo { get; set; }

    // Slot information for remark generation
    public int? ReplacedEmployeeId { get; set; }
    public int SlotFillType { get; set; }  // 1=Planned, 2=Replacement, 3=EmptyFill, 4=Empty
    public int ShiftGroupId { get; set; }
}