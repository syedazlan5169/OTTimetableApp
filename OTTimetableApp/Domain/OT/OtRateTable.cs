namespace OTTimetableApp.Domain.OT;

public static class OtRateTable
{
    public static decimal GetRate(OtCategory cat, RateBand band)
    {
        return cat switch
        {
            OtCategory.KelepasanGiliran => band == RateBand.Day ? 1.25m : 1.5m,
            OtCategory.KelepasanAm => band == RateBand.Day ? 1.75m : 2.0m,
            OtCategory.KelepasanAmGantian => band == RateBand.Day ? 1.75m : 2.0m,
            OtCategory.WorkingDay => band == RateBand.Day ? 1.125m : 1.25m,
            _ => 0m
        };
    }
}