using CommunityToolkit.Mvvm.ComponentModel;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace OTTimetableApp.ViewModels;

public partial class BulkClaimVM : ObservableObject
{
    private readonly MonthViewService _monthSvc;
    private readonly EmployeeService _empSvc;
    private readonly ClaimGenerationService _claimGenSvc;
    private readonly ExcelExportService _excelSvc;
    private readonly AuditLogService _auditSvc;

    public ObservableCollection<CalendarOptionVM> Calendars { get; } = new();
    public ObservableCollection<MonthOptionVM> Months { get; } =
        new(Enumerable.Range(1, 12).Select(m => new MonthOptionVM
        {
            Month = m,
            Name = new DateTime(2026, m, 1).ToString("MMMM")
        }));

    public ObservableCollection<BulkEmployeeSelectionVM> Employees { get; } = new();

    [ObservableProperty] private int selectedCalendarId;
    [ObservableProperty] private int selectedMonth;

    [ObservableProperty] private string catatanLampiranE = "";
    [ObservableProperty] private string catatanLampiranA = "";

    [ObservableProperty] private bool allChecked = true;

    public bool HasResults => Employees.Any(e => !string.IsNullOrEmpty(e.Status));

    public BulkClaimVM(MonthViewService monthSvc, EmployeeService empSvc, ClaimGenerationService claimGenSvc, ExcelExportService excelSvc, AuditLogService auditSvc)
    {
        _monthSvc = monthSvc;
        _empSvc = empSvc;
        _claimGenSvc = claimGenSvc;
        _excelSvc = excelSvc;
        _auditSvc = auditSvc;

        var settings = ClaimSettings.Load();
        CatatanLampiranE = settings.CatatanLampiranE;
        CatatanLampiranA = settings.CatatanLampiranA;

        SelectedMonth = DateTime.Today.Month;
    }

    partial void OnAllCheckedChanged(bool value)
    {
        foreach (var emp in Employees)
            emp.IsChecked = value;
    }

    public void LoadLookups()
    {
        Calendars.Clear();
        foreach (var c in _monthSvc.ListCalendars())
            Calendars.Add(c);

        Employees.Clear();
        foreach (var e in _empSvc.GetAll().Where(x => x.IsActive).OrderBy(x => x.Name))
        {
            Employees.Add(new BulkEmployeeSelectionVM
            {
                Id = e.Id,
                Name = e.Name,
                IsChecked = AllChecked
            });
        }

        if (SelectedCalendarId == 0 && Calendars.Count > 0) SelectedCalendarId = Calendars[0].Id;
    }

    private decimal ComputeHourlyRate(int employeeId)
    {
        var emp = _empSvc.GetAll().FirstOrDefault(x => x.Id == employeeId);
        if (emp?.Salary == null)
            return 1m;

        var raw = emp.Salary.Value * 12m / 2504m;
        return Math.Truncate(raw * 100m) / 100m;
    }

