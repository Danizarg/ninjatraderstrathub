@echo off
setlocal
if not exist "%~dp0AdaptiveTradingLab.exe" (
    powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0app\Build.ps1"
    if errorlevel 1 (
        pause
        exit /b 1
    )
)
start "" "%~dp0AdaptiveTradingLab.exe"
exit /b 0
