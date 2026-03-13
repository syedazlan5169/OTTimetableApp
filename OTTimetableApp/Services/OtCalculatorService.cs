using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;
using OTTimetableApp.Domain.OT;

namespace OTTimetableApp.Services;

public class OtCalculatorService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private static DateTime ToDateTime(DateOnly d, TimeOnly t)
    => d.ToDateTime(t);

    private static IEnumerable<(DateTime start, DateTime end)> SubtractInterval(
        DateTime start,
        DateTime end,
        DateTime cutStart,
        DateTime cutEnd)
    {
        // no overlap
        if (end <= cutStart || start >= cutEnd)
        {
            yield return (start, end);
            yield break;
        }

        // fully covered
        if (start >= cutStart && end <= cutEnd)
            yield break;

        // left piece
        if (start < cutStart)
        {
            var leftEnd = end < cutStart ? end : cutStart;
            if (leftEnd > start)
                yield return (start, leftEnd);
        }

        // right piece
        if (end > cutEnd)
        {
            var rightStart = start > cutEnd ? start : cutEnd;
            if (end > rightStart)
                yield return (rightStart, end);
        }
    }

    private static (DateTime start, DateTime end)? GetOwnShiftInterval(CalendarDay day, int baseGroupId)
    {
        // if base group OFF => no own shift
        if (day.OffGroupId == baseGroupId) return null;

        // Determine which shift base group is assigned to
        if (day.MorningGroupId == baseGroupId)
            return (ToDateTime(day.Date, new TimeOnly(7, 0)), ToDateTime(day.Date, new TimeOnly(15, 0)));

        if (day.EveningGroupId == baseGroupId)
            return (ToDateTime(day.Date, new TimeOnly(14, 0)), ToDateTime(day.Date, new TimeOnly(23, 0)));

        if (day.NightGroupId == baseGroupId)
        {
            // remember: night shift for row date means 22:00 previous day -> 07:00 on this day
            return (ToDateTime(day.Date.AddDays(-1), new TimeOnly(22, 0)), ToDateTime(day.Date, new TimeOnly(7, 0)));
        }

        return null;
    }

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

        var dayByDate = days.ToDictionary(d => d.Date);
        var baseGroupId = db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.EmployeeId == employeeId)
            .Select(gm => (int?)gm.GroupId)
            .FirstOrDefault();

        if (baseGroupId == null)
            throw new InvalidOperationException("Employee is not assigned to any group yet. Please assign the employee in Group Manager before generating claim.");

        int baseGroupIdValue = baseGroupId.Value;

        // 16.4: precompute PH Gantian dates for this employee
        var phGantianDates = BuildPhGantianDates(days, baseGroupIdValue);

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
        var raw = new List<RawSeg>();

        foreach (var sl in slots)
        {
            var sh = shiftById[sl.ShiftAssignmentId];
            var rowDay = dayById[sh.CalendarDayId]; // timetable row date

            var (uiShiftFrom, uiShiftTo, _) = GetShiftTime(sh.ShiftType);

            // Own shift skip logic (but allow PH/PHG)
            if (IsOwnShift(rowDay, baseGroupIdValue, sh.ShiftType))
            {
                var catForDay = ResolveCategory(rowDay, baseGroupIdValue, phGantianDates);
                if (catForDay != OtCategory.KelepasanAm && catForDay != OtCategory.KelepasanAmGantian)
                    continue;
            }

            var segments = GetShiftSegments(rowDay.Date, sh.ShiftType);

            foreach (var seg in segments)
            {
                if (!dayByDate.TryGetValue(seg.date, out var segDay))
                    continue;

                var cat = ResolveCategory(segDay, baseGroupIdValue, phGantianDates);

                // Build DateTime interval
                var startDt = ToDateTime(seg.date, seg.from);
                var endDt = ToDateTime(seg.date, seg.to);

                // Special: 00:00 means next day boundary for end
                if (seg.to == new TimeOnly(0, 0) && seg.from != new TimeOnly(0, 0))
                    endDt = ToDateTime(seg.date.AddDays(1), new TimeOnly(0, 0));

                // WorkingDay overlap rule: if employee has own shift SAME DATE,
                // and OT overlaps own shift, subtract own-shift time so it won't be double-claimed.
                if (cat == OtCategory.WorkingDay)
                {
                    var own = GetOwnShiftInterval(segDay, baseGroupIdValue);
                    if (own != null)
                    {
                        var (ownStart, ownEnd) = own.Value;

                        foreach (var (pStart, pEnd) in SubtractInterval(startDt, endDt, ownStart, ownEnd))
                        {
                            if (pEnd > pStart)
                            {
                                raw.Add(new RawSeg
                                {
                                    Start = pStart,
                                    End = pEnd,
                                    Category = cat,
                                    UiShiftAssignmentId = sh.Id,
                                    UiShiftDate = rowDay.Date,
                                    UiShiftFrom = uiShiftFrom,
                                    UiShiftTo = uiShiftTo,
                                    ReplacedEmployeeId = sl.ReplacedEmployeeId,
                                    SlotFillType = (int)sl.FillType,
                                    ShiftGroupId = sh.GroupId
                                });
                            }
                        }

                        continue;
                    }
                }

                if (endDt > startDt)
                {
                    raw.Add(new RawSeg
                    {
                        Start = startDt,
                        End = endDt,
                        Category = cat,
                        UiShiftAssignmentId = sh.Id,
                        UiShiftDate = rowDay.Date,
                        UiShiftFrom = uiShiftFrom,
                        UiShiftTo = uiShiftTo,
                        ReplacedEmployeeId = sl.ReplacedEmployeeId,
                        SlotFillType = (int)sl.FillType,
                        ShiftGroupId = sh.GroupId
                    });
                }
            }
        }

        // 1) merge overlaps (fix 14-15, 22-23)
        var mergedRaw = MergeOverlaps(raw);

        // 2) compute deductions from continuous chains
        var deductions = BuildDeductions(mergedRaw);

        // 3) split to day/night bands
        var bandSegs = SplitIntoBands(mergedRaw);

        // 4) subtract deductions
        var finalSegs = ApplyDeductions(bandSegs, deductions);

        // 5) convert to claim lines
        var outLines = ToClaimLines(finalSegs);

        // 6) month filter (optional; depends your payroll practice)
        return outLines
            .Where(l => l.ClaimDate >= start && l.ClaimDate <= end)
            .ToList();
    }

    private static List<RawSeg> MergeOverlaps(List<RawSeg> input)
    {
        var sorted = input
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .ThenBy(x => x.End)
            .ToList();

        var result = new List<RawSeg>();
        foreach (var s in sorted)
        {
            if (result.Count == 0)
            {
                result.Add(new RawSeg
                {
                    Start = s.Start,
                    End = s.End,
                    Category = s.Category,
                    UiShiftAssignmentId = s.UiShiftAssignmentId,
                    UiShiftDate = s.UiShiftDate,
                    UiShiftFrom = s.UiShiftFrom,
                    UiShiftTo = s.UiShiftTo,
                    ReplacedEmployeeId = s.ReplacedEmployeeId,
                    SlotFillType = s.SlotFillType,
                    ShiftGroupId = s.ShiftGroupId
                });
                continue;
            }

            var last = result[^1];

            // Case 1: same original shift assignment + same category => we can merge safely
            if (s.Start <= last.End && s.Category == last.Category && s.UiShiftAssignmentId == last.UiShiftAssignmentId)
            {
                if (s.End > last.End) last.End = s.End;
                continue;
            }

            // Case 2: different shift assignment (or different category)
            // Prevent double-claiming overlapping time (e.g. 07:00-15:00 and 14:00-23:00).
            // Keep the earlier segment as-is, and trim the later segment to start at last.End.
            var trimmedStart = s.Start;
            var trimmedUiFrom = s.UiShiftFrom;

            if (trimmedStart < last.End)
            {
                trimmedStart = last.End;
                trimmedUiFrom = TimeOnly.FromDateTime(trimmedStart);
            }

            if (s.End <= trimmedStart)
                continue;

            result.Add(new RawSeg
            {
                Start = trimmedStart,
                End = s.End,
                Category = s.Category,
                UiShiftAssignmentId = s.UiShiftAssignmentId,
                UiShiftDate = s.UiShiftDate,
                UiShiftFrom = trimmedUiFrom,
                UiShiftTo = s.UiShiftTo,
                ReplacedEmployeeId = s.ReplacedEmployeeId,
                SlotFillType = s.SlotFillType,
                ShiftGroupId = s.ShiftGroupId
            });
        }

        // Normalize UI shift window per shift assignment after trimming/merging.
        var boundsByShift = result
            .GroupBy(x => x.UiShiftAssignmentId)
            .ToDictionary(
                g => g.Key,
                g => (Start: g.Min(x => x.Start), End: g.Max(x => x.End)));

        foreach (var seg in result)
        {
            var b = boundsByShift[seg.UiShiftAssignmentId];
            seg.UiShiftFrom = TimeOnly.FromDateTime(b.Start);
            seg.UiShiftTo = TimeOnly.FromDateTime(b.End);
        }

        return result;
    }

    private static List<(DateTime start, DateTime end)> BuildDeductions(List<RawSeg> merged)
    {
        // merged must be non-overlapping for correct chain durations
        var deductions = new List<(DateTime start, DateTime end)>();

        // Build continuous chains (gap breaks counter)
        for (int i = 0; i < merged.Count; i++)
        {
            var chainStart = merged[i].Start;
            var chainEnd = merged[i].End;

            int j = i;
            while (j + 1 < merged.Count && merged[j + 1].Start <= chainEnd)
            {
                // continuous (touching/overlapping)
                chainEnd = (merged[j + 1].End > chainEnd) ? merged[j + 1].End : chainEnd;
                j++;
            }

            var totalHours = (chainEnd - chainStart).TotalHours;
            int n = (int)Math.Floor(totalHours / 9.0);

            for (int k = 1; k <= n; k++)
            {
                // deduct the 9th hour itself
                var dStart = chainStart.AddHours(k * 9 - 1);
                var dEnd = chainStart.AddHours(k * 9);
                deductions.Add((dStart, dEnd));
            }

            i = j;
        }

        return deductions;
    }

    private static List<OtSeg> SplitIntoBands(List<RawSeg> raw)
    {
        var result = new List<OtSeg>();

        foreach (var r in raw)
        {
            var cur = r.Start;

            while (cur < r.End)
            {
                var band = GetBand(cur);
                var nextBoundary = GetNextBandBoundary(cur);

                var segEnd = r.End < nextBoundary ? r.End : nextBoundary;

                if (segEnd > cur)
                {
                    result.Add(new OtSeg
                    {
                        Start = cur,
                        End = segEnd,
                        Category = r.Category,
                        Band = band,
                        UiShiftAssignmentId = r.UiShiftAssignmentId,
                        UiShiftDate = r.UiShiftDate,
                        UiShiftFrom = r.UiShiftFrom,
                        UiShiftTo = r.UiShiftTo,
                        ReplacedEmployeeId = r.ReplacedEmployeeId,
                        SlotFillType = r.SlotFillType,
                        ShiftGroupId = r.ShiftGroupId
                    });
                }

                cur = segEnd;
            }
        }

        return result;
    }

    private static RateBand GetBand(DateTime dt)
    {
        var t = dt.TimeOfDay;
        var dayStart = new TimeSpan(6, 0, 0);
        var nightStart = new TimeSpan(22, 0, 0);
        return (t >= dayStart && t < nightStart) ? RateBand.Day : RateBand.Night;
    }

    private static DateTime GetNextBandBoundary(DateTime dt)
    {
        var d = dt.Date;
        var dayStart = d.AddHours(6);
        var nightStart = d.AddHours(22);
        var nextDayStart = d.AddDays(1).AddHours(6);

        // If we are in night band: boundary is 06:00 next (same day if before 6, else next day)
        if (GetBand(dt) == RateBand.Night)
        {
            if (dt < dayStart) return dayStart;
            return nextDayStart;
        }

        // In day band: boundary is 22:00 same day
        return nightStart;
    }

    private static List<OtSeg> ApplyDeductions(List<OtSeg> segs, List<(DateTime start, DateTime end)> deductions)
    {
        var result = segs;

        foreach (var d in deductions)
        {
            var next = new List<OtSeg>();

            foreach (var s in result)
            {
                // no overlap
                if (d.end <= s.Start || d.start >= s.End)
                {
                    next.Add(s);
                    continue;
                }

                // left piece
                if (d.start > s.Start)
                {
                    next.Add(new OtSeg
                    {
                        Start = s.Start,
                        End = d.start,
                        Category = s.Category,
                        Band = s.Band,
                        UiShiftAssignmentId = s.UiShiftAssignmentId,
                        UiShiftDate = s.UiShiftDate,
                        UiShiftFrom = s.UiShiftFrom,
                        UiShiftTo = s.UiShiftTo,
                        ReplacedEmployeeId = s.ReplacedEmployeeId,
                        SlotFillType = s.SlotFillType,
                        ShiftGroupId = s.ShiftGroupId
                    });
                }

                // right piece
                if (d.end < s.End)
                {
                    next.Add(new OtSeg
                    {
                        Start = d.end,
                        End = s.End,
                        Category = s.Category,
                        Band = s.Band,
                        UiShiftAssignmentId = s.UiShiftAssignmentId,
                        UiShiftDate = s.UiShiftDate,
                        UiShiftFrom = s.UiShiftFrom,
                        UiShiftTo = s.UiShiftTo,
                        ReplacedEmployeeId = s.ReplacedEmployeeId,
                        SlotFillType = s.SlotFillType,
                        ShiftGroupId = s.ShiftGroupId
                    });
                }
            }

            result = next;
        }

        return result;
    }

    private static List<OtClaimLine> ToClaimLines(List<OtSeg> segs)
    {
        static OtClaimLine MakeLine(OtSeg s, DateTime start, DateTime end)
        {
            var claimDate = DateOnly.FromDateTime(start);
            var from = TimeOnly.FromDateTime(start);
            var to = TimeOnly.FromDateTime(end);

            var hours = (decimal)(end - start).TotalHours;
            var rate = OtRateTable.GetRate(s.Category, s.Band);

            return new OtClaimLine
            {
                ClaimDate = claimDate,
                From = from,
                To = to,
                Category = s.Category,
                Band = s.Band,
                Hours = hours,
                Rate = rate,
                UiShiftAssignmentId = s.UiShiftAssignmentId,
                UiShiftDate = s.UiShiftDate,
                UiShiftFrom = s.UiShiftFrom,
                UiShiftTo = s.UiShiftTo,
                ReplacedEmployeeId = s.ReplacedEmployeeId,
                SlotFillType = s.SlotFillType,
                ShiftGroupId = s.ShiftGroupId
            };
        }

        var lines = new List<OtClaimLine>();

        foreach (var s in segs.Where(s => s.End > s.Start))
        {
            var cur = s.Start;
            var end = s.End;

            // Ensure claim lines never cross midnight so ClaimDate and night-shift presentation stay correct.
            while (cur.Date < end.Date)
            {
                var midnight = cur.Date.AddDays(1);
                if (midnight > cur)
                    lines.Add(MakeLine(s, cur, midnight));
                cur = midnight;
            }

            if (end > cur)
                lines.Add(MakeLine(s, cur, end));
        }

        return lines
            .OrderBy(x => x.ClaimDate)
            .ThenBy(x => x.From)
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

    private sealed class OtSeg
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }          // End > Start
        public OtCategory Category { get; set; }
        public RateBand Band { get; set; }         // after split

        public int UiShiftAssignmentId { get; set; }
        public DateOnly UiShiftDate { get; set; }
        public TimeOnly UiShiftFrom { get; set; }
        public TimeOnly UiShiftTo { get; set; }

        public int? ReplacedEmployeeId { get; set; }
        public int SlotFillType { get; set; }
        public int ShiftGroupId { get; set; }
    }

    private sealed class RawSeg
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }          // End > Start
        public OtCategory Category { get; set; }

        public int UiShiftAssignmentId { get; set; }
        public DateOnly UiShiftDate { get; set; }
        public TimeOnly UiShiftFrom { get; set; }
        public TimeOnly UiShiftTo { get; set; }

        public int? ReplacedEmployeeId { get; set; }
        public int SlotFillType { get; set; }
        public int ShiftGroupId { get; set; }
    }
}