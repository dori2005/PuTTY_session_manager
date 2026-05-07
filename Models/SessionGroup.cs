using System.Text.Json.Serialization;

namespace PuttySessionManager.Models;

public class SessionGroup
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>포함된 세션의 RegistryName 목록.</summary>
    [JsonPropertyName("sessions")]
    public List<string> SessionNames { get; set; } = new();

    [JsonPropertyName("children")]
    public List<SessionGroup> Children { get; set; } = new();

    [JsonPropertyName("isExpanded")]
    public bool IsExpanded { get; set; } = true;
}

/// <summary>groups.json 파일의 루트 객체.</summary>
public class AppData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("groups")]
    public List<SessionGroup> Groups { get; set; } = new();

    [JsonPropertyName("ungroupedExpanded")]
    public bool UngroupedExpanded { get; set; } = true;
}
