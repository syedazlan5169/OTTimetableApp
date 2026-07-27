using OTTimetableApp.Domain.OT;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp.Services;

public class ClaimGenerationResult
{
    public List<ClaimLineVM> Lines { get; set; } = new();
    public decimal ExcessWorkingHours { get; set; }
}

/// <summary>
/// Builds display-ready claim lines (with remarks) for an employee, for a given calendar/month.
/// Shared between the single-employee Create Claim flow and the Bulk Claim flow so remark
/// generation rules stay consistent in one place.
/// </summary>
public class ClaimGenerationService
{
    private readonly MonthViewService _monthSvc;
    private readonly EmployeeService _empSvc;
    private readonly OtCalculatorService _otSvc;

    public ClaimGenerationService(MonthViewService monthSvc, EmployeeService empSvc, OtCalculatorService otSvc)
    {
        _monthSvc = monthSvc;
        _empSvc = empSvc;
        _otSvc = otSvc;
    }

    private static string CategoryToDisplay(OtCategory cat)
    {
        return cat switch
        {
            OtCategory.WorkingDay => "Working Day",
            OtCategory.KelepasanGiliran => "Kelepasan Giliran",
            OtCategory.KelepasanAm => "Kelepasan Am",
            OtCategory.KelepasanAmGantian => "Kelepasan Am Gantian",
            _ => cat.ToString()
        };
    }

    private static string GetGroupLabel(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return groupName;

        if (groupName.StartsWith("KUMPULAN ", StringComparison.OrdinalIgnoreCase))
            return groupName.Substring("KUMPULAN ".Length).Trim();

        if (groupName.StartsWith("KUMPULAN", StringComparison.OrdinalIgnoreCase))
            return groupName.Substring("KUMPULAN".Length).Trim();

        return groupName;
    }

    private static string LeaveReasonToDisplay(OTTimetableApp.Data.Models.LeaveReason reason)
    {
        return reason switch
        {
            OTTimetableApp.Data.Models.LeaveReason.Bercuti => "Cuti",
            OTTimetableApp.Data.Models.LeaveReason.Berkursus => "Kursus",
            _ => reason.ToString()
        };
    }

