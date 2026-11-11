<#
.SYNOPSIS
    KZTEK Zero-Touch Bootstrap cho máy đích Windows (Windows 10/11 / Windows Server).
    Đảm bảo tài khoản quản trị kztek/123456, kích hoạt OpenSSH Server (cổng 22 + SFTP),
    mở Ping ICMPv4/v6 + cổng TCP 22 & 17600, đặt DefaultShell tuyệt đối = powershell.exe,
    chuẩn hóa quyền NTFS sshd_config (icacls), tắt Sleep/LockScreen và bật AutoLogon.

.DESCRIPTION
    Chạy bằng PowerShell với quyền Administrator (hoặc gọi qua 1-click-enable-ssh.bat / autounattend.xml):
        .\bootstrap-windows-ssh.ps1
        .\bootstrap-windows-ssh.ps1 -SshPort 22 -EnsureAdminUser "kztek" -AdminPassword "123456"
#>
[CmdletBinding()]
param(
    [int]    $SshPort             = 22,
    [int]    $AgentPort           = 17600,
    [string] $EnsureAdminUser     = "kztek",
    [string] $AdminPassword       = "123456",
    [string] $AutoLogonUser       = "kztek",
    [string] $AutoLogonPassword   = "123456",
    [switch] $DisableSleepAndLock = $true
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

# Kiểm tra quyền Administrator
$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Cần chạy bootstrap-windows-ssh.ps1 với quyền Administrator."
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   KZTEK WINDOWS REMOTE BOOTSTRAP (OpenSSH + SFTP + ICMP Ping)  " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# ── Bước 0: Đảm bảo tài khoản Local Administrator mặc định (kztek / 123456) ──
if (-not [string]::IsNullOrWhiteSpace($EnsureAdminUser) -and -not [string]::IsNullOrWhiteSpace($AdminPassword)) {
    Write-Host "[0/7] Đảm bảo tài khoản quản trị '$EnsureAdminUser'..." -ForegroundColor Yellow
    $prevEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & net.exe user $EnsureAdminUser 2>&1 | Out-Null
    $ErrorActionPreference = $prevEap
    if ($LASTEXITCODE -ne 0) {
        & net.exe user $EnsureAdminUser $AdminPassword /add /y | Out-Null
        Write-Host "  -> Đã tạo mới tài khoản '$EnsureAdminUser'." -ForegroundColor Green
    } else {
        & net.exe user $EnsureAdminUser $AdminPassword | Out-Null
        Write-Host "  -> Đã cập nhật mật khẩu cho tài khoản '$EnsureAdminUser'." -ForegroundColor Green
    }
    & net.exe user $EnsureAdminUser /active:yes /expires:never | Out-Null
    # Lỗi 1378 (đã là thành viên nhóm) là vô hại; 'Stop' + 2>&1 sẽ biến stderr thành lỗi chết ở PS 5.1.
    $prevEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & net.exe localgroup Administrators $EnsureAdminUser /add 2>&1 | Out-Null
    $ErrorActionPreference = $prevEap
    $global:LASTEXITCODE = 0
    try {
        Set-LocalUser -Name $EnsureAdminUser -PasswordNeverExpires $true -ErrorAction SilentlyContinue
    } catch {
        & wmic.exe useraccount where "Name='$EnsureAdminUser'" set PasswordExpires=FALSE >$null 2>&1
    }
    Write-Host "  -> Tài khoản '$EnsureAdminUser' đã kích hoạt và thuộc nhóm Administrators." -ForegroundColor Green
}

# ── Bước 1: Chuyển Network Profile sang Private ──────────────────────────────
Write-Host "[1/7] Chuyển Network Connection Profile sang Private..." -ForegroundColor Yellow
try {
    Get-NetConnectionProfile -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.NetworkCategory -ne 'Private' -and $_.NetworkCategory -ne 'DomainAuthenticated') {
            Set-NetConnectionProfile -InterfaceIndex $_.InterfaceIndex -NetworkCategory Private -ErrorAction SilentlyContinue
        }
    }
    Write-Host "  -> Đã thiết lập Network Category = Private." -ForegroundColor Green
} catch {
    Write-Host "  -> Cảnh báo nhỏ khi chuyển Network Profile: $($_.Exception.Message)" -ForegroundColor DarkYellow
}

# ── Bước 2: Mở Windows Firewall cho phép Ping (ICMPv4 & ICMPv6) ─────────────
Write-Host "[2/7] Mở Windows Defender Firewall cho phép Ping (ICMPv4 & ICMPv6)..." -ForegroundColor Yellow
$icmpV4RuleName = "KZTEK Allow ICMPv4 Ping"
$icmpV6RuleName = "KZTEK Allow ICMPv6 Ping"

Remove-NetFirewallRule -DisplayName $icmpV4RuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $icmpV4RuleName `
    -Protocol ICMPv4 -IcmpType 8 `
    -Direction Inbound -Action Allow -Profile Any -Enabled True | Out-Null

