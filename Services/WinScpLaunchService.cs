using System.Diagnostics;

namespace PuttySessionManager.Services;

public class WinScpLaunchService
{
    private string? _cachedPath;

    public string? FindWinScpPath()
    {
        if (_cachedPath is not null) return _cachedPath;

        // 1. PATH
        var fromPath = FindInPath("winscp.exe");
        if (fromPath is not null) return _cachedPath = fromPath;

        // 2. 일반 설치 경로
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),   "WinSCP", "WinSCP.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"WinSCP", "WinSCP.exe"),
            @"C:\Program Files\WinSCP\WinSCP.exe",
            @"C:\Program Files (x86)\WinSCP\WinSCP.exe",
        };
        foreach (var p in candidates)
            if (File.Exists(p)) return _cachedPath = p;

        return null;
    }

    /// <summary>
    /// WinSCP로 세션을 연다.
    /// PuTTY 레지스트리에서 호스트/유저/포트/개인키 정보를 읽어 그대로 넘긴다.
    /// 호스트가 없으면 /open "세션명" 형식으로 폴백.
    /// </summary>
    public bool Launch(string displayName, SessionConnectionInfo info)
    {
        var path = FindWinScpPath();
        if (path is null) return false;

        string args;
        if (!string.IsNullOrWhiteSpace(info.HostName))
        {
            var userPart = string.IsNullOrWhiteSpace(info.UserName) ? "" : $"{info.UserName}@";
            var portPart = (info.Port == 22 || info.Port == 0) ? "" : $":{info.Port}";
            args = $"sftp://{userPart}{info.HostName}{portPart}";

            // PuTTY 개인키 파일이 등록돼있으면 그대로 사용 (.ppk 직접 지원)
            if (!string.IsNullOrWhiteSpace(info.PrivateKeyPath) && File.Exists(info.PrivateKeyPath))
                args += $" /privatekey=\"{info.PrivateKeyPath}\"";
        }
        else
        {
            args = $"/open \"{displayName}\"";
        }

        Process.Start(new ProcessStartInfo
        {
            FileName        = path,
            Arguments       = args,
            UseShellExecute = false
        });
        return true;
    }

    /// <summary>편의 오버로드 (호환성).</summary>
    public bool Launch(string displayName, string hostName, string userName)
        => Launch(displayName, new SessionConnectionInfo { HostName = hostName, UserName = userName });

    public void SetCustomPath(string path) => _cachedPath = path;

    private static string? FindInPath(string exe)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            var full = Path.Combine(dir.Trim(), exe);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
