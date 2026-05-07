using System.Text.Json;
using PuttySessionManager.Models;

namespace PuttySessionManager.Services;

public class GroupStorageService
{
    private static readonly string DataFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PuttySessionManager",
        "groups.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppData Load()
    {
        try
        {
            if (!File.Exists(DataFilePath)) return new AppData();
            var json = File.ReadAllText(DataFilePath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
        }
        catch
        {
            return new AppData();
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath)!);
        File.WriteAllText(DataFilePath, JsonSerializer.Serialize(data, JsonOptions));
    }
}
