using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class SlotUpdateService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SlotUpdateService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void UpdateSlot(int slotId, int? newEmployeeId)
    {
        using var db = _dbFactory.CreateDbContext();

        var slot = db.ShiftSlots
            .Include(s => s.ShiftAssignment)
            .First(s => s.Id == slotId);

        var shiftId = slot.ShiftAssignmentId;

        var allSlotsInShift = db.ShiftSlots
            .Where(s => s.ShiftAssignmentId == shiftId)
            .ToList();

        var planned = slot.PlannedEmployeeId;

        // CASE 1 — Clear slot (no one assigned)
        if (newEmployeeId == null)
        {
            slot.ActualEmployeeId = null;
            slot.ReplacedEmployeeId = null;
            slot.FillType = SlotFillType.Empty;
            db.SaveChanges();
            return;
        }

        // CASE 2 — Empty warrant slot (planned null) => EmptyFill
        if (planned == null)
        {
            slot.ActualEmployeeId = newEmployeeId;
            slot.ReplacedEmployeeId = null;
            slot.FillType = SlotFillType.EmptyFill;
            db.SaveChanges();
            return;
        }

        // CASE 3 — Planned slot with same person
        if (newEmployeeId == planned)
        {
            slot.ActualEmployeeId = newEmployeeId;
            slot.ReplacedEmployeeId = null;
            slot.FillType = SlotFillType.Planned;
            db.SaveChanges();
            return;
        }

        // CASE 4 — Replacement of planned member
        bool alreadyReplaced = allSlotsInShift.Any(s =>
            s.Id != slot.Id &&
            s.ReplacedEmployeeId == planned);

        if (alreadyReplaced)
            throw new InvalidOperationException("This base member is already replaced in this shift.");

        slot.ActualEmployeeId = newEmployeeId;
        slot.ReplacedEmployeeId = planned;
        slot.FillType = SlotFillType.Replacement;

        db.SaveChanges();
    }
}