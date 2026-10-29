using System.Text.Json;
using ZKTecoC3Manager.Models;

namespace ZKTecoC3Manager.Services;

/// <summary>Lưu thông số kết nối gần nhất vào settings.json cạnh file exe.</summary>
public sealed class AppSettings
{
    public string Ip { get; set; } = "192.168.1.201";
    public int Port { get; set; } = 4370;
    public int TimeoutMs { get; set; } = 4000;
    public int PollIntervalMs { get; set; } = 500;
    public bool AutoReconnect { get; set; } = true;
    public int OpenSeconds { get; set; } = 5;
    public VerifyApiSettings Verify { get; set; } = new();

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException) { }
        return new AppSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch (IOException) { }
    }
}
