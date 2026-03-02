using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class CalendarGeneratorService
{
    private readonly AppDbContext _db;

    public CalendarGeneratorService(AppDbContext db)
    {
        _db = db;
    }

    public void GenerateYear(int calendarId)
    {
        var cal = _db.Calendars.First(c => c.Id == calendarId);

        if (cal.IsGenerated)
            throw new InvalidOperationException("Calendar already generated. Delete it and create a new one.");

        // Load all group IDs (A/B/C/D etc.)
        var groups = _db.Groups.AsNoTracking().Select(g => g.Id).ToList();
        if (groups.Count < 4)
            throw new InvalidOperationException("Need at least 4 groups in database.");

        // Validate distinct initial setup
        var initSet = new HashSet<int> { cal.InitNightGroupId, cal.InitMorningGroupId, cal.InitEveningGroupId };
        if (initSet.Count != 3)
            throw new InvalidOperationException("Initial groups for Night/Morning/Evening must be different.");

        // Off group = the remaining group not used that day
        int offGroupId = groups.First(gid => !initSet.Contains(gid));

        // Rotation states: 0=Evening, 1=Morning, 2=Night, 3=Off
        // We assign states based on Jan 1 setup
        var state = new Dictionary<int, int>();
        foreach (var gid in groups)
            state[gid] = 3; // default Off

        state[cal.InitEveningGroupId] = 0;
        state[cal.InitMorningGroupId] = 1;
        state[cal.InitNightGroupId] = 2;
        state[offGroupId] = 3;

        // Generate all days in the year
        var start = new DateOnly(cal.Year, 1, 1);
        var end = new DateOnly(cal.Year, 12, 31);

        var daysToInsert = new List<CalendarDay>();

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            // Find which group is on which state today
            int eveningGroup = state.First(kv => kv.Value == 0).Key;
            int morningGroup = state.First(kv => kv.Value == 1).Key;
            int nightGroup = state.First(kv => kv.Value == 2).Key;
            int offGroup = state.First(kv => kv.Value == 3).Key;

            daysToInsert.Add(new CalendarDay
            {
                CalendarId = cal.Id,
                Date = date,
                EveningGroupId = eveningGroup,
                MorningGroupId = morningGroup,
                NightGroupId = nightGroup,
                OffGroupId = offGroup,
                IsPublicHoliday = false,
                PublicHolidayName = null
            });

            // Advance all groups to next state for next day
            // Evening -> Morning -> Night -> Off -> Evening ...
            var keys = state.Keys.ToList();
            foreach (var gid in keys)
                state[gid] = (state[gid] + 1) % 4;
        }

        // Write in one transaction
        using var tx = _db.Database.BeginTransaction();

        _db.CalendarDays.AddRange(daysToInsert);
        cal.IsGenerated = true;

        _db.SaveChanges();
        CreateShiftAssignmentsAndSlots(cal.Id);
        tx.Commit();
    }

    private void CreateShiftAssignmentsAndSlots(int calendarId)
    {
        var days = _db.CalendarDays
            .Where(d => d.CalendarId == calendarId)
            .OrderBy(d => d.Date)
            .ToList();

        // Load group members (base slots) once
        var baseMembers = _db.GroupMembers
            .AsNoTracking()
            .ToList();

        foreach (var day in days)
        {
            // For each day, create 3 shift assignments with locked GroupId
            var shifts = new[]
            {
            new ShiftAssignment { CalendarDayId = day.Id, ShiftType = ShiftType.Night,   GroupId = day.NightGroupId },
            new ShiftAssignment { CalendarDayId = day.Id, ShiftType = ShiftType.Morning, GroupId = day.MorningGroupId },
            new ShiftAssignment { CalendarDayId = day.Id, ShiftType = ShiftType.Evening, GroupId = day.EveningGroupId },
        };

            _db.ShiftAssignments.AddRange(shifts);
            _db.SaveChanges(); // so shifts have IDs

            foreach (var sh in shifts)
            {
                // Find the 5 base warrants for this group
                var gm = baseMembers
                    .Where(x => x.GroupId == sh.GroupId)
                    .OrderBy(x => x.SlotIndex)
                    .ToList();

                // Safety: if missing slots, still generate 5
                for (int slotIndex = 1; slotIndex <= 5; slotIndex++)
                {
                    var plannedEmpId = gm.FirstOrDefault(x => x.SlotIndex == slotIndex)?.EmployeeId;

                    var fillType = plannedEmpId == null ? SlotFillType.Empty : SlotFillType.Planned;

                    _db.ShiftSlots.Add(new ShiftSlot
                    {
                        ShiftAssignmentId = sh.Id,
                        SlotIndex = slotIndex,
                        PlannedEmployeeId = plannedEmpId,
                        ActualEmployeeId = plannedEmpId,
                        ReplacedEmployeeId = null,
                        FillType = fillType
                    });
                }

                _db.SaveChanges();
            }
        }
    }
}