using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Services;

public class EmployeeService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public EmployeeService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public List<Employee> GetAll()
    {
        using var db = _dbFactory.CreateDbContext();

        return db.Employees
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToList();
    }

    public List<Group> GetGroups()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Groups.AsNoTracking().OrderBy(g => g.Name).ToList();
    }

    public Dictionary<int, int> GetBaseGroupMap()
    {
        using var db = _dbFactory.CreateDbContext();

        return db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.EmployeeId != null)
            .ToDictionary(gm => gm.EmployeeId!.Value, gm => gm.GroupId);
    }

    public void Save(Employee e)
    {
        if (string.IsNullOrWhiteSpace(e.Name))
            throw new InvalidOperationException("Short Name (Name) is required.");
        if (string.IsNullOrWhiteSpace(e.FullName))
            throw new InvalidOperationException("Full Name is required.");

        using var db = _dbFactory.CreateDbContext();

        if (e.Id == 0)
        {
            db.Employees.Add(e);
        }
        else
        {
            var existing = db.Employees.First(x => x.Id == e.Id);

            existing.Name = e.Name.Trim();
            existing.FullName = e.FullName.Trim();

            existing.IcNo = e.IcNo?.Trim();
            existing.PikNo = e.PikNo?.Trim();
            existing.Branch = e.Branch?.Trim();
            existing.PhoneNo = e.PhoneNo?.Trim();

            existing.Salary = e.Salary;
            existing.SalaryNo = e.SalaryNo?.Trim();

            existing.BankAccountNo = e.BankAccountNo?.Trim();
            existing.BankName = e.BankName?.Trim();

            existing.IsActive = e.IsActive;
        }

        db.SaveChanges();
    }

    public void Delete(int employeeId)
    {
        using var db = _dbFactory.CreateDbContext();

        var e = db.Employees.First(x => x.Id == employeeId);
        db.Employees.Remove(e);

        db.SaveChanges();
    }


}