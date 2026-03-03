using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.Domain.OT;

namespace OTTimetableApp.Services;

public class OtCalculatorService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private static bool IsBaseGroupOffOnDate(CalendarDay d, int baseGroupId)
    => d.OffGroupId == baseGroupId;

    private static bool IsBaseGroupWorkingOnDate(CalendarDay d, int baseGroupId)
        => d.NightGroupId == baseGroupId
           || d.MorningGroupId == baseGroupId
           || d.EveningGroupId == baseGroupId;

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
        var loadStart = start.AddDays(-7);
        var loadEnd = end.AddDays(7);

        var days = db.CalendarDays
            .AsNoTracking()
            .Where(d => d.CalendarId == calendarId && d.Date >= loadStart && d.Date <= loadEnd)
            .OrderBy(d => d.Date)
            .ToList();

        var emp = db.Employees.AsNoTracking().First(e => e.Id == employeeId);
        var dayByDate = days.ToDictionary(d => d.Date);

        if (emp.BaseGroupId == null)
            throw new InvalidOperationException("Employee has no Base Group yet. Please assign Base Group before generating claim.");

        int baseGroupId = emp.BaseGroupId.Value;

        // 16.4: precompute PH Gantian dates for this employee
        var phGantianDates = BuildPhGantianDates(days, baseGroupId);

        var monthDays = days.Where(d => d.Date >= start && d.Date <= end).ToList();
        var dayIds = monthDays.Select(d => d.Id).ToList();

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
            var rowDay = dayById[sh.CalendarDayId]; // this is the timetable row date (clock-out date for Night)

            // On PH / PHG, own shift is claimable. On normal days, own shift is NOT claimable.
            if (IsOwnShift(rowDay, baseGroupId, sh.ShiftType))
            {
                // Use rowDay date for decision (for Night shift we handle per segment later)
                var catForDay = ResolveCategory(rowDay, baseGroupId, phGantianDates);

                if (catForDay != OtCategory.KelepasanAm && catForDay != OtCategory.KelepasanAmGantian)
                    continue;
            }

            // Build correct date/time segments for this shift
            var segments = GetShiftSegments(rowDay.Date, sh.ShiftType);

            foreach (var seg in segments)
            {
                // Resolve category using the segment's claim date
                if (!dayByDate.TryGetValue(seg.date, out var segDay))
                    continue; // outside loaded window (rare)

                var cat = ResolveCategory(segDay, baseGroupId, phGantianDates);

                // Split seg into Day/Night bands
                lines.AddRange(SplitDayNight(seg.date, seg.from, seg.to, cat));
            }
        }

        return lines
            .Where(l => l.ClaimDate >= start && l.ClaimDate <= end)
            .OrderBy(l => l.ClaimDate)
            .ThenBy(l => l.From)
            .ToList();
    }

    private static OtCategory ResolveCategory(CalendarDay day, int baseGroupId, HashSet<DateOnly> phGantianDates)
    {
        // PH day:
        if (day.IsPublicHoliday)
        {
            // If base group OFF on PH, PH must be claimed on next working day (PHG),
            // so OT on PH day is still Giliran
            if (day.OffGroupId == baseGroupId)
                return OtCategory.KelepasanGiliran;

            // Otherwise PH can be claimed normally
            return OtCategory.KelepasanAm;
        }

        // PH Gantian day (computed date)
        if (phGantianDates.Contains(day.Date))
            return OtCategory.KelepasanAmGantian;

        // Working day vs Off day
        if (day.OffGroupId != baseGroupId)
            return OtCategory.WorkingDay;

        return OtCategory.KelepasanGiliran;
    }

    private static List<(DateOnly date, TimeOnly from, TimeOnly to)> GetShiftSegments(DateOnly rowDate, ShiftType t)
    {
        return t switch
        {
            ShiftType.Morning => new()
        {
            (rowDate, new TimeOnly(7, 0), new TimeOnly(15, 0))
        },

            ShiftType.Evening => new()
        {
            (rowDate, new TimeOnly(14, 0), new TimeOnly(23, 0))
        },

            // IMPORTANT: rowDate is the CLOCK-OUT date for night shift
            ShiftType.Night => new()
        {
            (rowDate.AddDays(-1), new TimeOnly(22, 0), new TimeOnly(0, 0)),
            (rowDate,            new TimeOnly(0, 0),  new TimeOnly(7, 0))
        },

            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };
    }

    private static HashSet<DateOnly> BuildPhGantianDates(List<CalendarDay> days, int baseGroupId)
    {
        // days are month-only and sorted
        var result = new HashSet<DateOnly>();

        for (int i = 0; i < days.Count; i++)
        {
            var d = days[i];

            if (!d.IsPublicHoliday) continue;

            // PHG only if base group OFF during PH
            if (d.OffGroupId != baseGroupId) continue;

            // next day where base group is working (not OFF)
            for (int j = i + 1; j < days.Count; j++)
            {
                var next = days[j];

                if (next.OffGroupId != baseGroupId)
                {
                    result.Add(next.Date);
                    break;
                }
            }
        }

        return result;
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

    private static bool IsOwnShift(CalendarDay day, int baseGroupId, ShiftType shiftType)
    {
        return shiftType switch
        {
            ShiftType.Night => day.NightGroupId == baseGroupId,
            ShiftType.Morning => day.MorningGroupId == baseGroupId,
            ShiftType.Evening => day.EveningGroupId == baseGroupId,
            _ => false
        };
    }
}