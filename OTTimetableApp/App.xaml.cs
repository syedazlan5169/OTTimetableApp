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
    private static ServiceProvider? _serviceProvider;
    public static IServiceProvider Services => _serviceProvider!;

    private UnhandledExceptionEventHandler? _domainExceptionHandler;
    private System.Windows.Threading.DispatcherUnhandledExceptionEventHandler? _dispatcherExceptionHandler;


    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Store event handlers so we can unsubscribe later
        _dispatcherExceptionHandler = (s, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "Unhandled UI Exception");
            ex.Handled = true;
        };
        this.DispatcherUnhandledException += _dispatcherExceptionHandler;

        _domainExceptionHandler = (s, ex) =>
        {
            MessageBox.Show(ex.ExceptionObject?.ToString() ?? "Unknown", "Unhandled AppDomain Exception");
        };
        AppDomain.CurrentDomain.UnhandledException += _domainExceptionHandler;

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
                    options.UseMySql(cs, serverVersion, mysqlOptions =>
                    {
                        mysqlOptions.EnableRetryOnFailure();
                    });
                    options.EnableSensitiveDataLogging(false);
                    options.EnableDetailedErrors(false);
                }, ServiceLifetime.Singleton);

                services.AddSingleton<MonthViewService>();
                services.AddSingleton<AuditLogService>();
                services.AddSingleton<AdminAuthService>();
                services.AddTransient<MonthViewerVM>();
                services.AddTransient<MainWindow>();
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
                services.AddTransient<AuditLogWindow>();
                services.AddTransient<AdminWindow>();
                services.AddTransient<SystemInfoWindow>();
                services.AddTransient<ReplacementReportWindow>();
                services.AddTransient<PhBulkImportWindow>();
                services.AddTransient<AdminLoginWindow>();
                services.AddTransient<ChangePasswordWindow>();
                services.AddTransient<StatsWindow>();

                _serviceProvider = services.BuildServiceProvider();

                var mainWindow = Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
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

    protected override void OnExit(ExitEventArgs e)
    {
        // Unsubscribe event handlers to release references
        if (_dispatcherExceptionHandler != null)
            this.DispatcherUnhandledException -= _dispatcherExceptionHandler;

        if (_domainExceptionHandler != null)
            AppDomain.CurrentDomain.UnhandledException -= _domainExceptionHandler;

        // Dispose ServiceProvider to clean up all services and DbContext connections
        _serviceProvider?.Dispose();
        _serviceProvider = null;

        base.OnExit(e);
    }
}