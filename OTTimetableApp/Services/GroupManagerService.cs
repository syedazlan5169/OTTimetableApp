using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class GroupManagerService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public GroupManagerService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public List<Group> GetGroups()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Groups.AsNoTracking().OrderBy(g => g.Name).ToList();
    }

    public List<Employee> GetEmployees()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ToList();
    }

    public (Group group, List<GroupMember> members) LoadGroup(int groupId)
    {
        using var db = _dbFactory.CreateDbContext();

        var g = db.Groups.First(x => x.Id == groupId);

        var members = db.GroupMembers
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.SlotIndex)
            .ToList();

        return (g, members);
    }

    public void SaveCapacity(int groupId, int capacity)
    {
        if (capacity < 1) throw new InvalidOperationException("Capacity must be >= 1.");

        using var db = _dbFactory.CreateDbContext();
        var g = db.Groups.First(x => x.Id == groupId);
        g.SlotCapacity = capacity;

        // If capacity is reduced, delete members beyond capacity
        var extras = db.GroupMembers
            .Where(x => x.GroupId == groupId && x.SlotIndex > capacity)
            .ToList();

        if (extras.Count > 0)
            db.GroupMembers.RemoveRange(extras);

        db.SaveChanges();
    }

    public void SetSlot(int groupId, int slotIndex, int? employeeId)
    {
        using var db = _dbFactory.CreateDbContext();

        // remove if set to blank
        var existing = db.GroupMembers.FirstOrDefault(x => x.GroupId == groupId && x.SlotIndex == slotIndex);

        if (employeeId == null)
        {
            if (existing != null)
            {
                db.GroupMembers.Remove(existing);
                db.SaveChanges();
            }
            return;
        }

        // Ensure this employee isn't already in another group (DB unique will enforce too)
        var already = db.GroupMembers.FirstOrDefault(x => x.EmployeeId == employeeId.Value);
        if (already != null && already.GroupId != groupId)
            throw new InvalidOperationException("Employee is already assigned to another group.");

        if (existing == null)
        {
            db.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                SlotIndex = slotIndex,
                EmployeeId = employeeId.Value
            });
        }
        else
        {
            existing.EmployeeId = employeeId.Value;
        }

        db.SaveChanges();
    }
}