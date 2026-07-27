using CommunityToolkit.Mvvm.ComponentModel;
using OTTimetableApp.ViewModels;

namespace OTTimetableApp.ViewModels;

/// <summary>
/// Represents one employee row in the Bulk Claim window - both as a selectable checkbox item
/// and (after generation) as a result row showing the employee's total claim.
/// </summary>
public partial class BulkEmployeeSelectionVM : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    [ObservableProperty] private bool isChecked;

    [ObservableProperty] private decimal grandTotal;
    [ObservableProperty] private decimal totalHoursOT;
    [ObservableProperty] private decimal excessWorkingHours;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool hasError;

    public List<ClaimLineVM> Lines { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public string CatatanLampiranE { get; set; } = "";
    public string CatatanLampiranA { get; set; } = "";
}
