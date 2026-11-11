[CmdletBinding()]
param(
    [switch]$Release,
    [switch]$Run,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

$Config = if ($Release) { "Release" } else { "Debug" }
$AppExe = Join-Path $ScriptDir "IPGS.RemoteControl.CcuUI\bin\$Config\net8.0\IPGS.RemoteControl.CcuUI.exe"

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   IPGS REMOTE CONTROL - TU DONG BUILD UNG DUNG           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Cau hinh build: $Config" -ForegroundColor Yellow
Write-Host " Thu muc goc   : $ScriptDir"
Write-Host ""

# 1. Dong tien trinh app neu dang chay de tranh loi khoa file
$running = Get-Process -Name "*CcuUI*" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Phat hien app dang chay. Dang dong de mo khoa file..." -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
    Write-Host "Da dong app thanh cong." -ForegroundColor Green
} else {
    Write-Host "Khong co tien trinh app nao dang chay." -ForegroundColor DarkGray
}

# 2. Build solution
Write-Host ""
Write-Host "Dang bien dich solution (IPGS.RemoteControl.sln)..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

& dotnet build IPGS.RemoteControl.sln -c $Config
$buildCode = $LASTEXITCODE

$sw.Stop()
$elapsed = [math]::Round($sw.Elapsed.TotalSeconds, 2)

if ($buildCode -ne 0) {
    Write-Host ""
    Write-Host "BUILD THAT BAI (Ma loi: $buildCode) sau $elapsed s!" -ForegroundColor Red
    exit $buildCode
}

Write-Host "Bien dich thanh cong trong $elapsed giay!" -ForegroundColor Green

# 3. Neu la Release -> Publish win-x64
if ($Release) {
    Write-Host ""
    Write-Host "Dang publish ban phat hanh win-x64..." -ForegroundColor Cyan
    & dotnet publish IPGS.RemoteControl.CcuUI\IPGS.RemoteControl.CcuUI.csproj -c Release -r win-x64 --self-contained false -o IPGS.RemoteControl.CcuUI\bin\publish\win-x64
    if ($LASTEXITCODE -eq 0) {
        $AppExe = Join-Path $ScriptDir "IPGS.RemoteControl.CcuUI\bin\publish\win-x64\IPGS.RemoteControl.CcuUI.exe"
        Write-Host "Publish win-x64 thanh cong!" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "----------------------------------------------------------" -ForegroundColor DarkGray
Write-Host "File chay (EXE): $AppExe" -ForegroundColor Yellow
Write-Host "----------------------------------------------------------" -ForegroundColor DarkGray

# 4. Tuy chon chay ngay
if ($Run) {
    if (Test-Path $AppExe) {
        Write-Host ""
        Write-Host "Dang khoi dong app: $AppExe..." -ForegroundColor Magenta
        Start-Process -FilePath $AppExe
    } else {
        Write-Host ""
        Write-Host "Khong tim thay file EXE de chay: $AppExe" -ForegroundColor Red
    }
} else {
    Write-Host ""
    Write-Host "Meo: Dung 'build.bat -Run' de vua build vua tu dong mo app!" -ForegroundColor DarkCyan
}

Write-Host ""
