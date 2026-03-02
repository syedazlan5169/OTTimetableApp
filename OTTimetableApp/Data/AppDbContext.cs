using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftSlot> ShiftSlots => Set<ShiftSlot>();
    public DbSet<Calendar> Calendars => Set<Calendar>();
    public DbSet<CalendarDay> CalendarDays => Set<CalendarDay>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Employee> Employees => Set<Employee>();
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Group>()
             .HasIndex(g => g.Name)
             .IsUnique();

        modelBuilder.Entity<GroupMember>()
            .HasIndex(m => new { m.GroupId, m.SlotIndex })
            .IsUnique();

        modelBuilder.Entity<GroupMember>()
            .HasOne(m => m.Employee)
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Calendar>()
            .HasIndex(c => new { c.Year, c.Name })
            .IsUnique();

        modelBuilder.Entity<CalendarDay>()
            .HasIndex(d => new { d.CalendarId, d.Date })
            .IsUnique();

        modelBuilder.Entity<CalendarDay>()
            .HasOne(d => d.Calendar)
            .WithMany()
            .HasForeignKey(d => d.CalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        // One CalendarDay has 3 ShiftAssignment rows (Night/Morning/Evening)
        modelBuilder.Entity<ShiftAssignment>()
            .HasIndex(x => new { x.CalendarDayId, x.ShiftType })
            .IsUnique();

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(x => x.CalendarDay)
            .WithMany()
            .HasForeignKey(x => x.CalendarDayId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShiftSlot>()
            .HasIndex(x => new { x.ShiftAssignmentId, x.SlotIndex })
            .IsUnique();

        modelBuilder.Entity<ShiftSlot>()
            .HasOne(x => x.ShiftAssignment)
            .WithMany()
            .HasForeignKey(x => x.ShiftAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Employee FKs (set null if employee removed later)
        modelBuilder.Entity<ShiftSlot>()
            .HasOne(x => x.PlannedEmployee)
            .WithMany()
            .HasForeignKey(x => x.PlannedEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ShiftSlot>()
            .HasOne(x => x.ActualEmployee)
            .WithMany()
            .HasForeignKey(x => x.ActualEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ShiftSlot>()
            .HasOne(x => x.ReplacedEmployee)
            .WithMany()
            .HasForeignKey(x => x.ReplacedEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    public static DbContextOptions<AppDbContext> BuildOptions()
    {
        var cfg = AppConfig.Load();

        var cs = $"Server={cfg.Host};Database={cfg.Database};User={cfg.User};Password={cfg.Password};";

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(cs, ServerVersion.AutoDetect(cs))
            .Options;
    }
}