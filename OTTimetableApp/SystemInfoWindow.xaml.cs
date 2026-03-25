using Microsoft.EntityFrameworkCore;
using OTTimetableApp.Data;
using OTTimetableApp.Infrastructure;
using System.Reflection;
using System.Windows;

namespace OTTimetableApp;

public partial class SystemInfoWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SystemInfoWindow(IDbContextFactory<AppDbContext> dbFactory)
    {
        InitializeComponent();
        _dbFactory = dbFactory;
        LoadInfo();
    }

    private void LoadInfo()
    {
        // Application
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "1.0.0";
        RuntimeText.Text = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        // Database
        var cfg = AppConfig.Load();
        HostText.Text = cfg.Host;
        DbNameText.Text = cfg.Database;

        try
        {
            using var db = _dbFactory.CreateDbContext();

            var migrations = db.Database.GetAppliedMigrations().ToList();
            MigrationText.Text = migrations.Count > 0
                ? migrations[^1]
                : "(none applied)";

            var totalEmp = db.Employees.AsNoTracking().Count();
            var activeEmp = db.Employees.AsNoTracking().Count(e => e.IsActive);
            EmployeeText.Text = $"{activeEmp} active / {totalEmp} total";

            GroupText.Text = db.Groups.AsNoTracking().Count().ToString();
            CalendarText.Text = db.Calendars.AsNoTracking().Count().ToString();
            AuditLogText.Text = db.AuditLogs.AsNoTracking().Count().ToString();
        }
        catch (Exception ex)
        {
            MigrationText.Text = $"Error: {ex.Message}";
            EmployeeText.Text = GroupText.Text = CalendarText.Text = AuditLogText.Text = "—";
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadInfo();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
