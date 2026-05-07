#Requires -Version 5.1
<#
.SYNOPSIS
    PuTTY Session Manager 설치 스크립트
.DESCRIPTION
    - %LOCALAPPDATA%\Programs\PuttySessionManager\ 에 설치
    - 바탕화면 바로가기 생성
    - 시작 메뉴 바로가기 생성
    - 제어판 "앱 및 기능" 등록 (관리자 권한 불필요)
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$AppName    = "PuTTY Session Manager"
$ExeName    = "PuttySessionManager.exe"
$InstallDir = "$env:LOCALAPPDATA\Programs\PuttySessionManager"
$ExeSrc     = Join-Path $PSScriptRoot "bin\$ExeName"

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "  $AppName 설치" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# exe 존재 확인
if (-not (Test-Path $ExeSrc)) {
    Write-Host "[오류] $ExeName 을 찾을 수 없습니다: $ExeSrc" -ForegroundColor Red
    Write-Host "install.ps1 과 같은 폴더에 bin\$ExeName 이 있어야 합니다."
    exit 1
}

# 1. 설치 디렉토리 생성 및 복사
Write-Host "[1/4] 파일 복사 중..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path $ExeSrc -Destination "$InstallDir\$ExeName" -Force
Write-Host "      설치 위치: $InstallDir" -ForegroundColor Gray

# 2. 바탕화면 바로가기
Write-Host "[2/4] 바탕화면 바로가기 생성 중..."
$desktop = [Environment]::GetFolderPath("Desktop")
$wsh     = New-Object -ComObject WScript.Shell
$sc      = $wsh.CreateShortcut("$desktop\$AppName.lnk")
$sc.TargetPath       = "$InstallDir\$ExeName"
$sc.WorkingDirectory = $InstallDir
$sc.IconLocation     = "$InstallDir\$ExeName,0"
$sc.Description      = $AppName
$sc.Save()
Write-Host "      완료" -ForegroundColor Gray

# 3. 시작 메뉴 바로가기
Write-Host "[3/4] 시작 메뉴 바로가기 생성 중..."
$startMenu = [Environment]::GetFolderPath("StartMenu")
$startDir  = "$startMenu\Programs"
New-Item -ItemType Directory -Force -Path $startDir | Out-Null
$sc2 = $wsh.CreateShortcut("$startDir\$AppName.lnk")
$sc2.TargetPath       = "$InstallDir\$ExeName"
$sc2.WorkingDirectory = $InstallDir
$sc2.IconLocation     = "$InstallDir\$ExeName,0"
$sc2.Description      = $AppName
$sc2.Save()
Write-Host "      완료" -ForegroundColor Gray

# 4. 제어판 "앱 및 기능" 등록 (HKCU — 관리자 권한 불필요)
Write-Host "[4/4] 제어판 등록 중..."
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PuttySessionManager"
$version      = (Get-Item "$InstallDir\$ExeName").VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($version)) { $version = "1.0.0" }

New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName"          -Value $AppName
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion"       -Value $version
Set-ItemProperty -Path $uninstallKey -Name "Publisher"            -Value "dori2"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation"      -Value $InstallDir
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon"          -Value "$InstallDir\$ExeName,0"
Set-ItemProperty -Path $uninstallKey -Name "UninstallString"      -Value "powershell -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`""
Set-ItemProperty -Path $uninstallKey -Name "NoModify"             -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name "NoRepair"             -Value 1 -Type DWord
Write-Host "      완료" -ForegroundColor Gray

# uninstall.ps1 도 설치 디렉토리에 복사
$uninstallSrc = Join-Path $PSScriptRoot "uninstall.ps1"
if (Test-Path $uninstallSrc) {
    Copy-Item -Path $uninstallSrc -Destination "$InstallDir\uninstall.ps1" -Force
}

Write-Host ""
Write-Host "====================================" -ForegroundColor Green
Write-Host "  설치 완료!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""
Write-Host "바탕화면의 '$AppName' 바로가기로 실행하세요." -ForegroundColor White
Write-Host ""
