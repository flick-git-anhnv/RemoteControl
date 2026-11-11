@echo off
REM KZTEK - Tu dong chay bootstrap-windows-ssh.ps1 sau khi cai Windows.
REM File nay + bootstrap-windows-ssh.ps1 + OpenSSH-Win64.zip dat trong USB tai
REM   sources\$OEM$\$$\Setup\Scripts\
REM Windows Setup tu chep vao C:\Windows\Setup\Scripts\ va goi file nay bang quyen SYSTEM.
REM autounattend.xml (FirstLogonCommands) goi lai file nay luc dang nhap lan dau de du phong
REM (may dung key OEM bo qua SetupComplete.cmd). Da chay thanh cong thi lan sau thoat ngay.
REM Log: C:\Windows\Temp\kztek-bootstrap.log

set "DIR=%~dp0"
set "LOG=%SystemRoot%\Temp\kztek-bootstrap.log"

if exist "%DIR%bootstrap-ok.txt" exit /b 0

echo ==== %DATE% %TIME% user=%USERNAME% ==== >> "%LOG%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%DIR%bootstrap-windows-ssh.ps1" >> "%LOG%" 2>&1
if errorlevel 1 goto fail

echo OK %DATE% %TIME% > "%DIR%bootstrap-ok.txt"
echo ==== OK ==== >> "%LOG%"
exit /b 0

:fail
echo ==== FAILED ==== >> "%LOG%"
exit /b 1
