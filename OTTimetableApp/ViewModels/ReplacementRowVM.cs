namespace OTTimetableApp.ViewModels;

public class ReplacementRowVM
{
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("dd/MM/yyyy");
    public string DayName => Date.ToString("dddd");
    public string Shift { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int SlotIndex { get; set; }
    public string ReplacedBy { get; set; } = "";
    public string Replaces { get; set; } = "";
}
