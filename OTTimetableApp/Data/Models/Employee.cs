namespace OTTimetableApp.Data.Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int? BaseGroupId { get; set; }
    public Group? BaseGroup { get; set; }
    public string FullName { get; set; } = "";      // Nama Penuh
    public string? IcNo { get; set; }               // No. KP
    public string? PikNo { get; set; }              // No. PiK
    public string? Branch { get; set; }             // Cawangan
    public string? PhoneNo { get; set; }            // No. Telefon
    public decimal? Salary { get; set; }            // Gaji
    public string? SalaryNo { get; set; }           // No. Gaji
    public string? BankAccountNo { get; set; }      // Akaun Bank
    public string? BankName { get; set; }           // Nama Bank
}