@echo off
REM Close Claude first (system tray -> Quit), then run this.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0add-to-claude-config.ps1"
echo.
pause