Remove-NetFirewallRule -DisplayName $icmpV6RuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $icmpV6RuleName `
    -Protocol ICMPv6 -IcmpType 128 `
    -Direction Inbound -Action Allow -Profile Any -Enabled True | Out-Null

$prevEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
netsh advfirewall firewall add rule name="KZTEK ICMPv4 Echo" protocol=icmpv4:8,any dir=in action=allow profile=any 2>&1 | Out-Null
$ErrorActionPreference = $prevEap
Write-Host "  -> Đã mở ICMPv4 (Type 8) & ICMPv6 (Type 128) Echo Request Inbound." -ForegroundColor Green

# ── Bước 3: Cài đặt & kích hoạt OpenSSH Server (sshd) + ssh-agent ──────────
Write-Host "[3/7] Kiểm tra và cài đặt OpenSSH Server (sshd)..." -ForegroundColor Yellow
$sshdSvc = Get-Service -Name sshd -ErrorAction SilentlyContinue
if (-not $sshdSvc) {
    $offlineZip = Join-Path $PSScriptRoot "OpenSSH-Win64.zip"
    $offlineDir = Join-Path $PSScriptRoot "OpenSSH-Win64"
    $targetOpenSshDir = Join-Path $env:ProgramFiles "OpenSSH"

    if (Test-Path $offlineZip) {
        Write-Host "  -> Phát hiện gói offline OpenSSH-Win64.zip cạnh script, đang giải nén..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Force -Path $targetOpenSshDir | Out-Null
        Expand-Archive -Path $offlineZip -DestinationPath $targetOpenSshDir -Force
        $installScript = Get-ChildItem -Path $targetOpenSshDir -Filter "install-sshd.ps1" -Recurse | Select-Object -First 1
        if ($installScript) {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installScript.FullName
        }
    } elseif (Test-Path (Join-Path $offlineDir "install-sshd.ps1")) {
        Write-Host "  -> Phát hiện thư mục offline OpenSSH-Win64 cạnh script, đang cài đặt..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Force -Path $targetOpenSshDir | Out-Null
        Copy-Item -Path (Join-Path $offlineDir "*") -Destination $targetOpenSshDir -Recurse -Force
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $targetOpenSshDir "install-sshd.ps1")
    } else {
        Write-Host "  -> Cài đặt qua Windows Capability (OpenSSH.Server~~~~0.0.1.0)..." -ForegroundColor Cyan
        try {
            Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 -ErrorAction Stop | Out-Null
        } catch {
            Write-Host "  -> Fallback sang DISM /Add-Capability..." -ForegroundColor DarkYellow
            & dism.exe /Online /Add-Capability /CapabilityName:OpenSSH.Server~~~~0.0.1.0 /NoRestart
        }
    }
    $sshdSvc = Get-Service -Name sshd -ErrorAction SilentlyContinue
}

if (-not $sshdSvc) {
    throw "Không thể cài đặt dịch vụ OpenSSH Server (sshd). Hãy đặt gói OpenSSH-Win64.zip cạnh script hoặc kiểm tra Windows Update/Capability."
}
Write-Host "  -> Dịch vụ sshd đã sẵn sàng." -ForegroundColor Green

# ── Bước 4: Cấu hình sshd_config sạch + icacls & DefaultShell tuyệt đối ──────
Write-Host "[4/7] Cấu hình sshd_config sạch, phân quyền icacls và DefaultShell..." -ForegroundColor Yellow

Set-Service -Name sshd -StartupType Automatic
$agentSvc = Get-Service -Name ssh-agent -ErrorAction SilentlyContinue
if ($agentSvc) {
    Set-Service -Name ssh-agent -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service -Name ssh-agent -ErrorAction SilentlyContinue
}

