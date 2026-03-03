using System.Collections.ObjectModel;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.ViewModels;

// One row = one date (like Excel)
public class DayRowVM
{
    public int CalendarDayId { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("dd/MM/yyyy");
    public string DayName => Date.ToDateTime(TimeOnly.MinValue).ToString("dddd");

    public bool IsPublicHoliday { get; set; }
    public string? PublicHolidayName { get; set; }

    public ShiftVM Night { get; set; } = new();
    public ShiftVM Morning { get; set; } = new();
    public ShiftVM Evening { get; set; } = new();

    public string OffGroupName { get; set; } = "";
    public List<string> OffGroupMembers { get; set; } = new();
}

public class ShiftVM
{
    public int ShiftAssignmentId { get; set; }
    public string GroupName { get; set; } = "";
    public ObservableCollection<ShiftSlotVM> Slots { get; set; } = new();
}

public class ShiftSlotVM
{
    public int ShiftSlotId { get; set; }
    public int SlotIndex { get; set; }

    public int? PlannedEmployeeId { get; set; }
    public int? ActualEmployeeId { get; set; }
    public int? ReplacedEmployeeId { get; set; }

    public SlotFillType FillType { get; set; }

    // for dropdown
    public List<EmployeeOptionVM> EmployeeOptions { get; set; } = new();
}

public class EmployeeOptionVM
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}