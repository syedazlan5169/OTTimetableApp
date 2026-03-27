using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.IO;

namespace OTTimetableApp.Infrastructure;

public class AppConfig
{
    public string Host { get; set; } = "localhost";
    public string Database { get; set; } = "ot_timetable";
    public string User { get; set; } = "admin";
    public string Password { get; set; } = "";

    // SSH tunnel settings
    public bool SshEnabled { get; set; } = false;
    public string SshHost { get; set; } = "";
    public int SshPort { get; set; } = 22;
    public string SshUser { get; set; } = "";
    public string SshPassword { get; set; } = "";
    public string SshPrivateKeyPath { get; set; } = "";
    public string SshRemoteHost { get; set; } = "127.0.0.1";
    public int SshRemotePort { get; set; } = 3306;
    public int SshLocalPort { get; set; } = 13306;

    public string BuildConnectionString()
    {
        if (SshEnabled)
            return $"Server=127.0.0.1;Port={SshLocalPort};Database={Database};User={User};Password={Password};";
        return $"Server={Host};Database={Database};User={User};Password={Password};";
    }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Host)
            || string.IsNullOrWhiteSpace(Database)
            || string.IsNullOrWhiteSpace(User)
            || string.IsNullOrWhiteSpace(Password))
            return false;

        if (SshEnabled)
        {
            if (string.IsNullOrWhiteSpace(SshHost) || string.IsNullOrWhiteSpace(SshUser))
                return false;
            if (string.IsNullOrWhiteSpace(SshPassword) && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
                return false;
        }

        return true;
    }

    public static string GetUserConfigPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OTTimetableApp");

        return Path.Combine(dir, "dbconfig.json");
    }

    public static AppConfig Load()
    {
        var userCfgPath = GetUserConfigPath();

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(userCfgPath, optional: true, reloadOnChange: true)
            .AddUserSecrets<AppConfig>(optional: true);

        var config = builder.Build();

        return new AppConfig
        {
            Host = config["Db:Host"] ?? "localhost",
            Database = config["Db:Database"] ?? "ot_timetable",
            User = config["Db:User"] ?? "admin",
            Password = config["Db:Password"] ?? "",
            SshEnabled = bool.TryParse(config["Db:Ssh:Enabled"], out var sshEnabled) && sshEnabled,
            SshHost = config["Db:Ssh:Host"] ?? "",
            SshPort = int.TryParse(config["Db:Ssh:Port"], out var sshPort) ? sshPort : 22,
            SshUser = config["Db:Ssh:User"] ?? "",
            SshPassword = config["Db:Ssh:Password"] ?? "",
            SshPrivateKeyPath = config["Db:Ssh:PrivateKeyPath"] ?? "",
            SshRemoteHost = config["Db:Ssh:RemoteHost"] ?? "127.0.0.1",
            SshRemotePort = int.TryParse(config["Db:Ssh:RemotePort"], out var sshRemotePort) ? sshRemotePort : 3306,
            SshLocalPort = int.TryParse(config["Db:Ssh:LocalPort"], out var sshLocalPort) ? sshLocalPort : 13306
        };
    }

    public static void Save(AppConfig cfg)
    {
        var path = GetUserConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var payload = new
        {
            Db = new
            {
                cfg.Host,
                cfg.Database,
                cfg.User,
                cfg.Password,
                Ssh = new
                {
                    Enabled = cfg.SshEnabled,
                    Host = cfg.SshHost,
                    Port = cfg.SshPort,
                    User = cfg.SshUser,
                    Password = cfg.SshPassword,
                    PrivateKeyPath = cfg.SshPrivateKeyPath,
                    RemoteHost = cfg.SshRemoteHost,
                    RemotePort = cfg.SshRemotePort,
                    LocalPort = cfg.SshLocalPort
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}