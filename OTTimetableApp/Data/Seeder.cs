using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Data;

public static class Seeder
{
    public static void SeedIfEmpty(AppDbContext db)
    {
        // If groups already exist, assume seeded
        if (db.Groups.Any()) return;

        // 1) Employees
        var employeeNames = new[]
        {
            "Shawal","Sakinah","Syed","Faiz",
            "Zahalan","Basrul","Azlan","Zikry",
            "Baiti","Hamizi","Asrul",
            "Zaini","Faizuddin","Mirza","Hafiz"
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
            new GroupMember { GroupId = gA.Id, SlotIndex = 1, EmployeeId = EmpId("Shawal") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 2, EmployeeId = EmpId("Sakinah") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 3, EmployeeId = EmpId("Syed") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 4, EmployeeId = EmpId("Faiz") },
            new GroupMember { GroupId = gA.Id, SlotIndex = 5, EmployeeId = null }
        );

        // KUMPULAN B: Zahalan, Basrul, Azlan, Zikry, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gB.Id, SlotIndex = 1, EmployeeId = EmpId("Zahalan") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 2, EmployeeId = EmpId("Basrul") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 3, EmployeeId = EmpId("Azlan") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 4, EmployeeId = EmpId("Zikry") },
            new GroupMember { GroupId = gB.Id, SlotIndex = 5, EmployeeId = null }
        );

        // KUMPULAN C: Baiti, Hamizi, Asrul, EMPTY, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gC.Id, SlotIndex = 1, EmployeeId = EmpId("Baiti") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 2, EmployeeId = EmpId("Hamizi") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 3, EmployeeId = EmpId("Asrul") },
            new GroupMember { GroupId = gC.Id, SlotIndex = 4, EmployeeId = null },
            new GroupMember { GroupId = gC.Id, SlotIndex = 5, EmployeeId = null }
        );

        // KUMPULAN D: Zaini, Faizuddin, Mirza, Hafiz, EMPTY
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = gD.Id, SlotIndex = 1, EmployeeId = EmpId("Zaini") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 2, EmployeeId = EmpId("Faizuddin") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 3, EmployeeId = EmpId("Mirza") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 4, EmployeeId = EmpId("Hafiz") },
            new GroupMember { GroupId = gD.Id, SlotIndex = 5, EmployeeId = null }
        );

        db.SaveChanges();
    }
}