using Microsoft.Win32;
using PuttySessionManager.Models;

namespace PuttySessionManager.Services;

public static class RegistryService
{
    private const string PuttySessionsPath = @"Software\SimonTatham\PuTTY\Sessions";

    /// <summary>
    /// HKCU PuTTY Sessions 하위의 모든 세션을 읽어 반환한다.
    /// PuTTY는 세션 이름을 URL 인코딩(%20 등)하여 레지스트리 키로 저장한다.
    /// </summary>
    public static List<PuttySession> GetAllSessions()
    {
        var sessions = new List<PuttySession>();

        using var key = Registry.CurrentUser.OpenSubKey(PuttySessionsPath);
        if (key is null) return sessions;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            if (subKeyName == "Default%20Settings") continue;

            sessions.Add(new PuttySession
            {
                RegistryName = subKeyName,
                DisplayName  = Uri.UnescapeDataString(subKeyName)
            });
        }

        return sessions.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>PuTTY 레지스트리에서 세션의 HostName, UserName을 읽는다.</summary>
    public static (string HostName, string UserName) GetSessionDetails(string registryName)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{PuttySessionsPath}\{registryName}");
        var host = key?.GetValue("HostName")?.ToString() ?? "";
        var user = key?.GetValue("UserName")?.ToString() ?? "";
        return (host, user);
    }

    /// <summary>WinSCP 연결에 필요한 전체 세션 정보 (개인키, 포트 포함).</summary>
    public static SessionConnectionInfo GetSessionConnectionInfo(string registryName)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{PuttySessionsPath}\{registryName}");
        if (key is null) return new SessionConnectionInfo();

        return new SessionConnectionInfo
        {
            HostName       = key.GetValue("HostName")?.ToString() ?? "",
            UserName       = key.GetValue("UserName")?.ToString() ?? "",
            Port           = key.GetValue("PortNumber") is int p ? p : 22,
            PrivateKeyPath = key.GetValue("PublicKeyFile")?.ToString() ?? ""
        };
    }
}

public class SessionConnectionInfo
{
    public string HostName       { get; set; } = "";
    public string UserName       { get; set; } = "";
    public int    Port           { get; set; } = 22;
    public string PrivateKeyPath { get; set; } = "";
}
