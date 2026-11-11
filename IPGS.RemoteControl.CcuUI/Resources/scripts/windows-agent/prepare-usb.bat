@echo off
REM KZTEK - Chep bo tu dong cau hinh vao USB cai Windows.
REM Cach dung:  prepare-usb.bat E:      (E: la o USB cai Windows da tao san)

if "%~1"=="" (
    echo Cach dung: %~nx0 ^<o USB, vi du E:^>
    exit /b 1
)
set "USB=%~1"
if not exist "%USB%\sources\" (
    echo [ERROR] %USB% khong phai USB cai Windows ^(khong thay thu muc sources^).
    exit /b 1
)
set "DST=%USB%\sources\$OEM$\$$\Setup\Scripts"
if not exist "%DST%" mkdir "%DST%"

copy /y "%~dp0autounattend.xml"           "%USB%\" >nul || goto fail
copy /y "%~dp0SetupComplete.cmd"          "%DST%\" >nul || goto fail
copy /y "%~dp0bootstrap-windows-ssh.ps1"  "%DST%\" >nul || goto fail
copy /y "%~dp0OpenSSH-Win64.zip"          "%DST%\" >nul || goto fail

echo [OK] Da chep autounattend.xml vao %USB%\ va 3 file vao %DST%
exit /b 0

:fail
echo [ERROR] Chep file that bai.
exit /b 1
