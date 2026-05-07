param([switch]$Clean)

$dotnet     = "C:\Program Files\dotnet\dotnet.exe"
$root       = $PSScriptRoot
$mainProj   = "$root\PuttySessionManager.csproj"
$setupProj  = "$root\setup\SetupApp.csproj"
$resDir     = "$root\setup\Resources"
$outputExe  = "$root\setup.exe"

Write-Host ""
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "  PuTTY Session Manager 빌드" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan

# 1. 메인 앱 self-contained 빌드
Write-Host "`n[1/3] 메인 앱 빌드 중..."
& $dotnet publish $mainProj -c Release -r win-x64 `
    -p:PublishSingleFile=true -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output "$root\setup\_mainbuild" -nologo --nologo 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "메인 앱 빌드 실패" -ForegroundColor Red; exit 1 }
Write-Host "      완료" -ForegroundColor Gray

# 2. 메인 exe를 setup 리소스로 복사
Write-Host "`n[2/3] 리소스 준비 중..."
New-Item -ItemType Directory -Force $resDir | Out-Null
Copy-Item "$root\setup\_mainbuild\PuttySessionManager.exe" "$resDir\PuttySessionManager.exe" -Force
Write-Host "      완료" -ForegroundColor Gray

# 3. setup.exe 빌드
Write-Host "`n[3/3] 인스톨러 빌드 중..."
& $dotnet publish $setupProj -c Release -r win-x64 `
    -p:PublishSingleFile=true -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output "$root\setup\_setupbuild" -nologo 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "인스톨러 빌드 실패" -ForegroundColor Red; exit 1 }

Copy-Item "$root\setup\_setupbuild\setup.exe" $outputExe -Force
Write-Host "      완료" -ForegroundColor Gray

# 임시 빌드 폴더 정리
Remove-Item "$root\setup\_mainbuild" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$root\setup\_setupbuild" -Recurse -Force -ErrorAction SilentlyContinue

$size = [math]::Round((Get-Item $outputExe).Length / 1MB, 1)
Write-Host ""
Write-Host "=============================" -ForegroundColor Green
Write-Host "  완료: setup.exe ($size MB)" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green
Write-Host ""
