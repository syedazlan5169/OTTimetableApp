namespace OTTimetableApp.Domain.OT;

public class MonthlyClaimResult
{
    public List<OtClaimLine> ClaimLines { get; set; } = new();
    public decimal ExcessWorkingHours { get; set; }
    public decimal ExcessWorkingHoursAmount { get; set; }
}
