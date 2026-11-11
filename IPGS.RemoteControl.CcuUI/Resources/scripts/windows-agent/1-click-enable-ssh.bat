@echo off
chcp 65001 >nul
title KZTEK - 1-Click Enable Windows OpenSSH and Ping for CCU Remote Control

REM Check Administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [KZTEK] Requesting Administrator privileges ^(UAC Elevate^)...
    powershell.exe -NoProfile -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [KZTEK] Running bootstrap-windows-ssh.ps1...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0bootstrap-windows-ssh.ps1"
if %errorLevel% neq 0 (
    echo.
    echo [ERROR] Bootstrap failed ^(ExitCode: %errorLevel%^).
    pause
    exit /b %errorLevel%
)

echo.
echo [SUCCESS] Windows PC is ready to accept CCU connections over IPv4!
pause
