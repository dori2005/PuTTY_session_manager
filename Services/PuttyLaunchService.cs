using System.Diagnostics;
using Microsoft.Win32;

namespace PuttySessionManager.Services;

public class PuttyLaunchService
{
    private string? _cachedPath;

    /// <summary>putty.exe 경로를 PATH → 일반 설치 경로 순으로 탐색한다.</summary>
    public string? FindPuttyPath()
    {
        if (_cachedPath is not null) return _cachedPath;

        // 1. PATH 환경변수
        var fromPath = FindInPath("putty.exe");
        if (fromPath is not null) return _cachedPath = fromPath;

        // 2. 일반 설치 경로
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PuTTY", "putty.exe"),
            @"C:\Program Files\PuTTY\putty.exe",
            @"C:\Program Files (x86)\PuTTY\putty.exe",
        };

        foreach (var path in candidates)
            if (File.Exists(path)) return _cachedPath = path;

        return null;
    }

    /// <summary>세션 DisplayName으로 PuTTY를 실행한다.</summary>
    public bool Launch(string sessionDisplayName)
    {
        var puttyPath = FindPuttyPath();
        if (puttyPath is null) return false;

        Process.Start(new ProcessStartInfo
        {
            FileName        = puttyPath,
            Arguments       = $"-load \"{sessionDisplayName}\"",
            UseShellExecute = false
        });
        return true;
    }

    public void SetCustomPath(string path)
    {
        _cachedPath = path;
    }

    private static string? FindInPath(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';'))
        {
            var full = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
