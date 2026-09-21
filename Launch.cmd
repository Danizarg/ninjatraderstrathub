@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Lab.ps1" %*
set "lab_exit_code=%errorlevel%"
echo.
pause
exit /b %lab_exit_code%
