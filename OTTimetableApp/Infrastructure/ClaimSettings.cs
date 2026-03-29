using System.IO;
using System.Text.Json;

namespace OTTimetableApp.Infrastructure;

public class ClaimSettings
{
    public string CatatanLampiranE { get; set; } = "MENYEMAK IMEJ/ MENAHAN KONTENA";
    public string CatatanLampiranA { get; set; } = "Menyemak sistem SMK dan GCS, Menganalisa imej dagangan kontena dan menahan kontena";

    private static string GetFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OTTimetableApp");

        return Path.Combine(dir, "claimsettings.json");
    }

    public static ClaimSettings Load()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
            return new ClaimSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClaimSettings>(json) ?? new ClaimSettings();
        }
        catch
        {
            return new ClaimSettings();
        }
    }

    public void Save()
    {
        var path = GetFilePath();
        var dir = Path.GetDirectoryName(path)!;

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
