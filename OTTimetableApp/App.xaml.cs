using System.Windows;
using OTTimetableApp.Data;

namespace OTTimetableApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create DbContext
        using var db = new AppDbContext(AppDbContext.BuildOptions());

        // Ensure database exists
        db.Database.EnsureCreated();

        // Seed default data if empty
        Seeder.SeedIfEmpty(db);
    }
}