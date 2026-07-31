using CommunityToolkit.Mvvm.ComponentModel;
using OTTimetableApp.Domain.OT;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Services;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;

namespace OTTimetableApp.ViewModels;

public partial class ClaimPreviewVM : ObservableObject, IDisposable
{
    private readonly MonthViewService _monthSvc;
    private readonly EmployeeService _empSvc;
    private readonly OtCalculatorService _otSvc;
    private readonly ExcelExportService _excelSvc;
    private readonly AuditLogService _auditSvc;
    private readonly ClaimGenerationService _claimGenSvc;
    public decimal Total1125 => Lines.Where(x => x.IsChecked).Sum(x => x.H1125 ?? 0);
    public decimal Total125 => Lines.Where(x => x.IsChecked).Sum(x => x.H125 ?? 0);
    public decimal Total15 => Lines.Where(x => x.IsChecked).Sum(x => x.H15 ?? 0);
    public decimal Total175 => Lines.Where(x => x.IsChecked).Sum(x => x.H175 ?? 0);
    public decimal Total20 => Lines.Where(x => x.IsChecked).Sum(x => x.H20 ?? 0);
    public decimal Claim1125 => Total1125 * 1.125m * HourlyRate;
    public decimal Claim125 => Total125 * 1.25m * HourlyRate;
    public decimal Claim15 => Total15 * 1.5m * HourlyRate;
    public decimal Claim175 => Total175 * 1.75m * HourlyRate;
    public decimal Claim20 => Total20 * 2.0m * HourlyRate;
    public decimal ExcessWorkingHoursTotal => ExcessWorkingHours * 1.25m * HourlyRate;
    public decimal GrandTotal => Claim1125 + Claim125 + Claim15 + Claim175 + Claim20 + ExcessWorkingHoursTotal;
    public decimal TotalHoursOT => Total1125 + Total125 + Total15 + Total175 + Total20 + ExcessWorkingHours;
    public decimal TotalHoursOTAdjusted =>
        Total1125 * 1.125m
        + Total125 * 1.25m
        + Total15 * 1.5m
        + Total175 * 1.75m
        + Total20 * 2.0m
        + ExcessWorkingHours * 1.25m;

    public string TotalHoursOTDisplay => $"{TotalHoursOT:N2}({TotalHoursOTAdjusted:N2})";

    [ObservableProperty] private decimal excessWorkingHours;
    [ObservableProperty] private string catatanLampiranE;
    [ObservableProperty] private string catatanLampiranA;

    private bool _isBulkUpdating;

    private bool _allChecked = true;
    public bool AllChecked
    {
        get => _allChecked;
        set
        {
            if (_allChecked == value) return;

            _allChecked = value;
            OnPropertyChanged();

            if (_isBulkUpdating) return;

            _isBulkUpdating = true;
            foreach (var line in Lines)
                line.IsChecked = value;
            _isBulkUpdating = false;

            RefreshTotals();
        }
    }

    public ObservableCollection<CalendarOptionVM> Calendars { get; } = new();
    public ObservableCollection<MonthOptionVM> Months { get; } =
        new(Enumerable.Range(1, 12).Select(m => new MonthOptionVM
        {
            Month = m,
            Name = new DateTime(2026, m, 1).ToString("MMMM")
        }));

    public ObservableCollection<EmployeePick> Employees { get; } = new();
    public ObservableCollection<ClaimLineVM> Lines { get; } = new();

    [ObservableProperty] private int selectedCalendarId;
    [ObservableProperty] private int selectedMonth;
    [ObservableProperty] private int selectedEmployeeId;

    public ClaimPreviewVM(MonthViewService monthSvc, EmployeeService empSvc, OtCalculatorService otSvc, ExcelExportService excelSvc, AuditLogService auditSvc, ClaimGenerationService claimGenSvc)
    {
        _monthSvc = monthSvc;
        _empSvc = empSvc;
        _otSvc = otSvc;
        _excelSvc = excelSvc;
        _auditSvc = auditSvc;
        _claimGenSvc = claimGenSvc;

        var settings = ClaimSettings.Load();
        CatatanLampiranE = settings.CatatanLampiranE;
        CatatanLampiranA = settings.CatatanLampiranA;

        // Auto-select current month when window opens
        SelectedMonth = DateTime.Today.Month;
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(Total1125));
        OnPropertyChanged(nameof(Total125));
        OnPropertyChanged(nameof(Total15));
        OnPropertyChanged(nameof(Total175));
        OnPropertyChanged(nameof(Total20));

        OnPropertyChanged(nameof(Claim1125));
        OnPropertyChanged(nameof(Claim125));
        OnPropertyChanged(nameof(Claim15));
        OnPropertyChanged(nameof(Claim175));
        OnPropertyChanged(nameof(Claim20));

