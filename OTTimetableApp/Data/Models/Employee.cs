namespace OTTimetableApp.Data.Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int? BaseGroupId { get; set; }
    public Group? BaseGroup { get; set; }
}