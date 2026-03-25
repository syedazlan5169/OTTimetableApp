using Microsoft.Extensions.DependencyInjection;
using OTTimetableApp.Services;
using System.Windows;

namespace OTTimetableApp;

public partial class AdminWindow : Window
{
    private readonly IServiceProvider _sp;
    private readonly AuditLogService _auditSvc;

    public AdminWindow(IServiceProvider sp, AuditLogService auditSvc)
    {
        InitializeComponent();
        _sp = sp;
        _auditSvc = auditSvc;
    }

    private void DatabaseSetup_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<DatabaseSetupWindow>();
        win.Owner = this;
        win.ShowDialog();

        if (win.Saved)
            MessageBox.Show("Database settings saved. Please restart the app to use the new connection.");
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<AuditLogWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void SystemInfo_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<SystemInfoWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void ReplacementReport_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<ReplacementReportWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void PhBulkImport_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<PhBulkImportWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var win = _sp.GetRequiredService<ChangePasswordWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void PurgeLogs_Click(object sender, RoutedEventArgs e)
    {
        var cutoff = DateTime.Now.AddDays(-30).ToString("dd/MM/yyyy");
        var confirm = MessageBox.Show(
            $"This will permanently delete all log entries before {cutoff}.\n\nContinue?",
            "Purge Old Logs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            int deleted = _auditSvc.PurgeOlderThan(30);
            MessageBox.Show($"{deleted} log record(s) deleted.", "Purge Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Purge failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
