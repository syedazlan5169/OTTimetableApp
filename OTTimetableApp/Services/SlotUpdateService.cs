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

    public void UpdateSlot(int slotId, int? newEmployeeId)
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
            slot.ReplacedEmployeeId = null;
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
        slot.FillType = SlotFillType.Replacement;
        db.SaveChanges();

        _auditSvc.Log("SlotReplaced",
            $"{dateStr} [{shiftLabel}] Slot {slot.SlotIndex}: {newEmpName} replaced {replacedEmpName}",
            calendarDay.CalendarId, calendar.Name);
    }
}