using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp.Services;

public class MonthViewService
{
    private readonly AppDbContext _db;

    public MonthViewService(AppDbContext db)
    {
        _db = db;
    }

    public List<DayRowVM> LoadMonth(int calendarId, int month)
    {
        var year = _db.Calendars.Where(c => c.Id == calendarId).Select(c => c.Year).First();

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var employees = _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeOptionVM { Id = e.Id, Name = e.Name })
            .ToList();

        // Load CalendarDays for the month
        var days = _db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToList();

        // Preload group names
        var groupNames = _db.Groups
            .AsNoTracking()
            .ToDictionary(g => g.Id, g => g.Name);

        // Off group base members list for display (optional)
        var groupMembers = _db.GroupMembers
            .AsNoTracking()
            .ToList();

        var dayIds = days.Select(d => d.Id).ToList();

        // Load shifts + slots for those days
        var shifts = _db.ShiftAssignments
            .AsNoTracking()
            .Where(s => dayIds.Contains(s.CalendarDayId))
            .ToList();

        var shiftIds = shifts.Select(s => s.Id).ToList();

        var slots = _db.ShiftSlots
            .AsNoTracking()
            .Where(sl => shiftIds.Contains(sl.ShiftAssignmentId))
            .OrderBy(sl => sl.SlotIndex)
            .ToList();

        // Build lookup maps
        var shiftsByDay = shifts.GroupBy(s => s.CalendarDayId).ToDictionary(g => g.Key, g => g.ToList());
        var slotsByShift = slots.GroupBy(sl => sl.ShiftAssignmentId).ToDictionary(g => g.Key, g => g.ToList());

        // Compose VMs
        var result = new List<DayRowVM>();

        foreach (var d in days)
        {
            var row = new DayRowVM
            {
                CalendarDayId = d.Id,
                Date = d.Date,
                IsPublicHoliday = d.IsPublicHoliday,
                PublicHolidayName = d.PublicHolidayName
            };

            // OFF display (base members)
            row.OffGroupName = groupNames[d.OffGroupId];
            row.OffGroupMembers = groupMembers
                .Where(m => m.GroupId == d.OffGroupId)
                .OrderBy(m => m.SlotIndex)
                .Select(m => m.EmployeeId == null ? "(EMPTY)" : employees.First(x => x.Id == m.EmployeeId).Name)
                .ToList();

            // Shifts for this day
            if (shiftsByDay.TryGetValue(d.Id, out var dayShifts))
            {
                ShiftVM BuildShift(ShiftType type)
                {
                    var sh = dayShifts.First(x => x.ShiftType == type);

                    var svm = new ShiftVM
                    {
                        ShiftAssignmentId = sh.Id,
                        GroupName = groupNames[sh.GroupId]
                    };

                    if (slotsByShift.TryGetValue(sh.Id, out var shSlots))
                    {
                        foreach (var sl in shSlots)
                        {
                            svm.Slots.Add(new ShiftSlotVM
                            {
                                ShiftSlotId = sl.Id,
                                SlotIndex = sl.SlotIndex,
                                PlannedEmployeeId = sl.PlannedEmployeeId,
                                ActualEmployeeId = sl.ActualEmployeeId,
                                ReplacedEmployeeId = sl.ReplacedEmployeeId,
                                FillType = sl.FillType,
                                EmployeeOptions = employees
                            });
                        }
                    }

                    return svm;
                }

                row.Night = BuildShift(ShiftType.Night);
                row.Morning = BuildShift(ShiftType.Morning);
                row.Evening = BuildShift(ShiftType.Evening);
            }

            result.Add(row);
        }

        return result;
    }
}