    /// <summary>
    /// Builds claim lines for a single employee. Throws InvalidOperationException if the
    /// employee is not found or not assigned to a group.
    /// </summary>
    public ClaimGenerationResult BuildClaimLines(int calendarId, int employeeId, int month)
    {
        var employee = _empSvc.GetAll().FirstOrDefault(e => e.Id == employeeId);
        if (employee == null)
            throw new InvalidOperationException("Employee not found");

        var claimResult = _otSvc.BuildMonthlyClaim(calendarId, employeeId, month);
        var claimLines = claimResult.ClaimLines;

        var result = new ClaimGenerationResult
        {
            ExcessWorkingHours = claimResult.ExcessWorkingHours
        };

        var baseGroupMap = _empSvc.GetBaseGroupMap();
        if (!baseGroupMap.TryGetValue(employeeId, out var baseGroupId))
            throw new InvalidOperationException("Employee is not assigned to any group yet. Please assign the employee in Group Manager before generating claim.");

        var workingShiftLabelCache = new Dictionary<DateOnly, string>();
        string GetWorkingShiftLabel(DateOnly date)
        {
            if (workingShiftLabelCache.TryGetValue(date, out var label))
                return label;

            label = _monthSvc.GetWorkingShiftLabel(calendarId, baseGroupId, date);
            workingShiftLabelCache[date] = label;
            return label;
        }

        // Merge presentation ONLY within an original shift assignment.
        // (Still keep different categories separate to avoid misleading Category display.)
        var grouped = claimLines
            .GroupBy(l => new { l.UiShiftAssignmentId, l.Category })
            .OrderBy(g => g.First().UiShiftDate)
            .ThenBy(g => g.First().UiShiftFrom);

        var output = new List<(DateOnly Date, TimeOnly From, ClaimLineVM Vm)>();

        ClaimLineVM BuildVm(DateOnly date, OtCategory category, string shift, IEnumerable<OtClaimLine> lines)
        {
            var categoryDisplay = category == OtCategory.WorkingDay
                ? GetWorkingShiftLabel(date)
                : CategoryToDisplay(category);

            var firstLine = lines.First();
            string remark = "";

            // Generate remark based on SlotFillType
            if (firstLine.SlotFillType == 2) // Replacement
            {
                if (firstLine.ReplacedEmployeeId.HasValue)
                {
                    var replacedEmp = _empSvc.GetAll().FirstOrDefault(e => e.Id == firstLine.ReplacedEmployeeId.Value);
                    if (replacedEmp != null)
                    {
                        var replacedName = !string.IsNullOrWhiteSpace(replacedEmp.AlternateName)
                            ? replacedEmp.AlternateName
                            : replacedEmp.Name;

                        var groups = _empSvc.GetGroups();
                        var group = groups.FirstOrDefault(g => g.Id == firstLine.ShiftGroupId);

                        var leaveReasonText = firstLine.LeaveReason.HasValue
                            ? LeaveReasonToDisplay(firstLine.LeaveReason.Value)
                            : null;

                        remark = group != null
                            ? $"Ganti {replacedName} Kump {GetGroupLabel(group.Name)}"
                            : $"Ganti {replacedName}";

                        if (!string.IsNullOrEmpty(leaveReasonText))
                            remark += $" ({leaveReasonText})";
                    }
                }
            }
            else if (firstLine.SlotFillType == 3) // EmptyFill
            {
                var groups = _empSvc.GetGroups();
                var group = groups.FirstOrDefault(g => g.Id == firstLine.ShiftGroupId);
                if (group != null)
                {
                    remark = $"Isi Kekosongan {group.Name}";
                }
            }
            else
            {
                // For Kelepasan Am & Gantian, show category if not replacing/filling
                if (category == OtCategory.KelepasanAm || category == OtCategory.KelepasanAmGantian)
                {
                    remark = CategoryToDisplay(category);
                }
            }

            var vm = new ClaimLineVM
            {
                IsChecked = true,
                Date = date,
                Category = categoryDisplay,
                Shift = shift,

                H1125 = lines.Where(x => x.Rate == 1.125m).Sum(x => x.Hours),
                H125 = lines.Where(x => x.Rate == 1.25m).Sum(x => x.Hours),
                H15 = lines.Where(x => x.Rate == 1.5m).Sum(x => x.Hours),
                H175 = lines.Where(x => x.Rate == 1.75m).Sum(x => x.Hours),
                H20 = lines.Where(x => x.Rate == 2.0m).Sum(x => x.Hours),

                Remark = remark
            };

            if (vm.H1125 == 0) vm.H1125 = null;
            if (vm.H125 == 0) vm.H125 = null;
            if (vm.H15 == 0) vm.H15 = null;
            if (vm.H175 == 0) vm.H175 = null;
            if (vm.H20 == 0) vm.H20 = null;

            return vm;
        }

        foreach (var g in grouped)
        {
            var first = g.First();

            // Special presentation rule for night shift: show it split per calendar date
            // (22:00-00:00 on previous day, 00:00-07:00 on shift date)
            bool crossesMidnight = first.UiShiftFrom > first.UiShiftTo;

            if (!crossesMidnight)
            {
                output.Add((
                    Date: first.UiShiftDate,
                    From: first.UiShiftFrom,
                    Vm: BuildVm(
                        date: first.UiShiftDate,
                        category: first.Category,
                        shift: $"{first.UiShiftFrom:HH:mm} - {first.UiShiftTo:HH:mm}",
                        lines: g)));

                continue;
            }

            var shiftDate = first.UiShiftDate;
            var prevDate = shiftDate.AddDays(-1);

            var byClaimDate = g
                .GroupBy(x => x.ClaimDate)
                .OrderBy(x => x.Key);

            foreach (var dg in byClaimDate)
            {
                var date = dg.Key;

                TimeOnly from;
                TimeOnly to;

                if (date == prevDate)
                {
                    from = first.UiShiftFrom;
                    to = new TimeOnly(0, 0);
                }
                else if (date == shiftDate)
                {
                    from = new TimeOnly(0, 0);
                    to = first.UiShiftTo;
                }
                else
                {
                    // fallback (shouldn't normally happen)
                    from = dg.Min(x => x.From);
                    to = dg.Max(x => x.To);
                }

                output.Add((
                    Date: date,
                    From: from,
                    Vm: BuildVm(
                        date: date,
                        category: first.Category,
                        shift: $"{from:HH:mm} - {to:HH:mm}",
                        lines: dg)));
            }
        }

        foreach (var item in output
            .OrderBy(x => x.Date)
            .ThenBy(x => x.From))
        {
            result.Lines.Add(item.Vm);
        }

        return result;
    }
}
