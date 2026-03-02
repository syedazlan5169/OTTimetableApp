using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp.Services;

public class MonthViewService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public MonthViewService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public List<CalendarOptionVM> ListCalendars()
    {
        using var db = _dbFactory.CreateDbContext();

        return db.Calendars
            .OrderByDescending(c => c.Year)
            .ThenBy(c => c.Name)
            .Select(c => new CalendarOptionVM
            {
                Id = c.Id,
                Display = $"{c.Year} - {c.Name}"
            })
            .ToList();
    }

    public (string MonthTitle, List<DayRowVM> Rows) LoadMonth(int calendarId, int month)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = db.Calendars.AsNoTracking().First(c => c.Id == calendarId);
        var monthTitle = new DateTime(cal.Year, month, 1).ToString("MMMM yyyy");

        var rows = LoadMonthRows(db, calendarId, month, cal.Year);

        return (monthTitle, rows);
    }

    private static List<DayRowVM> LoadMonthRows(AppDbContext db, int calendarId, int month, int year)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var employees = db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeOptionVM { Id = e.Id, Name = e.Name })
            .ToList();

        var days = db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToList();

        var groupNames = db.Groups
            .AsNoTracking()
            .ToDictionary(g => g.Id, g => g.Name);

        var groupMembers = db.GroupMembers
            .AsNoTracking()
            .ToList();

        var dayIds = days.Select(d => d.Id).ToList();

        var shifts = db.ShiftAssignments
            .AsNoTracking()
            .Where(s => dayIds.Contains(s.CalendarDayId))
            .ToList();

        var shiftIds = shifts.Select(s => s.Id).ToList();

        var slots = db.ShiftSlots
            .AsNoTracking()
            .Where(sl => shiftIds.Contains(sl.ShiftAssignmentId))
            .OrderBy(sl => sl.SlotIndex)
            .ToList();

        var shiftsByDay = shifts.GroupBy(s => s.CalendarDayId).ToDictionary(g => g.Key, g => g.ToList());
        var slotsByShift = slots.GroupBy(sl => sl.ShiftAssignmentId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<DayRowVM>();

        foreach (var d in days)
        {
            var row = new DayRowVM
            {
                CalendarDayId = d.Id,
                Date = d.Date,
                IsPublicHoliday = d.IsPublicHoliday,
                PublicHolidayName = d.PublicHolidayName,
                OffGroupName = groupNames[d.OffGroupId],
                OffGroupMembers = groupMembers
                    .Where(m => m.GroupId == d.OffGroupId)
                    .OrderBy(m => m.SlotIndex)
                    .Select(m =>
                    {
                        if (m.EmployeeId == null) return "(EMPTY)";
                        var emp = employees.FirstOrDefault(x => x.Id == m.EmployeeId.Value);
                        return emp?.Name ?? "(UNKNOWN)";
                    })
                    .ToList()
            };

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