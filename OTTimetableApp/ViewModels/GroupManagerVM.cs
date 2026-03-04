using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OTTimetableApp.Data.Models;
using OTTimetableApp.Services;

namespace OTTimetableApp.ViewModels;

public class GroupManagerVM : INotifyPropertyChanged
{
    private readonly GroupManagerService _svc;

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<EmployeePickVM> Employees { get; } = new();

    public ObservableCollection<GroupSlotRowVM> Slots { get; } = new();

    private int _selectedGroupId;
    public int SelectedGroupId
    {
        get => _selectedGroupId;
        set { _selectedGroupId = value; OnPropertyChanged(); LoadSelectedGroup(); }
    }

    private int _slotCapacity = 5;
    public int SlotCapacity
    {
        get => _slotCapacity;
        set { _slotCapacity = value; OnPropertyChanged(); }
    }

    public string GroupTitle { get; private set; } = "";

    public GroupManagerVM(GroupManagerService svc)
    {
        _svc = svc;
    }

    public void Load()
    {
        Groups.Clear();
        foreach (var g in _svc.GetGroups())
            Groups.Add(g);

        Employees.Clear();
        Employees.Add(new EmployeePickVM { Id = null, Name = "(Empty)" });
        foreach (var e in _svc.GetEmployees())
            Employees.Add(new EmployeePickVM { Id = e.Id, Name = e.Name });

        if (SelectedGroupId == 0 && Groups.Count > 0)
            SelectedGroupId = Groups[0].Id;
        else
            LoadSelectedGroup();
    }

    private void LoadSelectedGroup()
    {
        if (SelectedGroupId == 0) return;

        var (group, members) = _svc.LoadGroup(SelectedGroupId);

        GroupTitle = group.Name;
        OnPropertyChanged(nameof(GroupTitle));

        SlotCapacity = group.SlotCapacity;

        Slots.Clear();
        for (int i = 1; i <= group.SlotCapacity; i++)
        {
            var empId = members.FirstOrDefault(x => x.SlotIndex == i)?.EmployeeId;

            Slots.Add(new GroupSlotRowVM
            {
                SlotIndex = i,
                EmployeeId = empId
            });
        }
    }

    public void SaveCapacity()
    {
        _svc.SaveCapacity(SelectedGroupId, SlotCapacity);
        LoadSelectedGroup();
    }

    public void SetSlot(int slotIndex, int? employeeId)
    {
        _svc.SetSlot(SelectedGroupId, slotIndex, employeeId);
        LoadSelectedGroup();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class EmployeePickVM
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
}

public class GroupSlotRowVM
{
    public int SlotIndex { get; set; }
    public int? EmployeeId { get; set; }
}