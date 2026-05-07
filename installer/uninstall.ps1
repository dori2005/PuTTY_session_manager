#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$AppName    = "PuTTY Session Manager"
$InstallDir = "$env:LOCALAPPDATA\Programs\PuttySessionManager"

Write-Host ""
Write-Host "====================================" -ForegroundColor Yellow
Write-Host "  $AppName 제거" -ForegroundColor Yellow
Write-Host "====================================" -ForegroundColor Yellow
Write-Host ""

$confirm = Read-Host "$AppName 을(를) 제거합니다. 계속하시겠습니까? (Y/N)"
if ($confirm -notmatch '^[Yy]') { Write-Host "취소됨."; exit 0 }

# 실행 중이면 종료
Get-Process -Name "PuttySessionManager" -ErrorAction SilentlyContinue | Stop-Process -Force

# 바탕화면 바로가기 삭제
$desktop = [Environment]::GetFolderPath("Desktop")
Remove-Item "$desktop\$AppName.lnk" -ErrorAction SilentlyContinue

# 시작 메뉴 바로가기 삭제
$startMenu = [Environment]::GetFolderPath("StartMenu")
Remove-Item "$startMenu\Programs\$AppName.lnk" -ErrorAction SilentlyContinue

# 제어판 등록 제거
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PuttySessionManager" -Recurse -ErrorAction SilentlyContinue

# 파일 삭제 (잠시 후 삭제되도록 cmd에 위임)
Write-Host "파일 삭제 중..."
Start-Process cmd -ArgumentList "/c timeout /t 2 /nobreak >nul && rmdir /s /q `"$InstallDir`"" -WindowStyle Hidden

Write-Host ""
Write-Host "제거 완료." -ForegroundColor Green
Write-Host "그룹 설정 파일(%APPDATA%\PuttySessionManager\groups.json)은 유지됩니다." -ForegroundColor Gray
Write-Host ""
