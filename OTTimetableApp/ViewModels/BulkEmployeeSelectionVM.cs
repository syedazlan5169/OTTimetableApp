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
    [ObservableProperty] private decimal oneThirdGaji;
    [ObservableProperty] private decimal hourlyRate;

    public List<ClaimLineVM> Lines { get; set; } = new();
    public string CatatanLampiranE { get; set; } = "";
    public string CatatanLampiranA { get; set; } = "";

    public bool IsClaimExceedsOneThird => GrandTotal > OneThirdGaji && OneThirdGaji > 0;

    public decimal ExceedClaimAmount => IsClaimExceedsOneThird ? GrandTotal - OneThirdGaji : 0m;

    public decimal ExceedClaimHours
    {
        get
        {
            if (!IsClaimExceedsOneThird) return 0m;

            if (HourlyRate <= 0) return 0m;

            return ExceedClaimAmount / HourlyRate;
        }
    }

    partial void OnGrandTotalChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsClaimExceedsOneThird));
        OnPropertyChanged(nameof(ExceedClaimAmount));
        OnPropertyChanged(nameof(ExceedClaimHours));
    }

    partial void OnOneThirdGajiChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsClaimExceedsOneThird));
        OnPropertyChanged(nameof(ExceedClaimAmount));
        OnPropertyChanged(nameof(ExceedClaimHours));
    }

    partial void OnHourlyRateChanged(decimal value)
    {
        OnPropertyChanged(nameof(ExceedClaimHours));
    }
}

