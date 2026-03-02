using Microsoft.Extensions.Configuration;

namespace OTTimetableApp.Infrastructure;

public class AppConfig
{
    public string Host { get; init; } = "localhost";
    public string Database { get; init; } = "ot_timetable";
    public string User { get; init; } = "admin";
    public string Password { get; init; } = "";

    public static AppConfig Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<AppConfig>(); // pulls Db:Password from secrets.json

        var config = builder.Build();

        var result = new AppConfig
        {
            Host = config["Db:Host"] ?? "localhost",
            Database = config["Db:Database"] ?? "ot_timetable",
            User = config["Db:User"] ?? "admin",
            Password = config["Db:Password"] ?? ""
        };

        if (string.IsNullOrWhiteSpace(result.Password))
            throw new Exception("Db:Password is empty. Set it in User Secrets.");

        return result;
    }
}