if ($sshdSvc.Status -ne 'Running') {
    Start-Service -Name sshd -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

$sshDataDir = Join-Path $env:ProgramData "ssh"
New-Item -ItemType Directory -Force -Path $sshDataDir | Out-Null
$sshdConfigPath = Join-Path $sshDataDir "sshd_config"

# Ghi đè sshd_config sạch sẽ, KHÔNG có khối Match Group administrators (Gotcha G032)
$cleanCfg = @(
    "Port $SshPort",
    "ListenAddress 0.0.0.0",
    "PasswordAuthentication yes",
    "PubkeyAuthentication yes",
    "Subsystem sftp sftp-server.exe"
)
[IO.File]::WriteAllLines($sshdConfigPath, $cleanCfg, (New-Object System.Text.UTF8Encoding($false)))

# Chuẩn hóa phân quyền NTFS (icacls) cho sshd_config: chỉ SYSTEM:(F) và Administrators:(F)
& icacls.exe $sshdConfigPath /reset | Out-Null
& icacls.exe $sshdConfigPath /grant "SYSTEM:(F)" /grant "Administrators:(F)" /inheritance:r | Out-Null

# Đặt DefaultShell của OpenSSH là đường dẫn tuyệt đối tới powershell.exe và xóa DefaultShellCommandOption
$openSshRegPath = "HKLM:\SOFTWARE\OpenSSH"
if (-not (Test-Path $openSshRegPath)) {
    New-Item -Path $openSshRegPath -Force | Out-Null
}
New-ItemProperty -Path $openSshRegPath -Name "DefaultShell" -Value "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -PropertyType String -Force | Out-Null
Remove-ItemProperty -Path $openSshRegPath -Name "DefaultShellCommandOption" -ErrorAction SilentlyContinue

Restart-Service -Name sshd -Force
Write-Host "  -> Đã cấu hình sshd_config + icacls, DefaultShell tuyệt đối và khởi động lại sshd." -ForegroundColor Green

# ── Bước 5: Mở cổng TCP 22 (SSH) & TCP 17600 (WinAgent) trên Firewall ────────
Write-Host "[5/7] Mở cổng TCP $SshPort (SSH) và TCP $AgentPort (WinAgent) trên Windows Firewall..." -ForegroundColor Yellow
$sshFwRuleName = "KZTEK OpenSSH Server"
Remove-NetFirewallRule -DisplayName $sshFwRuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $sshFwRuleName `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort $SshPort `
    -Profile Any -Enabled True | Out-Null

$agentFwRuleName = "Kztek WinAgent"
Remove-NetFirewallRule -DisplayName $agentFwRuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $agentFwRuleName `
    -Direction Inbound -Action Allow -Protocol TCP -LocalPort $AgentPort `
    -Profile Any -Enabled True | Out-Null
Write-Host "  -> Đã mở cổng TCP $SshPort và TCP $AgentPort (Profile Any)." -ForegroundColor Green

# ── Bước 6: Bật LocalAccountTokenFilterPolicy = 1 ───────────────────────────
Write-Host "[6/7] Cấu hình LocalAccountTokenFilterPolicy = 1 cho quản trị từ xa..." -ForegroundColor Yellow
$sysPolicyPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
if (-not (Test-Path $sysPolicyPath)) {
    New-Item -Path $sysPolicyPath -Force | Out-Null
}
New-ItemProperty -Path $sysPolicyPath -Name "LocalAccountTokenFilterPolicy" -Value 1 -PropertyType DWord -Force | Out-Null
Write-Host "  -> Đã bật LocalAccountTokenFilterPolicy = 1." -ForegroundColor Green

# ── Bước 7: Tắt Sleep / Lock Screen & Cấu hình AutoLogon ─────────────────────
Write-Host "[7/7] Tối ưu hóa chế độ điện năng, chống khóa màn hình & AutoLogon..." -ForegroundColor Yellow
if ($DisableSleepAndLock) {
    & powercfg.exe /change monitor-timeout-ac 0
    & powercfg.exe /change monitor-timeout-dc 0
    & powercfg.exe /change standby-timeout-ac 0
    & powercfg.exe /change standby-timeout-dc 0
    & powercfg.exe /change hibernate-timeout-ac 0
    & powercfg.exe /change hibernate-timeout-dc 0

    $persPolicyPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization"
    if (-not (Test-Path $persPolicyPath)) {
        New-Item -Path $persPolicyPath -Force | Out-Null
    }
    New-ItemProperty -Path $persPolicyPath -Name "NoLockScreen" -Value 1 -PropertyType DWord -Force | Out-Null
    Write-Host "  -> Đã tắt Sleep, tắt Monitor Timeout và vô hiệu hóa Lock Screen." -ForegroundColor Green
}

if (-not [string]::IsNullOrWhiteSpace($AutoLogonUser)) {
    $winlogonPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    New-ItemProperty -Path $winlogonPath -Name "AutoAdminLogon" -Value "1" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $winlogonPath -Name "DefaultUserName" -Value $AutoLogonUser -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $winlogonPath -Name "DefaultPassword" -Value $AutoLogonPassword -PropertyType String -Force | Out-Null
    Write-Host "  -> Đã bật AutoAdminLogon cho tài khoản '$AutoLogonUser'." -ForegroundColor Green
}

# ── Bảng thông tin tóm tắt ───────────────────────────────────────────────────
$ipv4List = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -ne '127.0.0.1' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -ExpandProperty IPAddress
$ipv4Display = if ($ipv4List) { $ipv4List -join ", " } else { "(Chưa có IPv4 LAN)" }
$sshdFinal = (Get-Service -Name sshd -ErrorAction SilentlyContinue).Status

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "   HOÀN TẤT CẤU HÌNH MÁY WINDOWS SẴN SÀNG KẾT NỐI TỪ CCU!      " -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ("  IPv4 hiện tại    : {0}" -f $ipv4Display)
Write-Host ("  Ping ICMPv4/v6   : ĐÃ MỞ (Allow Inbound Any Profile)")
Write-Host ("  OpenSSH Server   : {0} (Cổng TCP {1}, SFTP + PowerShell)" -f $sshdFinal, $SshPort)
Write-Host ("  WinAgent Port    : ĐÃ MỞ (Cổng TCP {0})" -f $AgentPort)
Write-Host ("  Tài khoản SSH    : {0} (Mật khẩu mặc định: {1})" -f $EnsureAdminUser, $AdminPassword)
Write-Host "================================================================" -ForegroundColor Green
