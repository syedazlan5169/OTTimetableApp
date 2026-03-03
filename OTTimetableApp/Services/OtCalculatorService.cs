using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.Domain.OT;

namespace OTTimetableApp.Services;

public class OtCalculatorService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public OtCalculatorService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public List<OtClaimLine> BuildMonthlyClaim(int calendarId, int employeeId, int month)
    {
        using var db = _dbFactory.CreateDbContext();

        var cal = db.Calendars.AsNoTracking().First(c => c.Id == calendarId);

        var start = new DateOnly(cal.Year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var days = db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToList();

        var dayIds = days.Select(d => d.Id).ToList();

        var shifts = db.ShiftAssignments
            .AsNoTracking()
            .Where(s => dayIds.Contains(s.CalendarDayId))
            .ToList();

        var shiftIds = shifts.Select(s => s.Id).ToList();

        // only where employee actually assigned
        var slots = db.ShiftSlots
            .AsNoTracking()
            .Where(sl => shiftIds.Contains(sl.ShiftAssignmentId) && sl.ActualEmployeeId == employeeId)
            .ToList();

        var dayById = days.ToDictionary(d => d.Id);
        var shiftById = shifts.ToDictionary(s => s.Id);

        var lines = new List<OtClaimLine>();

        foreach (var sl in slots)
        {
            var sh = shiftById[sl.ShiftAssignmentId];
            var day = dayById[sh.CalendarDayId];

            // TEMP placeholder category (Step 16 will calculate correctly)
            var cat = OtCategory.KelepasanGiliran;

            var (from, to, crossesMidnight) = GetShiftTime(sh.ShiftType);

            lines.AddRange(SplitIntoBands(day.Date, from, to, crossesMidnight, cat));
        }

        return lines
            .OrderBy(l => l.ClaimDate)
            .ThenBy(l => l.From)
            .ToList();
    }

    private static (TimeOnly from, TimeOnly to, bool crossesMidnight) GetShiftTime(ShiftType t)
    {
        return t switch
        {
            ShiftType.Morning => (new TimeOnly(7, 0), new TimeOnly(15, 0), false),
            ShiftType.Evening => (new TimeOnly(14, 0), new TimeOnly(23, 0), false),
            ShiftType.Night => (new TimeOnly(22, 0), new TimeOnly(7, 0), true),
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };
    }

    private static IEnumerable<OtClaimLine> SplitIntoBands(
        DateOnly day,
        TimeOnly from,
        TimeOnly to,
        bool crossesMidnight,
        OtCategory cat)
    {
        var segments = new List<(DateOnly date, TimeOnly a, TimeOnly b)>();

        if (!crossesMidnight)
        {
            segments.Add((day, from, to));
        }
        else
        {
            // 22:00 -> 00:00 (same day)
            segments.Add((day, from, new TimeOnly(0, 0)));
            // 00:00 -> 07:00 (next day)
            segments.Add((day.AddDays(1), new TimeOnly(0, 0), to));
        }

        foreach (var (date, a, b) in segments)
        {
            foreach (var line in SplitDayNight(date, a, b, cat))
                yield return line;
        }
    }

    private static List<OtClaimLine> SplitDayNight(DateOnly date, TimeOnly from, TimeOnly to, OtCategory cat)
    {
        var dayStart = new TimeOnly(6, 0);
        var nightStart = new TimeOnly(22, 0);

        int d0 = dayStart.Hour * 60;     // 360
        int n0 = nightStart.Hour * 60;   // 1320

        int f = from.Hour * 60 + from.Minute;
        int t = to.Hour * 60 + to.Minute;

        // allow 22:00 -> 00:00 style segment
        if (t < f) t += 24 * 60;

        var cuts = new[] { d0, n0, 24 * 60, 24 * 60 + d0, 24 * 60 + n0 }
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var result = new List<OtClaimLine>();

        int cur = f;
        int end = t;

        while (cur < end)
        {
            int nextCut = cuts.FirstOrDefault(c => c > cur);
            if (nextCut == 0) nextCut = end;

            int segEnd = Math.Min(nextCut, end);

            int curMod = cur % (24 * 60);
            var band = (curMod >= d0 && curMod < n0) ? RateBand.Day : RateBand.Night;

            var hours = (segEnd - cur) / 60m;
            if (hours > 0)
            {
                var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(curMod));
                var endTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(segEnd % (24 * 60)));

                result.Add(new OtClaimLine
                {
                    ClaimDate = date,
                    From = start,
                    To = endTime,
                    Category = cat,
                    Band = band,
                    Hours = hours,
                    Rate = OtRateTable.GetRate(cat, band)
                });
            }

            cur = segEnd;
        }

        return result;
    }
}