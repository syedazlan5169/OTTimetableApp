using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OTTimetableApp.Data.Models;
using OTTimetableApp.Services;

namespace OTTimetableApp.ViewModels;

public class EmployeeManagerVM : INotifyPropertyChanged
{
    private readonly EmployeeService _svc;

    public ObservableCollection<Employee> Employees { get; } = new();
    public ObservableCollection<GroupOptionVM> Groups { get; } = new();

    private string _search = "";
    public string Search
    {
        get => _search;
        set { _search = value; OnPropertyChanged(); ApplyFilter(); }
    }

    private List<Employee> _allEmployees = new();

    private Employee? _selectedEmployee;
    public Employee? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            _selectedEmployee = value;
            OnPropertyChanged();
            LoadToEditor(value);
        }
    }

    private Employee _edit = new() { IsActive = true };
    public Employee Edit
    {
        get => _edit;
        set { _edit = value; OnPropertyChanged(); }
    }

    public EmployeeManagerVM(EmployeeService svc)
    {
        _svc = svc;
    }

    public void Load()
    {
        Groups.Clear();
        Groups.Add(new GroupOptionVM { Id = null, Name = "(Not assigned)" });

        foreach (var g in _svc.GetGroups())
            Groups.Add(new GroupOptionVM { Id = g.Id, Name = g.Name });

        _allEmployees = _svc.GetAll();
        ApplyFilter();

        if (Employees.Count > 0 && SelectedEmployee == null)
            SelectedEmployee = Employees[0];
    }

    private void ApplyFilter()
    {
        Employees.Clear();

        IEnumerable<Employee> filtered = _allEmployees;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(e =>
                (e.Name ?? "").ToLowerInvariant().Contains(s) ||
                (e.FullName ?? "").ToLowerInvariant().Contains(s) ||
                (e.IcNo ?? "").ToLowerInvariant().Contains(s));
        }

        foreach (var e in filtered.OrderBy(x => x.Name))
            Employees.Add(e);
    }

    public void NewEmployee()
    {
        Edit = new Employee
        {
            IsActive = true,
            BaseGroupId = null
        };

        OnPropertyChanged(nameof(BaseGroupDisplay));
        RefreshComputed();
        SelectedEmployee = null;
    }

    public void Save()
    {
        _svc.Save(Edit);
        RefreshComputed();
        ReloadAfterSave(Edit.Id);
    }

    public void DeleteSelected()
    {
        if (Edit.Id == 0) return;

        _svc.Delete(Edit.Id);
        Load();
        if (Employees.Count > 0) SelectedEmployee = Employees[0];
        else NewEmployee();
    }

    private void ReloadAfterSave(int savedId)
    {
        Load();

        if (savedId != 0)
        {
            var found = Employees.FirstOrDefault(x => x.Id == savedId);
            if (found != null) SelectedEmployee = found;
        }
    }

    private void LoadToEditor(Employee? src)
    {
        if (src == null) return;

        // Clone into Edit so grid selection doesn’t auto-save
        Edit = new Employee
        {
            Id = src.Id,
            Name = src.Name,
            FullName = src.FullName,

            IcNo = src.IcNo,
            PikNo = src.PikNo,
            Branch = src.Branch,
            PhoneNo = src.PhoneNo,

            Salary = src.Salary,
            SalaryNo = src.SalaryNo,

            BankAccountNo = src.BankAccountNo,
            BankName = src.BankName,

            BaseGroupId = src.BaseGroupId,
            IsActive = src.IsActive
        };

        OnPropertyChanged(nameof(BaseGroupDisplay));
        RefreshComputed();
    }

    public string BaseGroupDisplay
    {
        get
        {
            if (Edit.BaseGroupId == null) return "(Not assigned)";
            var g = Groups.FirstOrDefault(x => x.Id == Edit.BaseGroupId);
            return g?.Name ?? "(Unknown)";
        }
    }

    public string HourlyRateDisplay
    {
        get
        {
            var sal = Edit?.Salary;
            if (sal == null) return "";

            var raw = sal.Value * 12m / 2504m;
            var truncated = Math.Truncate(raw * 100m) / 100m;
            return truncated.ToString("0.00");
        }
    }

    public void RefreshComputed()
    {
        OnPropertyChanged(nameof(HourlyRateDisplay));
        OnPropertyChanged(nameof(BaseGroupDisplay));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}