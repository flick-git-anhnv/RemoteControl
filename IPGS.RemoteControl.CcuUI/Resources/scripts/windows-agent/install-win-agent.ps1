<#
.SYNOPSIS
    Cài / gỡ IPGS WinAgent (điều khiển từ xa máy Windows từ CCU) trên máy đích Windows.

.DESCRIPTION
    Chạy bằng PowerShell với quyền Administrator (trực tiếp hoặc từ xa qua SSH từ CCU)
    trong thư mục đã publish của WinAgent (chứa IPGS.RemoteControl.WinAgent.exe và appsettings.json):

        .\install-win-agent.ps1                                   # cài đặt, tự sinh token ngẫu nhiên
        .\install-win-agent.ps1 -Port 17600 -TargetFps 30 -JpegQuality 70 -AllowedClientIPs 192.168.1.10,192.168.1.0/24
        .\install-win-agent.ps1 -Uninstall                        # gỡ

    Việc script làm:
      1. Chép agent vào C:\Program Files\KztekWinAgent (hoặc -InstallDir), bảo toàn appsettings.json
         và chứng chỉ TLS tự ký agent-tls.pfx (tránh làm đổi vân tay TLS - Gotcha G028).
      2. Ghi appsettings.json: sinh token 32 ký tự hex nếu đang là placeholder, đặt Port, AllowedClientIPs,
         TargetFps, JpegQuality.
      3. Mở Windows Firewall cho cổng TCP $Port VÀ mở luôn ICMPv4/ICMPv6 Echo Request (Ping).
      4. Tắt timeout màn hình và sleep (powercfg) để phiên Desktop luôn sẵn sàng chụp hình GDI.
      5. Tạo Scheduled Task chạy trong phiên Desktop của user đang đăng nhập (hỗ trợ cả khi chạy từ SSH),
         quyền cao nhất (-LogonType Interactive -RunLevel Highest), tự khởi động ngay lập tức.
#>
[CmdletBinding()]
param(
    [string]   $InstallDir       = (Join-Path $env:ProgramFiles 'KztekWinAgent'),
    [int]      $Port             = 17600,
    [string[]] $AllowedClientIPs = @('0.0.0.0/0'),
    [string]   $Token            = '',
    [int]      $TargetFps        = 30,
    [int]      $JpegQuality      = 70,
    [switch]   $StartImmediately = $true,
    [switch]   $Uninstall
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$TaskName = 'KztekWinAgent'
$FwRule   = 'Kztek WinAgent'
$IcmpRule = 'KZTEK Allow ICMPv4 Ping'
$Exe      = 'IPGS.RemoteControl.WinAgent.exe'

$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Cần chạy PowerShell với quyền Administrator.'
}

function Stop-Agent {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
}

if ($Uninstall) {
    Stop-Agent
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName $FwRule -ErrorAction SilentlyContinue
    if (Test-Path $InstallDir) {
        # Bảo toàn agent-tls.pfx hoặc xoá toàn bộ khi gỡ hẳn
        Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host 'Đã gỡ WinAgent.' -ForegroundColor Green
    return
}

$src = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($src)) {
    $src = (Get-Location).Path
}
if (-not (Test-Path (Join-Path $src $Exe))) {
    throw "Không thấy $Exe tại '$src'. Hãy chạy script trong thư mục publish của WinAgent."
}

Stop-Agent
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# Bảo toàn agent-tls.pfx nếu đã có trong InstallDir (Gotcha G028: không làm mất vân tay TLS đã ghim trên CCU)
$tlsCertName = 'agent-tls.pfx'
$existingCertBytes = $null
$existingCertPath = Join-Path $InstallDir $tlsCertName
if (Test-Path $existingCertPath) {
    $existingCertBytes = [IO.File]::ReadAllBytes($existingCertPath)
}

if ((Resolve-Path $src).Path -ne (Resolve-Path $InstallDir).Path) {
    # Giữ appsettings.json cũ (token đã cấu hình) và agent-tls.pfx khi cài đè.
    $keepCfg  = Test-Path (Join-Path $InstallDir 'appsettings.json')
    $keepCert = Test-Path $existingCertPath
    Get-ChildItem $src -Force | Where-Object {
        -not ($keepCfg -and $_.Name -eq 'appsettings.json') -and
        -not ($keepCert -and $_.Name -eq $tlsCertName)
    } | Copy-Item -Destination $InstallDir -Recurse -Force
}

if ($null -ne $existingCertBytes -and -not (Test-Path $existingCertPath)) {
    [IO.File]::WriteAllBytes($existingCertPath, $existingCertBytes)
}

# ── appsettings.json ─────────────────────────────────────────────────────
$cfgPath = Join-Path $InstallDir 'appsettings.json'
if (-not (Test-Path $cfgPath)) {
    $srcCfg = Join-Path $src 'appsettings.json'
    if (Test-Path $srcCfg) {
        Copy-Item $srcCfg -Destination $cfgPath -Force
    } else {
        $defaultJson = @{
            RemoteControl = @{
                Port = $Port
                Token = ''
                AllowedClientIPs = @('0.0.0.0/0')
                TargetFps = $TargetFps
                JpegQuality = $JpegQuality
                CpuAdaptive = $false
                MaxFrameBytes = 8388608
            }
        } | ConvertTo-Json -Depth 6
        [IO.File]::WriteAllText($cfgPath, $defaultJson, (New-Object Text.UTF8Encoding($false)))
    }
}

