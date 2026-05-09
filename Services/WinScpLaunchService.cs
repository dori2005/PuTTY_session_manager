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
    /// hostName/userName이 있으면 sftp://user@host 형식으로, 없으면 /open "세션명" 형식으로 시도.
    /// </summary>
    public bool Launch(string displayName, string hostName, string userName)
    {
        var path = FindWinScpPath();
        if (path is null) return false;

        // 호스트 정보가 있으면 URL 방식 (WinSCP 미설정 PC에서도 동작)
        string args;
        if (!string.IsNullOrWhiteSpace(hostName))
        {
            var userPart = string.IsNullOrWhiteSpace(userName) ? "" : $"{userName}@";
            args = $"sftp://{userPart}{hostName}";
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
