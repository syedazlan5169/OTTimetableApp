using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OTTimetableApp.Data;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using System.Windows;

namespace OTTimetableApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);


        this.DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "Unhandled UI Exception");
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            MessageBox.Show(ex.ExceptionObject?.ToString() ?? "Unknown", "Unhandled AppDomain Exception");
        };




        var cfg = AppConfig.Load();
        var cs = $"Server={cfg.Host};Database={cfg.Database};User={cfg.User};Password={cfg.Password};";

        var services = new ServiceCollection();

        // IMPORTANT: specify server version explicitly to avoid AutoDetect recursion/overhead
        // Change this if your MySQL version differs.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseMySql(cs, serverVersion);
        });

        services.AddSingleton<MonthViewService>();
        services.AddSingleton<MonthViewerVM>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<CalendarGeneratorService2>();
        services.AddTransient<CalendarManagerWindow>();
        services.AddSingleton<SlotUpdateService>();
        services.AddSingleton<PublicHolidayService>();
        services.AddSingleton<OtCalculatorService>();
        services.AddSingleton<EmployeeService>();
        services.AddTransient<EmployeeManagerVM>();
        services.AddTransient<EmployeeManagerWindow>();
        services.AddSingleton<GroupManagerService>();
        services.AddTransient<GroupManagerVM>();
        services.AddTransient<GroupManagerWindow>();

        Services = services.BuildServiceProvider();

        // Seed once
        using (var scope = Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = dbFactory.CreateDbContext();
            db.Database.EnsureCreated();
            Seeder.SeedIfEmpty(db);
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}