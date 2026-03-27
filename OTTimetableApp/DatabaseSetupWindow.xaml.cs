using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OTTimetableApp.Data;
using OTTimetableApp.Infrastructure;
using System.Windows;

namespace OTTimetableApp;

public partial class DatabaseSetupWindow : Window
{
    private readonly MySqlServerVersion _serverVersion = new(new Version(8, 0, 0));

    public bool Saved { get; private set; }

    public DatabaseSetupWindow()
    {
        InitializeComponent();

        var cfg = AppConfig.Load();
        HostBox.Text = cfg.Host;
        DatabaseBox.Text = cfg.Database;
        UserBox.Text = cfg.User;
        PasswordBox.Password = cfg.Password;

        SshCheckBox.IsChecked = cfg.SshEnabled;
        SshHostBox.Text = cfg.SshHost;
        SshPortBox.Text = cfg.SshPort.ToString();
        SshUserBox.Text = cfg.SshUser;
        SshPasswordBox.Password = cfg.SshPassword;
        SshKeyPathBox.Text = cfg.SshPrivateKeyPath;
        SshRemoteHostBox.Text = cfg.SshRemoteHost;
        SshRemotePortBox.Text = cfg.SshRemotePort.ToString();

        SshGroupBox.Visibility = cfg.SshEnabled ? Visibility.Visible : Visibility.Collapsed;

        ConfigPathText.Text = $"Config file: {AppConfig.GetUserConfigPath()}";
    }

    private AppConfig ReadConfigFromUi()
    {
        int.TryParse(SshPortBox.Text, out var sshPort);
        int.TryParse(SshRemotePortBox.Text, out var sshRemotePort);

        return new()
        {
            Host = HostBox.Text.Trim(),
            Database = DatabaseBox.Text.Trim(),
            User = UserBox.Text.Trim(),
            Password = PasswordBox.Password,
            SshEnabled = SshCheckBox.IsChecked == true,
            SshHost = SshHostBox.Text.Trim(),
            SshPort = sshPort > 0 ? sshPort : 22,
            SshUser = SshUserBox.Text.Trim(),
            SshPassword = SshPasswordBox.Password,
            SshPrivateKeyPath = SshKeyPathBox.Text.Trim(),
            SshRemoteHost = string.IsNullOrWhiteSpace(SshRemoteHostBox.Text) ? "127.0.0.1" : SshRemoteHostBox.Text.Trim(),
            SshRemotePort = sshRemotePort > 0 ? sshRemotePort : 3306
        };
    }

    private DbContextOptions<AppDbContext> BuildOptions(AppConfig cfg)
    {
        var cs = cfg.BuildConnectionString();
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(cs, _serverVersion)
            .Options;
    }

    private void Log(string message)
    {
        StatusBox.AppendText(message + Environment.NewLine);
        StatusBox.ScrollToEnd();
    }

    private void SshCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        SshGroupBox.Visibility = SshCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select SSH Private Key File",
            Filter = "All Files (*.*)|*.*|PEM Files (*.pem)|*.pem|PPK Files (*.ppk)|*.ppk"
        };
        if (dlg.ShowDialog() == true)
            SshKeyPathBox.Text = dlg.FileName;
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        using var tunnel = new SshTunnelService();
        try
        {
            StatusBox.Clear();
            var cfg = ReadConfigFromUi();
            if (!cfg.IsValid())
            {
                Log("Please fill in all required fields.");
                return;
            }

            if (cfg.SshEnabled)
            {
                Log("Starting SSH tunnel...");
                tunnel.Start(cfg);
                Log($"SSH tunnel active (local port {cfg.SshLocalPort}).");
            }

            var options = BuildOptions(cfg);
            using var db = new AppDbContext(options);

            Log("Testing connection...");
            var ok = db.Database.CanConnect();
            Log(ok ? "Connection OK." : "Connection failed.");
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
        }
    }

    private void Migrate_Click(object sender, RoutedEventArgs e)
    {
        using var tunnel = new SshTunnelService();
        try
        {
            var cfg = ReadConfigFromUi();
            if (!cfg.IsValid())
            {
                Log("Please fill in all required fields.");
                return;
            }

            if (cfg.SshEnabled)
            {
                Log("Starting SSH tunnel...");
                tunnel.Start(cfg);
                Log($"SSH tunnel active (local port {cfg.SshLocalPort}).");
            }

            var options = BuildOptions(cfg);
            using var db = new AppDbContext(options);

            Log("Running migrations...");
            db.Database.Migrate();
            Log("Migrations completed.");
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cfg = ReadConfigFromUi();
            if (!cfg.IsValid())
            {
                Log("Please fill in all required fields.");
                return;
            }

            AppConfig.Save(cfg);
            Log("Saved.");
            Saved = true;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
