using CommunityToolkit.Mvvm.ComponentModel;
using OTTimetableApp.Domain.OT;
using OTTimetableApp.Services;
using System.Collections.ObjectModel;

namespace OTTimetableApp.ViewModels;

public partial class ClaimPreviewVM : ObservableObject
{
    private readonly MonthViewService _monthSvc;
    private readonly EmployeeService _empSvc;
    private readonly OtCalculatorService _otSvc;
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
    public decimal GrandTotal => Claim1125 + Claim125 + Claim15 + Claim175 + Claim20;

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
    [ObservableProperty] private int selectedMonth = 1;
    [ObservableProperty] private int selectedEmployeeId;

    public ClaimPreviewVM(MonthViewService monthSvc, EmployeeService empSvc, OtCalculatorService otSvc)
    {
        _monthSvc = monthSvc;
        _empSvc = empSvc;
        _otSvc = otSvc;
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

        var claimLines = _otSvc.BuildMonthlyClaim(SelectedCalendarId, SelectedEmployeeId, SelectedMonth);

        // Convert OtClaimLine -> ClaimLineVM with rate buckets
        foreach (var l in claimLines)
        {
            var hours = l.Hours;
            var rate = l.Rate;

            var shift = $"{l.From:HH:mm}-{l.To:HH:mm}";
            var cat = CategoryToDisplay(l.Category);

            var vm = new ClaimLineVM
            {
                IsChecked = true,
                Date = l.ClaimDate,
                Category = cat,
                Shift = shift
            };

            // Put hours into correct rate column
            if (rate == 1.125m) vm.H1125 = hours;
            else if (rate == 1.25m) vm.H125 = hours;
            else if (rate == 1.5m) vm.H15 = hours;
            else if (rate == 1.75m) vm.H175 = hours;
            else if (rate == 2.0m) vm.H20 = hours;

            Lines.Add(vm);
        }

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

        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(HourlyRate));

        AllChecked = true;
        AttachLineHandlers();
        RefreshTotals();
    }


}

public class EmployeePick
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}