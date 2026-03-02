namespace OTTimetableApp.Data.Models;

public class Calendar
{
    public int Id { get; set; }

    // Unique name chosen by user
    public string Name { get; set; } = "";

    // The year this calendar is for, e.g. 2026
    public int Year { get; set; }

    // Initial setup for 1st January
    public int InitNightGroupId { get; set; }     // 22-07 group on Jan 1
    public int InitMorningGroupId { get; set; }   // 07-15 group on Jan 1
    public int InitEveningGroupId { get; set; }   // 14-23 group on Jan 1

    // Marks calendar as generated/locked
    public bool IsGenerated { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}