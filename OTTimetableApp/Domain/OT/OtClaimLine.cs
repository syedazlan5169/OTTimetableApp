namespace OTTimetableApp.Domain.OT;

public enum OtCategory
{
    WorkingDay,
    KelepasanGiliran,
    KelepasanAm,
    KelepasanAmGantian
}

public enum RateBand
{
    Day,
    Night
}

public class OtClaimLine
{
    public DateOnly ClaimDate { get; set; }          // the date shown on claim
    public TimeOnly From { get; set; }
    public TimeOnly To { get; set; }

    public OtCategory Category { get; set; }
    public RateBand Band { get; set; }

    public decimal Hours { get; set; }              // already split day/night
    public decimal Rate { get; set; }               // 1.125, 1.25, etc

    public string Notes { get; set; } = "";         // optional debug/audit
}