$cfg = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
$rc  = $cfg.RemoteControl
$needNew = [string]::IsNullOrWhiteSpace($rc.Token) -or $rc.Token -like 'REPLACE_WITH*'
if ($Token) {
    $rc.Token = $Token
} elseif ($needNew) {
    $bytes = New-Object byte[] 16
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $rc.Token = -join ($bytes | ForEach-Object { $_.ToString('x2') })
}

$rc.Port = $Port
$rc.AllowedClientIPs = @($AllowedClientIPs)
if ($TargetFps -gt 0) {
    $rc | Add-Member -NotePropertyName 'TargetFps' -NotePropertyValue $TargetFps -Force
}
if ($JpegQuality -gt 0) {
    $rc | Add-Member -NotePropertyName 'JpegQuality' -NotePropertyValue $JpegQuality -Force
}

# UTF-8 không BOM để .NET đọc JSON chắc chắn.
[IO.File]::WriteAllText($cfgPath, ($cfg | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))

# ── Firewall (TCP Port + ICMP Ping) ──────────────────────────────────────
Remove-NetFirewallRule -DisplayName $FwRule -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $FwRule -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Any -Enabled True | Out-Null

Remove-NetFirewallRule -DisplayName $IcmpRule -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $IcmpRule -Direction Inbound -Action Allow -Protocol ICMPv4 -IcmpType 8 -Profile Any -Enabled True | Out-Null

# ── Tắt Sleep & Monitor Timeout (chống màn hình đen GDI) ─────────────────
try {
    & powercfg.exe /change monitor-timeout-ac 0
    & powercfg.exe /change monitor-timeout-dc 0
    & powercfg.exe /change standby-timeout-ac 0
    & powercfg.exe /change standby-timeout-dc 0
} catch { }

# ── Xác định User đang đăng nhập Desktop (hỗ trợ cả khi cài từ xa qua SSH) ─
function Resolve-InteractiveDesktopUser {
    try {
        $csUser = (Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue).UserName
        if (-not [string]::IsNullOrWhiteSpace($csUser)) {
            return $csUser
        }
    } catch { }

    try {
        $explorerProc = Get-CimInstance -ClassName Win32_Process -Filter "Name = 'explorer.exe'" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($explorerProc) {
            $owner = Invoke-CimMethod -InputObject $explorerProc -MethodName GetOwner -ErrorAction SilentlyContinue
            if ($owner -and -not [string]::IsNullOrWhiteSpace($owner.User)) {
                if (-not [string]::IsNullOrWhiteSpace($owner.Domain)) {
                    return "$($owner.Domain)\$($owner.User)"
                }
                return "$env:COMPUTERNAME\$($owner.User)"
            }
        }
    } catch { }

    try {
        $quserOut = & quser.exe 2>$null
        if ($quserOut) {
            foreach ($line in ($quserOut | Select-Object -Skip 1)) {
                $clean = ($line -replace '^\s*>?', '').Trim()
                if ($clean -match '^(\S+)\s+') {
                    $u = $Matches[1]
                    if (-not [string]::IsNullOrWhiteSpace($u)) {
                        return "$env:COMPUTERNAME\$u"
                    }
                }
            }
        }
    } catch { }

    if (-not [string]::IsNullOrWhiteSpace($env:USERDOMAIN) -and -not [string]::IsNullOrWhiteSpace($env:USERNAME)) {
        return "$env:USERDOMAIN\$env:USERNAME"
    }
    return $env:USERNAME
}

$desktopUser = Resolve-InteractiveDesktopUser

# ── Scheduled Task ───────────────────────────────────────────────────────
$exePath = Join-Path $InstallDir $Exe
$logPath = Join-Path $InstallDir 'agent.log'
$action  = New-ScheduledTaskAction -Execute $exePath -Argument "--hide-console --log `"$logPath`"" -WorkingDirectory $InstallDir
$trigger = New-ScheduledTaskTrigger -AtLogOn
$trigger.UserId = $desktopUser
$taskPrincipal = New-ScheduledTaskPrincipal -UserId $desktopUser -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $taskPrincipal -Settings $settings -Force | Out-Null

if ($StartImmediately) {
    try {
        Start-ScheduledTask -TaskName $TaskName -ErrorAction Stop
    } catch {
        Write-Host "  [Cảnh báo] Chưa thể khởi chạy ngay Scheduled Task '$TaskName' (User '$desktopUser' có thể chưa đăng nhập vào Desktop): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ''
Write-Host 'Đã cài WinAgent thành công.' -ForegroundColor Green
Write-Host "  Thư mục     : $InstallDir"
Write-Host "  Cổng        : $Port (TCP)"
Write-Host "  FPS / JPEG  : $TargetFps FPS / Quality $JpegQuality"
Write-Host "  Token       : $($rc.Token)    <- nhập vào CCU khi thêm máy"
Write-Host "  Log         : $logPath"
Write-Host "  Chạy        : Scheduled Task '$TaskName' (User: $desktopUser, Interactive/Highest)"
