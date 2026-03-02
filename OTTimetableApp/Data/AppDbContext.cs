using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Data.Models;

namespace OTTimetableApp.Data;

public class AppDbContext : DbContext
{
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