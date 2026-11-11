@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-app.ps1" %*
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build that bai! (Ma loi: %ERRORLEVEL%)
    exit /b %ERRORLEVEL%
)
endlocal
