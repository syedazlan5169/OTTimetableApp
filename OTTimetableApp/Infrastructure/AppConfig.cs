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

    public string BuildConnectionString()
        => $"Server={Host};Database={Database};User={User};Password={Password};";

    public bool IsValid()
        => !string.IsNullOrWhiteSpace(Host)
           && !string.IsNullOrWhiteSpace(Database)
           && !string.IsNullOrWhiteSpace(User)
           && !string.IsNullOrWhiteSpace(Password);

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
            Password = config["Db:Password"] ?? ""
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
                cfg.Password
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}