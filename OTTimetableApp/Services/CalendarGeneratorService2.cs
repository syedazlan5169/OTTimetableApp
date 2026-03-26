using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class CalendarGeneratorService2
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CalendarGeneratorService2(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void GenerateYear(int calendarId)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = db.Calendars.AsNoTracking().First(c => c.Id == calendarId);

        if (cal.IsGenerated)
            throw new InvalidOperationException("Calendar already generated. Delete it and create a new one.");

        var groups = db.Groups.AsNoTracking().Select(g => g.Id).ToList();
        if (groups.Count < 4)
            throw new InvalidOperationException("Need at least 4 groups in database.");

        var initSet = new HashSet<int> { cal.InitNightGroupId, cal.InitMorningGroupId, cal.InitEveningGroupId };
        if (initSet.Count != 3)
            throw new InvalidOperationException("Initial groups must be different.");

        int offGroupId = groups.First(gid => !initSet.Contains(gid));

        var strategy = db.Database.CreateExecutionStrategy();
        strategy.Execute(() =>
        {
            db.ChangeTracker.Clear();

            // 0=Evening, 1=Morning, 2=Night, 3=Off
            var state = groups.ToDictionary(gid => gid, _ => 3);
            state[cal.InitEveningGroupId] = 0;
            state[cal.InitMorningGroupId] = 1;
            state[cal.InitNightGroupId] = 2;
            state[offGroupId] = 3;

            var start = new DateOnly(cal.Year, 1, 1);
            var end = new DateOnly(cal.Year, 12, 31);

            var days = new List<CalendarDay>();

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                int eveningGroup = state.First(kv => kv.Value == 0).Key;
                int morningGroup = state.First(kv => kv.Value == 1).Key;
                int nightGroup = state.First(kv => kv.Value == 2).Key;
                int offGroup = state.First(kv => kv.Value == 3).Key;

                days.Add(new CalendarDay
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

                var keys = state.Keys.ToList();
                foreach (var gid in keys)
                    state[gid] = (state[gid] + 1) % 4;
            }

            using var tx = db.Database.BeginTransaction();

            db.CalendarDays.AddRange(days);
            var calTracked = db.Calendars.First(c => c.Id == calendarId);
            calTracked.IsGenerated = true;
            db.SaveChanges();

            // Create shift assignments + slots
            var baseMembers = db.GroupMembers.AsNoTracking().ToList();

            var insertedDays = db.CalendarDays
                .Where(d => d.CalendarId == cal.Id)
                .OrderBy(d => d.Date)
                .ToList();

            foreach (var day in insertedDays)
            {
                var shifts = new[]
                {
                    new ShiftAssignment { CalendarDayId = day.Id, ShiftType = ShiftType.Night,   GroupId = day.NightGroupId },
                    new ShiftAssignment { CalendarDayId = day.Id, ShiftType = ShiftType.Morning, GroupId = day.MorningGroupId },
                    new ShiftAssignment { CalendarDayId = day.Id, ShiftType = ShiftType.Evening, GroupId = day.EveningGroupId },
                };

                db.ShiftAssignments.AddRange(shifts);
                db.SaveChanges();

                foreach (var sh in shifts)
                {
                    var gm = baseMembers
                        .Where(x => x.GroupId == sh.GroupId)
                        .OrderBy(x => x.SlotIndex)
                        .ToList();

                    var cap = db.Groups.AsNoTracking().Where(g => g.Id == sh.GroupId).Select(g => g.SlotCapacity).First();
                    for (int slotIndex = 1; slotIndex <= cap; slotIndex++)
                    {
                        var plannedEmpId = gm.FirstOrDefault(x => x.SlotIndex == slotIndex)?.EmployeeId;

                        var fillType = plannedEmpId == null ? SlotFillType.Empty : SlotFillType.Planned;

                        db.ShiftSlots.Add(new ShiftSlot
                        {
                            ShiftAssignmentId = sh.Id,
                            SlotIndex = slotIndex,
                            PlannedEmployeeId = plannedEmpId,
                            ActualEmployeeId = plannedEmpId,
                            ReplacedEmployeeId = null,
                            FillType = fillType
                        });
                    }

                    db.SaveChanges();
                }
            }

            tx.Commit();
        });
    }
}