using CommunityToolkit.Mvvm.ComponentModel;
using OTTimetableApp.Domain.OT;
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

    [ObservableProperty] private decimal excessWorkingHours;

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

    public ClaimPreviewVM(MonthViewService monthSvc, EmployeeService empSvc, OtCalculatorService otSvc, ExcelExportService excelSvc)
    {
        _monthSvc = monthSvc;
        _empSvc = empSvc;
        _otSvc = otSvc;
        _excelSvc = excelSvc;

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
        OnPropertyChanged(nameof(HourlyRate));
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

    public void Generate()
    {
        Lines.Clear();

        if (SelectedCalendarId == 0) throw new InvalidOperationException("Select a calendar.");
        if (SelectedEmployeeId == 0) throw new InvalidOperationException("Select an employee.");

        var claimResult = _otSvc.BuildMonthlyClaim(SelectedCalendarId, SelectedEmployeeId, SelectedMonth);
        var claimLines = claimResult.ClaimLines;
        ExcessWorkingHours = claimResult.ExcessWorkingHours;

        var baseGroupMap = _empSvc.GetBaseGroupMap();
        if (!baseGroupMap.TryGetValue(SelectedEmployeeId, out var baseGroupId))
            throw new InvalidOperationException("Employee is not assigned to any group yet. Please assign the employee in Group Manager before generating claim.");

        var workingShiftLabelCache = new Dictionary<DateOnly, string>();
        string GetWorkingShiftLabel(DateOnly date)
        {
            if (workingShiftLabelCache.TryGetValue(date, out var label))
                return label;

            label = _monthSvc.GetWorkingShiftLabel(SelectedCalendarId, baseGroupId, date);
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
                        remark = $"Ganti {replacedEmp.Name}";
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
            Lines.Add(item.Vm);
        }

        AllChecked = true;
        AttachLineHandlers();
        RefreshTotals();
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
            _excelSvc.ExportClaim(SelectedCalendarId, SelectedMonth, SelectedEmployeeId, HourlyRate, ExcessWorkingHours, checkedLines, saveDialog.FileName);
            System.Windows.MessageBox.Show("Export successful!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

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