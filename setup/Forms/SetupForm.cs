using System.Reflection;
using Microsoft.Win32;

namespace SetupApp.Forms;

public partial class SetupForm : Form
{
    private const string AppName = "PuTTY Session Manager";
    private const string ExeName = "PuttySessionManager.exe";

    public SetupForm()
    {
        InitializeComponent();
        _txtPath.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "PuttySessionManager");
    }

    // ── 경로 선택 ────────────────────────────────────────────────────
    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description            = "설치할 폴더를 선택하세요.",
            SelectedPath           = _txtPath.Text,
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtPath.Text = dlg.SelectedPath;
    }

    // ── 설치 ─────────────────────────────────────────────────────────
    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        var installDir    = _txtPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(installDir))
        {
            MessageBox.Show("설치 경로를 입력해주세요.", "오류",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 백그라운드로 넘기기 전에 UI 값 미리 읽기
        var makeDesktop   = _chkDesktop.Checked;
        var makeStartMenu = _chkStartMenu.Checked;

        SetInstalling(true);

        try
        {
            await Task.Run(() => InstallFiles(installDir));

            // 바로가기/제어판 등록은 STA 필요 → UI 스레드에서
            if (makeDesktop)
            {
                SetStatus("바탕화면 바로가기 생성 중...", 70);
                CreateShortcut(
                    Path.Combine(installDir, ExeName),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        $"{AppName}.lnk"));
            }

            if (makeStartMenu)
            {
                SetStatus("시작 메뉴 등록 중...", 83);
                var startDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                Directory.CreateDirectory(startDir);
                CreateShortcut(
                    Path.Combine(installDir, ExeName),
                    Path.Combine(startDir, $"{AppName}.lnk"));
            }

            SetStatus("제어판 등록 중...", 93);
            RegisterUninstall(installDir);

            SetStatus("완료", 100);
            ShowComplete(installDir);
        }
        catch (Exception ex)
        {
            SetInstalling(false);
            MessageBox.Show($"설치 중 오류가 발생했습니다.\n\n{ex.Message}",
                "설치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // 파일 복사는 백그라운드에서
    private void InstallFiles(string installDir)
    {
        SetStatus("폴더 생성 중...", 10);
        Directory.CreateDirectory(installDir);

        SetStatus("파일 복사 중...", 30);
        var exePath = Path.Combine(installDir, ExeName);
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ExeName)
            ?? throw new Exception($"내장 리소스를 찾을 수 없습니다: {ExeName}");
        using var fs = File.Create(exePath);
        stream.CopyTo(fs);

        SetStatus("제거 스크립트 생성 중...", 55);
        WriteUninstallScript(installDir);
    }

    // WScript.Shell COM — STA 스레드(UI)에서 호출해야 함
    private static void CreateShortcut(string exePath, string shortcutPath)
    {
        dynamic shell    = Activator.CreateInstance(
            Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath       = exePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
        shortcut.IconLocation     = $"{exePath},0";
        shortcut.Description      = AppName;
        shortcut.Save();
    }

    private static void RegisterUninstall(string installDir)
    {
        var exePath = Path.Combine(installDir, ExeName);
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PuttySessionManager");
        key.SetValue("DisplayName",     AppName);
        key.SetValue("DisplayVersion",  "0.3.2");
        key.SetValue("Publisher",       "dori2005");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon",     $"{exePath},0");
        key.SetValue("UninstallString",
            $"powershell -ExecutionPolicy Bypass -File \"{Path.Combine(installDir, "uninstall.ps1")}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void WriteUninstallScript(string installDir)
    {
        // $$"""...""" 사용: {{ }} 만 C# 보간, 나머지 { } 는 PowerShell 리터럴
        var escapedDir = installDir.Replace(@"\", @"\\");
        var script = $$"""
            $AppName    = "{{AppName}}"
            $InstallDir = "{{escapedDir}}"

            $confirm = Read-Host "$AppName 을 제거합니다. 계속하시겠습니까? (Y/N)"
            if ($confirm -notmatch '^[Yy]') { exit 0 }

            Get-Process -Name "PuttySessionManager" -ErrorAction SilentlyContinue | Stop-Process -Force
            Remove-Item "$([System.Environment]::GetFolderPath('Desktop'))\$AppName.lnk" -ErrorAction SilentlyContinue
            Remove-Item "$([System.Environment]::GetFolderPath('StartMenu'))\Programs\$AppName.lnk" -ErrorAction SilentlyContinue
            Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PuttySessionManager" -Recurse -ErrorAction SilentlyContinue
            Start-Process cmd -ArgumentList "/c timeout /t 2 /nobreak >nul && rmdir /s /q `"$InstallDir`"" -WindowStyle Hidden
            Write-Host "제거 완료."
            """;
        File.WriteAllText(Path.Combine(installDir, "uninstall.ps1"), script,
            System.Text.Encoding.UTF8);
    }

    // ── 완료 화면 ────────────────────────────────────────────────────
    private void ShowComplete(string installDir)
    {
        _panelContent.Controls.Clear();

        var lbl = new Label
        {
            Text      = $"✓  설치가 완료되었습니다!\n\n설치 위치: {installDir}",
            Location  = new Point(20, 30),
            Size      = new Size(460, 80),
            Font      = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(0, 140, 0)
        };
        _panelContent.Controls.Add(lbl);

        _btnInstall.Text      = "닫기";
        _btnInstall.BackColor = Color.FromArgb(80, 80, 80);
        _btnInstall.Enabled   = true;
        _btnInstall.Click    -= BtnInstall_Click;
        _btnInstall.Click    += (_, _) => Close();
        _btnCancel.Visible    = false;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    private void SetInstalling(bool on)
    {
        _btnInstall.Enabled  = !on;
        _btnBrowse.Enabled   = !on;
        _txtPath.Enabled     = !on;
        _progressBar.Visible = on;
    }

    private void SetStatus(string msg, int progress)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(msg, progress));
            return;
        }
        _lblStatus.Text      = msg;
        _progressBar.Value   = progress;
    }
}
