using Microsoft.EntityFrameworkCore;
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

        ConfigPathText.Text = $"Config file: {AppConfig.GetUserConfigPath()}";
    }

    private AppConfig ReadConfigFromUi()
        => new()
        {
            Host = HostBox.Text.Trim(),
            Database = DatabaseBox.Text.Trim(),
            User = UserBox.Text.Trim(),
            Password = PasswordBox.Password
        };

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

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusBox.Clear();

            var cfg = ReadConfigFromUi();
            if (!cfg.IsValid())
            {
                Log("Please fill Host / Database / User / Password.");
                return;
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
        try
        {
            var cfg = ReadConfigFromUi();
            if (!cfg.IsValid())
            {
                Log("Please fill Host / Database / User / Password.");
                return;
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
                Log("Please fill Host / Database / User / Password.");
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
