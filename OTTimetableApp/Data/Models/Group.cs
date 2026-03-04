namespace OTTimetableApp.Data.Models;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = "";   // e.g. "KUMPULAN A"
    public int SlotCount { get; set; } = 5;
    public int SlotCapacity { get; set; } = 5;
}