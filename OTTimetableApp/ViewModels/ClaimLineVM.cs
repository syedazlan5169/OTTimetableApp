using CommunityToolkit.Mvvm.ComponentModel;

namespace OTTimetableApp.ViewModels;

public partial class ClaimLineVM : ObservableObject
{
    [ObservableProperty] private bool isChecked = true;

    public DateOnly Date { get; set; }
    public string Category { get; set; } = "";
    public string Shift { get; set; } = "";

    // Put duration into the correct rate column (others blank/0.00)
    public decimal H1125 { get; set; }
    public decimal H125 { get; set; }
    public decimal H15 { get; set; }
    public decimal H175 { get; set; }
    public decimal H20 { get; set; }
}