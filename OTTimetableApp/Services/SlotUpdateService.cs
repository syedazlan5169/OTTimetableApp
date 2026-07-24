using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class SlotUpdateService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly AuditLogService _auditSvc;

    public SlotUpdateService(IDbContextFactory<AppDbContext> dbFactory, AuditLogService auditSvc)
    {
        _dbFactory = dbFactory;
        _auditSvc = auditSvc;
    }

    // Determine whether applying newEmployeeId to this slot requires prompting the user for
    // a leave reason. This covers:
    //  - Replacement: a planned member being replaced by someone else (CASE 4)
    //  - Unfilled leave: a planned member being cleared to "None" with no replacement (CASE 1
    //    where a planned member existed) - they are still on leave, just the slot stays empty.
    public bool RequiresLeaveReason(int slotId, int? newEmployeeId)
    {
        using var db = _dbFactory.CreateDbContext();

        var slot = db.ShiftSlots.First(s => s.Id == slotId);
        var planned = slot.PlannedEmployeeId;

        if (planned == null) return false;                // no planned member to be on leave

        if (newEmployeeId == null) return true;            // CASE 1 - cleared, planned member on leave, no replacement
        if (newEmployeeId == planned) return false;        // CASE 3 - same person, working as planned

        return true;                                       // CASE 4 - replacement
    }

    // Returns the currently persisted ActualEmployeeId for this slot, straight from the DB.
    // Used by the UI to revert a ComboBox selection when the user cancels the leave-reason
    // prompt, since the ComboBox's two-way binding already mutated the VM before save.
    public int? GetPersistedActualEmployeeId(int slotId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.ShiftSlots
            .Where(s => s.Id == slotId)
            .Select(s => s.ActualEmployeeId)
            .First();
    }

    public void UpdateSlot(int slotId, int? newEmployeeId, LeaveReason? leaveReason = null)
    {
        using var db = _dbFactory.CreateDbContext();

        var slot = db.ShiftSlots
            .Include(s => s.ShiftAssignment)
                .ThenInclude(a => a.CalendarDay)
                    .ThenInclude(d => d.Calendar)
            .Include(s => s.ActualEmployee)
            .First(s => s.Id == slotId);

        var shiftId = slot.ShiftAssignmentId;

        var allSlotsInShift = db.ShiftSlots
            .Where(s => s.ShiftAssignmentId == shiftId)
            .ToList();

        var planned = slot.PlannedEmployeeId;

        var calendarDay = slot.ShiftAssignment.CalendarDay;
        var calendar = calendarDay.Calendar;
        string dateStr = calendarDay.Date.ToString("dd/MM/yyyy");
        string shiftLabel = slot.ShiftAssignment.ShiftType.ToString();
        string oldEmpName = slot.ActualEmployee?.Name ?? "(None)";

        // CASE 1 — Clear slot (no one assigned)
        if (newEmployeeId == null)
        {
            slot.ActualEmployeeId = null;
            // Keep track of who is on leave even though nobody replaced them,
            // so the reason can be reported later (e.g. in Excel export).
            slot.ReplacedEmployeeId = planned;
            slot.LeaveReason = planned != null ? leaveReason : null;
            slot.FillType = SlotFillType.Empty;
            db.SaveChanges();
            _auditSvc.Log("SlotCleared",
                $"{dateStr} [{shiftLabel}] Slot {slot.SlotIndex}: cleared (was: {oldEmpName})",
                calendarDay.CalendarId, calendar.Name);
            return;
        }

        string newEmpName = db.Employees
            .Where(e => e.Id == newEmployeeId.Value)
            .Select(e => e.Name)
            .FirstOrDefault() ?? "(Unknown)";

        // CASE 2 — Empty warrant slot (planned null) => EmptyFill
        if (planned == null)
        {
            slot.ActualEmployeeId = newEmployeeId;
            slot.ReplacedEmployeeId = null;
            slot.LeaveReason = null;
            slot.FillType = SlotFillType.EmptyFill;
            db.SaveChanges();
            _auditSvc.Log("SlotFilled",
                $"{dateStr} [{shiftLabel}] Slot {slot.SlotIndex}: filled empty warrant with {newEmpName}",
                calendarDay.CalendarId, calendar.Name);
            return;
        }

        // CASE 3 — Planned slot with same person
        if (newEmployeeId == planned)
        {
            slot.ActualEmployeeId = newEmployeeId;
            slot.ReplacedEmployeeId = null;
            slot.LeaveReason = null;
            slot.FillType = SlotFillType.Planned;
            db.SaveChanges();
            _auditSvc.Log("SlotAssigned",
                $"{dateStr} [{shiftLabel}] Slot {slot.SlotIndex}: assigned {newEmpName} (planned)",
                calendarDay.CalendarId, calendar.Name);
            return;
        }

        // CASE 4 — Replacement of planned member
        bool alreadyReplaced = allSlotsInShift.Any(s =>
            s.Id != slot.Id &&
            s.ReplacedEmployeeId == planned);

        if (alreadyReplaced)
            throw new InvalidOperationException("This base member is already replaced in this shift.");

        string replacedEmpName = db.Employees
            .Where(e => e.Id == planned.Value)
            .Select(e => e.Name)
            .FirstOrDefault() ?? "(Unknown)";

        slot.ActualEmployeeId = newEmployeeId;
        slot.ReplacedEmployeeId = planned;
        slot.LeaveReason = leaveReason;
        slot.FillType = SlotFillType.Replacement;
        db.SaveChanges();

        _auditSvc.Log("SlotReplaced",
            $"{dateStr} [{shiftLabel}] Slot {slot.SlotIndex}: {newEmpName} replaced {replacedEmpName}",
            calendarDay.CalendarId, calendar.Name);
    }
}