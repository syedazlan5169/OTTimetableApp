using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace OTTimetableApp;

public partial class AdminWindow : Window
{
    private readonly IServiceProvider _sp;

    public AdminWindow(IServiceProvider sp)
    {
        InitializeComponent();
        _sp = sp;
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
}
