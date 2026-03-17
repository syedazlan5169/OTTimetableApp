using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using OTTimetableApp.Data;
using OTTimetableApp.Infrastructure;
using OTTimetableApp.Services;
using OTTimetableApp.ViewModels;
using System.Windows;
using System.Globalization;

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

        var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

        bool EnsureDbReady(AppConfig cfg)
        {
            var cs = cfg.BuildConnectionString();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(cs, serverVersion)
                .Options;

            using var db = new AppDbContext(options);
            db.Database.Migrate();
            Seeder.SeedIfEmpty(db);
            return true;
        }

        while (true)
        {
            var cfg = AppConfig.Load();

            if (!cfg.IsValid())
            {
                var setup = new DatabaseSetupWindow();
                setup.ShowDialog();
                if (!setup.Saved)
                {
                    Shutdown();
                    return;
                }
                continue;
            }

            try
            {
                EnsureDbReady(cfg);

                var cs = cfg.BuildConnectionString();
                var services = new ServiceCollection();

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
                services.AddSingleton<ExcelExportService>();
                services.AddSingleton<PdfExportService>();
                services.AddTransient<EmployeeManagerVM>();
                services.AddTransient<EmployeeManagerWindow>();
                services.AddSingleton<GroupManagerService>();
                services.AddTransient<GroupManagerVM>();
                services.AddTransient<GroupManagerWindow>();
                services.AddTransient<ClaimPreviewVM>();
                services.AddTransient<ClaimPreviewWindow>();
                services.AddTransient<DatabaseSetupWindow>();
                services.AddTransient<DateRangePickerDialog>();

                Services = services.BuildServiceProvider();

                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                break;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database error", MessageBoxButton.OK, MessageBoxImage.Error);

                var setup = new DatabaseSetupWindow();
                setup.ShowDialog();
                if (!setup.Saved)
                {
                    Shutdown();
                    return;
                }
            }
        }
    }
}