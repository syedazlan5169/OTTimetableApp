namespace OTTimetableApp.Data.Models;

public enum ShiftType
{
    Night = 1,   // 22:00-07:00
    Morning = 2, // 07:00-15:00
    Evening = 3  // 14:00-23:00
}

public enum SlotFillType
{
    Planned = 1,      // default from base group member
    Replacement = 2,  // replaced someone on leave
    EmptyFill = 3,    // filled an originally empty warrant
    Empty = 4         // no one assigned
}