using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data.Models;
using OTTimetableApp.Services;

namespace OTTimetableApp.Data;

public static class Seeder
{
    public static void SeedIfEmpty(AppDbContext db)
    {
        // Always ensure admin user exists, independent of other seed data
        SeedAdminUser(db);

        // If groups already exist, assume everything else is seeded
        if (db.Groups.Any()) return;

        // 1) Employees
        var employeeNames = new[]
        {
            "Employee1","Employee2","Employee3","Employee4",
            "Employee5","Employee6","Employee7","Employee8",
            "Employee9","Employee10","Employee11","Employee12",
            "Employee13","Employee14","Employee15","Employee16"
        };

        var employees = employeeNames
            .Select(n => new Employee { Name = n, IsActive = true })
            .ToList();

        db.Employees.AddRange(employees);
        db.SaveChanges();

        int? EmpId(string name) => db.Employees.First(e => e.Name == name).Id;

        // 2) Groups
        var gA = new Group { Name = "KUMPULAN A", SlotCount = 5 };
        var gB = new Group { Name = "KUMPULAN B", SlotCount = 5 };
        var gC = new Group { Name = "KUMPULAN C", SlotCount = 5 };
        var gD = new Group { Name = "KUMPULAN D", SlotCount = 5 };

        db.Groups.AddRange(gA, gB, gC, gD);
        db.SaveChanges();

        // 3) Group Members (SlotIndex 1..5)
        // KUMPULAN A: Shawal, Sakinah, Syed, Faiz, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gA.Id, SlotIndex = 1, EmployeeId = EmpId("Employee1") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 2, EmployeeId = EmpId("Employee2") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 3, EmployeeId = EmpId("Employee3") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 4, EmployeeId = EmpId("Employee4") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 5, EmployeeId = null }
        );

        // KUMPULAN B: Zahalan, Basrul, Azlan, Zikry, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gB.Id, SlotIndex = 1, EmployeeId = EmpId("Employee5") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 2, EmployeeId = EmpId("Employee6") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 3, EmployeeId = EmpId("Employee7") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 4, EmployeeId = EmpId("Employee8") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 5, EmployeeId = null }
        );

        // KUMPULAN C: Baiti, Hamizi, Asrul, EMPTY, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gC.Id, SlotIndex = 1, EmployeeId = EmpId("Employee9") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 2, EmployeeId = EmpId("Employee10") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 3, EmployeeId = EmpId("Employee11") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 4, EmployeeId = EmpId("Employee12") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 5, EmployeeId = null }
        );

        // KUMPULAN D: Zaini, Faizuddin, Mirza, Hafiz, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gD.Id, SlotIndex = 1, EmployeeId = EmpId("Employee13") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 2, EmployeeId = EmpId("Employee14") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 3, EmployeeId = EmpId("Employee15") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 4, EmployeeId = EmpId("Employee16") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 5, EmployeeId = null }
        );

        db.SaveChanges();
    }

    private static void SeedAdminUser(AppDbContext db)
    {
        if (db.AdminUsers.Any()) return;

        db.AdminUsers.Add(new AdminUser
        {
            Username = "admin",
            PasswordHash = AdminAuthService.HashPassword("Lanpke050890!")
        });
        db.SaveChanges();
    }
}