    /// <summary>
    /// Generates claims for all checked employees. Employees that fail (no group assigned,
    /// no claim lines, etc.) are marked with an error status but do not stop the batch.
    /// </summary>
    public void GenerateAll()
    {
        if (SelectedCalendarId == 0)
            throw new InvalidOperationException("Select a calendar.");

        var selected = Employees.Where(e => e.IsChecked).ToList();
        if (selected.Count == 0)
            throw new InvalidOperationException("Please select at least one employee.");

        foreach (var emp in selected)
        {
            try
            {
                var result = _claimGenSvc.BuildClaimLines(SelectedCalendarId, emp.Id, SelectedMonth);
                emp.HourlyRate = ComputeHourlyRate(emp.Id);
                emp.ExcessWorkingHours = result.ExcessWorkingHours;
                emp.Lines = result.Lines;
                emp.CatatanLampiranE = CatatanLampiranE;
                emp.CatatanLampiranA = CatatanLampiranA;

                var checkedLines = result.Lines.Where(l => l.IsChecked).ToList();

                decimal total1125 = checkedLines.Sum(x => x.H1125 ?? 0);
                decimal total125 = checkedLines.Sum(x => x.H125 ?? 0);
                decimal total15 = checkedLines.Sum(x => x.H15 ?? 0);
                decimal total175 = checkedLines.Sum(x => x.H175 ?? 0);
                decimal total20 = checkedLines.Sum(x => x.H20 ?? 0);

                decimal grandTotal =
                    total1125 * 1.125m * emp.HourlyRate
                    + total125 * 1.25m * emp.HourlyRate
                    + total15 * 1.5m * emp.HourlyRate
                    + total175 * 1.75m * emp.HourlyRate
                    + total20 * 2.0m * emp.HourlyRate
                    + emp.ExcessWorkingHours * 1.25m * emp.HourlyRate;

                emp.GrandTotal = grandTotal;
                emp.TotalHoursOT = total1125 + total125 + total15 + total175 + total20 + emp.ExcessWorkingHours;
                emp.HasError = false;
                emp.Status = checkedLines.Count == 0 ? "No OT lines" : "Generated";
            }
            catch (Exception ex)
            {
                emp.HasError = true;
                emp.GrandTotal = 0;
                emp.Status = ex.Message;
            }
        }

        try
        {
            _auditSvc.Log("BulkClaimGenerated", $"Bulk generated claims for {selected.Count} employee(s) for calendarId:{SelectedCalendarId} month:{SelectedMonth}");
        }
        catch
        {
            // Swallow audit logging errors so UI action is not disrupted
        }

        OnPropertyChanged(nameof(HasResults));
    }

    /// <summary>
    /// Exports one Excel file per successfully-generated employee into the chosen folder.
    /// Returns a summary of exported/skipped employees.
    /// </summary>
    public (int exported, List<string> skipped) ExportAll(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        int exported = 0;
        var skipped = new List<string>();

        foreach (var emp in Employees.Where(e => e.IsChecked))
        {
            var checkedLines = emp.Lines.Where(l => l.IsChecked).ToList();

            if (emp.HasError || checkedLines.Count == 0)
            {
                skipped.Add($"{emp.Name} ({(string.IsNullOrEmpty(emp.Status) ? "not generated" : emp.Status)})");
                continue;
            }

            // Excel template can only handle a limited number of rows (see single export logic)
            const int startRow = 20;
            const int maxRow = 53;
            const int maxAvailableRows = maxRow - startRow + 1;

            int totalRowsNeeded = 0;
            foreach (var dateGroup in checkedLines.GroupBy(l => l.Date))
            {
                int shiftsCount = dateGroup.Count();
                int rowPairsNeeded = (int)Math.Ceiling(shiftsCount / 2.0);
                totalRowsNeeded += rowPairsNeeded * 2;
            }

            if (totalRowsNeeded > maxAvailableRows)
            {
                skipped.Add($"{emp.Name} (too many OT lines: {totalRowsNeeded} rows needed)");
                continue;
            }

            var fileName = $"OT_Claim_{emp.Name}_{DateTime.Now:yyyyMMdd}.xlsx";
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
                fileName = fileName.Replace(c, '_');

            var outputPath = Path.Combine(folderPath, fileName);

            _excelSvc.ExportClaim(SelectedCalendarId, SelectedMonth, emp.Id, emp.HourlyRate, emp.ExcessWorkingHours, checkedLines, emp.CatatanLampiranE, emp.CatatanLampiranA, outputPath);
            exported++;
        }

        new ClaimSettings { CatatanLampiranE = CatatanLampiranE, CatatanLampiranA = CatatanLampiranA }.Save();

        try
        {
            _auditSvc.Log("BulkClaimExported", $"Bulk exported {exported} claim(s) to {folderPath}", SelectedCalendarId);
        }
        catch
        {
            // Ignore audit logging failures
        }

        return (exported, skipped);
    }
}
