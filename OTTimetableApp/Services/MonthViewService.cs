using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp.Services;

public class MonthViewService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly AuditLogService _auditSvc;

    // ── Reference-data cache (employees / groups rarely change) ─────────────
    private record RefData(
        List<EmployeeOptionVM> Employees,
        Dictionary<int, string> EmpNamesById,
        Dictionary<int, string> GroupNames,
        List<GroupMember> GroupMembers);

    private RefData? _refCache;
    private readonly object _refCacheLock = new();

    public MonthViewService(IDbContextFactory<AppDbContext> dbFactory, AuditLogService auditSvc)
    {
        _dbFactory = dbFactory;
        _auditSvc = auditSvc;
    }

    /// <summary>Call after any admin change to employees or groups.</summary>
    public void InvalidateReferenceCache()
    {
        lock (_refCacheLock) { _refCache = null; }
    }

    private RefData LoadRefData(AppDbContext db)
    {
        lock (_refCacheLock)
        {
            if (_refCache is not null)
                return _refCache;

            var employees = db.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .Select(e => new EmployeeOptionVM { Id = e.Id, Name = e.Name })
                .ToList();

            var empNamesById = employees.ToDictionary(x => x.Id, x => x.Name);

            // Blank option at the top (Id=0 represents NULL)
            employees.Insert(0, new EmployeeOptionVM { Id = 0, Name = "(None)" });

            var groupNames = db.Groups
                .AsNoTracking()
                .ToDictionary(g => g.Id, g => g.Name);

            var groupMembers = db.GroupMembers
                .AsNoTracking()
                .ToList();

            _refCache = new RefData(employees, empNamesById, groupNames, groupMembers);
            return _refCache;
        }
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

    public string GetWorkingShiftLabel(int calendarId, int baseGroupId, DateOnly date)
    {
        using var db = _dbFactory.CreateDbContext();

        var day = db.CalendarDays
            .AsNoTracking()
            .First(d => d.CalendarId == calendarId && d.Date == date);

        if (day.MorningGroupId == baseGroupId) return "07:00-15:00";
        if (day.EveningGroupId == baseGroupId) return "14:00-23:00";
        if (day.NightGroupId == baseGroupId) return "22:00-07:00";

        return "OFF";
    }

    public (string MonthTitle, List<DayRowVM> Rows) LoadMonth(int calendarId, int month)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = db.Calendars
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == calendarId);

        // Calendar deleted / not found → return empty payload safely
        if (cal == null)
            return ("", new List<DayRowVM>());

        var monthTitle = new DateTime(cal.Year, month, 1).ToString("MMMM yyyy");

        var rows = LoadMonthRows(db, calendarId, month, cal.Year);

        return (monthTitle, rows);
    }

    public async Task<(string MonthTitle, List<DayRowVM> Rows)> LoadMonthAsync(int calendarId, int month)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = await db.Calendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calendarId)
            .ConfigureAwait(false);

        if (cal == null)
            return ("", new List<DayRowVM>());

        var monthTitle = new DateTime(cal.Year, month, 1).ToString("MMMM yyyy");
        var rows = await LoadMonthRowsAsync(db, calendarId, month, cal.Year).ConfigureAwait(false);

        return (monthTitle, rows);
    }

    private List<DayRowVM> LoadMonthRows(AppDbContext db, int calendarId, int month, int year)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var refData = LoadRefData(db);

        var days = db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToList();

        if (days.Count == 0)
            return new List<DayRowVM>();

        var dayIds = days.Select(d => d.Id).ToList();

        // Single JOIN query — replaces the previous two separate IN-list queries
        var slots = db.ShiftSlots
            .AsNoTracking()
            .Include(sl => sl.ShiftAssignment)
            .Where(sl => dayIds.Contains(sl.ShiftAssignment.CalendarDayId))
            .OrderBy(sl => sl.SlotIndex)
            .ToList();

        var shiftsByDay = slots
            .Select(sl => sl.ShiftAssignment)
            .DistinctBy(s => s.Id)
            .GroupBy(s => s.CalendarDayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var slotsByShift = slots
            .GroupBy(sl => sl.ShiftAssignmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return BuildRows(days, shiftsByDay, slotsByShift, refData);
    }

    private async Task<List<DayRowVM>> LoadMonthRowsAsync(AppDbContext db, int calendarId, int month, int year)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var refData = LoadRefData(db);

        var days = await db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToListAsync()
            .ConfigureAwait(false);

        if (days.Count == 0)
            return new List<DayRowVM>();

        var dayIds = days.Select(d => d.Id).ToList();

        // Single JOIN query — replaces the previous two separate IN-list queries
        var slots = await db.ShiftSlots
            .AsNoTracking()
            .Include(sl => sl.ShiftAssignment)
            .Where(sl => dayIds.Contains(sl.ShiftAssignment.CalendarDayId))
            .OrderBy(sl => sl.SlotIndex)
            .ToListAsync()
            .ConfigureAwait(false);

        var shiftsByDay = slots
            .Select(sl => sl.ShiftAssignment)
            .DistinctBy(s => s.Id)
            .GroupBy(s => s.CalendarDayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var slotsByShift = slots
            .GroupBy(sl => sl.ShiftAssignmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return BuildRows(days, shiftsByDay, slotsByShift, refData);
    }

    private static List<DayRowVM> BuildRows(
        List<CalendarDay> days,
        Dictionary<int, List<ShiftAssignment>> shiftsByDay,
        Dictionary<int, List<ShiftSlot>> slotsByShift,
        RefData refData)
    {
        var employees = refData.Employees;
        var empNamesById = refData.EmpNamesById;
        var groupNames = refData.GroupNames;
        var groupMembers = refData.GroupMembers;

        var result = new List<DayRowVM>(days.Count);

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
                        GroupId = sh.GroupId,
                        GroupName = groupNames[sh.GroupId]
                    };

                    if (slotsByShift.TryGetValue(sh.Id, out var shSlots))
                    {
                        var usedIds = shSlots
                            .Select(x => x.ActualEmployeeId)
                            .Where(x => x.HasValue && x.Value != 0)
                            .Select(x => x!.Value)
                            .ToHashSet();

                        foreach (var sl in shSlots)
                        {
                            // For this slot, allow its own current selection (so it doesn't disable itself)
                            var usedExceptSelf = usedIds.ToHashSet();
                            if (sl.ActualEmployeeId.HasValue && sl.ActualEmployeeId.Value != 0)
                                usedExceptSelf.Remove(sl.ActualEmployeeId.Value);

                            var optionList = employees
                                .Select(o => new EmployeeOptionVM
                                {
                                    Id = o.Id,
                                    Name = o.Name,
                                    IsEnabled = (o.Id == 0) || !usedExceptSelf.Contains(o.Id) // allow (None)
                                })
                                .ToList();

                            string statusText = sl.FillType switch
                            {
                                SlotFillType.Planned => "Planned",
                                SlotFillType.Replacement => "Replace",
                                SlotFillType.EmptyFill => "Fill",
                                SlotFillType.Empty => "Empty",
                                _ => "?"
                            };

                            string? replacesName = null;
                            if (sl.FillType == SlotFillType.Replacement && sl.ReplacedEmployeeId.HasValue)
                            {
                                if (empNamesById.TryGetValue(sl.ReplacedEmployeeId.Value, out var n))
                                    replacesName = n;
                                else
                                    replacesName = "(UNKNOWN)";
                            }

                            string? onLeaveName = null;
                            if (sl.FillType == SlotFillType.Empty && sl.PlannedEmployeeId.HasValue)
                            {
                                if (empNamesById.TryGetValue(sl.PlannedEmployeeId.Value, out var n))
                                    onLeaveName = n;
                                else
                                    onLeaveName = "(UNKNOWN)";
                            }

                            svm.Slots.Add(new ShiftSlotVM
                            {
                                ShiftSlotId = sl.Id,
                                SlotIndex = sl.SlotIndex,
                                PlannedEmployeeId = sl.PlannedEmployeeId,
                                ActualEmployeeId = sl.ActualEmployeeId,
                                ReplacedEmployeeId = sl.ReplacedEmployeeId,
                                FillType = sl.FillType,
                                StatusText = statusText,
                                ReplacesName = replacesName,
                                OnLeaveName = onLeaveName,
                                EmployeeOptions = optionList
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

    public void SavePublicHoliday(int calendarDayId, bool isPh, string? phName)
    {
        using var db = _dbFactory.CreateDbContext();

        var day = db.CalendarDays.First(d => d.Id == calendarDayId);
        day.IsPublicHoliday = isPh;
        day.PublicHolidayName = string.IsNullOrWhiteSpace(phName) ? null : phName.Trim();

        db.SaveChanges();
    }

    public void SaveSlotChange(int shiftSlotId, int? newActualEmployeeId)
    {
        if (newActualEmployeeId == 0) newActualEmployeeId = null;
        
        using var db = _dbFactory.CreateDbContext();
        var slot = db.ShiftSlots.First(s => s.Id == shiftSlotId);

        slot.ActualEmployeeId = newActualEmployeeId;

        // Guard: no duplicate within the same ShiftAssignment
        if (newActualEmployeeId.HasValue)
        {
            var exists = db.ShiftSlots.Any(s =>
                s.ShiftAssignmentId == slot.ShiftAssignmentId
                && s.Id != slot.Id
                && s.ActualEmployeeId == newActualEmployeeId);

            if (exists)
                throw new InvalidOperationException("Duplicate name in the same shift is not allowed.");
        }

        // Determine FillType + ReplacedEmployeeId based on planned slot
        // PlannedEmployeeId null means this is an "empty warrant" originally.
        if (newActualEmployeeId == null)
        {
            slot.FillType = SlotFillType.Empty;
            slot.ReplacedEmployeeId = null;
        }
        else if (slot.PlannedEmployeeId == null)
        {
            // Filling an empty warrant
            slot.FillType = SlotFillType.EmptyFill;
            slot.ReplacedEmployeeId = null;
        }
        else if (newActualEmployeeId == slot.PlannedEmployeeId)
        {
            // Same as planned
            slot.FillType = SlotFillType.Planned;
            slot.ReplacedEmployeeId = null;
        }
        else
        {
            // Replacing planned member
            slot.FillType = SlotFillType.Replacement;
            slot.ReplacedEmployeeId = slot.PlannedEmployeeId;
        }

        db.SaveChanges();
    }

    public void ResetMonth(int calendarId, int month)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = db.Calendars.AsNoTracking().FirstOrDefault(c => c.Id == calendarId);
        if (cal == null) return;

        var start = new DateOnly(cal.Year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var dayIds = db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= start && d.Date <= end)
            .Select(d => d.Id)
            .ToList();

        if (dayIds.Count == 0) return;

        var shiftIds = db.ShiftAssignments
            .AsNoTracking()
            .Where(s => dayIds.Contains(s.CalendarDayId))
            .Select(s => s.Id)
            .ToList();

        if (shiftIds.Count == 0) return;

        var slots = db.ShiftSlots
            .Where(sl => shiftIds.Contains(sl.ShiftAssignmentId))
            .ToList();

        foreach (var slot in slots)
        {
            slot.ActualEmployeeId = slot.PlannedEmployeeId;
            slot.ReplacedEmployeeId = null;
            slot.FillType = slot.PlannedEmployeeId == null ? SlotFillType.Empty : SlotFillType.Planned;
        }

        db.SaveChanges();

        _auditSvc.Log("MonthReset",
            $"Month {new DateTime(cal.Year, month, 1):MMMM yyyy} reset to planned assignments.",
            calendarId, cal.Name);
    }

    public List<Group> ListGroups()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Groups.AsNoTracking().OrderBy(g => g.Name).ToList();
    }

    public int CreateCalendar(string name, int year, int initNightGroupId, int initMorningGroupId, int initEveningGroupId)
    {
        name = name.Trim();

        using var db = _dbFactory.CreateDbContext();

        if (db.Calendars.Any(c => c.Name == name && c.Year == year))
            throw new InvalidOperationException("Calendar with same name + year already exists.");

        var cal = new Calendar
        {
            Name = name,
            Year = year,
            InitNightGroupId = initNightGroupId,
            InitMorningGroupId = initMorningGroupId,
            InitEveningGroupId = initEveningGroupId,
            IsGenerated = false
        };

        db.Calendars.Add(cal);
        db.SaveChanges();

        _auditSvc.Log("CalendarCreated", $"Calendar '{name}' ({year}) created.", cal.Id, name);

        return cal.Id;
    }

    public void DeleteCalendar(int calendarId)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = db.Calendars.First(c => c.Id == calendarId);

        _auditSvc.Log("CalendarDeleted", $"Calendar '{cal.Name}' ({cal.Year}) deleted.", calendarId, cal.Name);

        // Cascade should delete days->shifts->slots (based on your FK config)
        db.Calendars.Remove(cal);
        db.SaveChanges();
    }
}