        OnPropertyChanged(nameof(ExcessWorkingHours));
        OnPropertyChanged(nameof(ExcessWorkingHoursTotal));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TotalHoursOT));
        OnPropertyChanged(nameof(TotalHoursOTAdjusted));
        OnPropertyChanged(nameof(TotalHoursOTDisplay));
        OnPropertyChanged(nameof(HourlyRate));
        OnPropertyChanged(nameof(OneThirdGaji));
    }

    private void AttachLineHandlers()
    {
        foreach (var line in Lines)
        {
            line.PropertyChanged -= Line_PropertyChanged;
            line.PropertyChanged += Line_PropertyChanged;
        }
    }

    private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ClaimLineVM.IsChecked)) return;

        if (!_isBulkUpdating)
        {
            _allChecked = Lines.Count > 0 && Lines.All(x => x.IsChecked);
            OnPropertyChanged(nameof(AllChecked));
        }

        RefreshTotals();
    }

    public decimal HourlyRate
    {
        get
        {
            var emp = _empSvc.GetAll().FirstOrDefault(x => x.Id == SelectedEmployeeId);
            if (emp?.Salary == null)
                return 1m;

            var raw = emp.Salary.Value * 12m / 2504m;
            return Math.Truncate(raw * 100m) / 100m;
        }
    }

    public decimal OneThirdGaji
    {
        get
        {
            var emp = _empSvc.GetAll().FirstOrDefault(x => x.Id == SelectedEmployeeId);
            if (emp?.Salary == null)
                return 0m;

            return emp.Salary.Value / 3m;
        }
    }

    public void LoadLookups()
    {
        Calendars.Clear();
        foreach (var c in _monthSvc.ListCalendars())
            Calendars.Add(c);

        Employees.Clear();
        foreach (var e in _empSvc.GetAll().Where(x => x.IsActive))
            Employees.Add(new EmployeePick { Id = e.Id, Name = e.Name });

        if (SelectedCalendarId == 0 && Calendars.Count > 0) SelectedCalendarId = Calendars[0].Id;
        if (SelectedEmployeeId == 0 && Employees.Count > 0) SelectedEmployeeId = Employees[0].Id;
    }

    public void Generate()
    {
        Lines.Clear();

        if (SelectedCalendarId == 0) throw new InvalidOperationException("Select a calendar.");
        if (SelectedEmployeeId == 0) throw new InvalidOperationException("Select an employee.");

        var employee = _empSvc.GetAll().FirstOrDefault(e => e.Id == SelectedEmployeeId);
        if (employee == null)
            throw new InvalidOperationException("Employee not found");

        var result = _claimGenSvc.BuildClaimLines(SelectedCalendarId, SelectedEmployeeId, SelectedMonth);
        ExcessWorkingHours = result.ExcessWorkingHours;

        foreach (var line in result.Lines)
        {
            Lines.Add(line);
        }

        AllChecked = true;
        AttachLineHandlers();
        RefreshTotals();

        try
        {
            _auditSvc.Log("ClaimGenerated", $"Generated claim for {employee.Name} for calendarId:{SelectedCalendarId} month:{SelectedMonth}");
        }
        catch
        {
            // Swallow audit logging errors so UI action is not disrupted
        }
    }

    public void ExportToExcel()
    {
        if (SelectedEmployeeId == 0)
            throw new InvalidOperationException("Please select an employee.");

        if (Lines.Count == 0)
            throw new InvalidOperationException("Please generate the claim first.");

        var employee = _empSvc.GetAll().FirstOrDefault(e => e.Id == SelectedEmployeeId);
        if (employee == null)
            throw new InvalidOperationException("Employee not found.");

        var checkedLines = Lines.Where(l => l.IsChecked).ToList();
        if (checkedLines.Count == 0)
            throw new InvalidOperationException("Please check at least one OT line to export.");

        // Calculate total rows needed (start at row 20, max row 53 = 34 rows available)
        const int startRow = 20;
        const int maxRow = 53;
        const int maxAvailableRows = maxRow - startRow + 1; // 34 rows

        int totalRowsNeeded = 0;
        var groupedByDate = checkedLines.GroupBy(l => l.Date);
        foreach (var dateGroup in groupedByDate)
        {
            int shiftsCount = dateGroup.Count();
            // Each date needs ceil(shiftsCount / 2) * 2 rows
            int rowPairsNeeded = (int)Math.Ceiling(shiftsCount / 2.0);
            totalRowsNeeded += rowPairsNeeded * 2;
        }

        if (totalRowsNeeded > maxAvailableRows)
        {
            System.Windows.MessageBox.Show(
                $"Too many OT lines selected! The Excel template can only handle {maxAvailableRows} rows, but {totalRowsNeeded} rows are needed.\n\n" +
                "Please uncheck some OT lines to reduce the number of rows and try again.",
                "Export Limit Exceeded",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var claimsFolder = Path.Combine(documentsPath, "Claims");

        if (!Directory.Exists(claimsFolder))
            Directory.CreateDirectory(claimsFolder);

        var saveDialog = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"OT_Claim_{employee.Name}_{DateTime.Now:yyyyMMdd}.xlsx",
            DefaultExt = ".xlsx",
            InitialDirectory = claimsFolder
        };

        if (saveDialog.ShowDialog() == true)
        {
            _excelSvc.ExportClaim(SelectedCalendarId, SelectedMonth, SelectedEmployeeId, HourlyRate, ExcessWorkingHours, checkedLines, CatatanLampiranE, CatatanLampiranA, saveDialog.FileName);

            new ClaimSettings { CatatanLampiranE = CatatanLampiranE, CatatanLampiranA = CatatanLampiranA }.Save();

            System.Windows.MessageBox.Show("Export successful!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

            try
            {
                _auditSvc.Log("ClaimExported", $"Exported claim for {employee.Name} with {checkedLines.Count} lines", SelectedCalendarId);
            }
            catch
            {
                // Ignore audit logging failures
            }

            // Open the folder location
            var folderPath = Path.GetDirectoryName(saveDialog.FileName);
            if (!string.IsNullOrEmpty(folderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            }
        }
    }

    private void DetachLineHandlers()
    {
        foreach (var line in Lines)
        {
            line.PropertyChanged -= Line_PropertyChanged;
        }
    }

    public void Dispose()
    {
        DetachLineHandlers();
        Lines.Clear();
    }
}

public class EmployeePick
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}