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
        tx.Commit();
    }
}