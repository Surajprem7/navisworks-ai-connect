@echo off
REM Run this ONCE. Starts the build helper now and every time you log in to Windows. No admin rights needed.
setlocal
set "SCRIPT=%~dp0build-watcher.ps1"
set "CMD=powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"%SCRIPT%\""

REM 1) Start it right now.
start "" /B powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "%SCRIPT%"

REM 2) Start it at every login (per-user registry key, no admin needed).
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "AIConnectBuildWatcher" /t REG_SZ /d "%CMD%" /f >nul 2>nul
if errorlevel 1 (
  echo The helper is running now, but could not be set to start at login.
  echo After a restart, double-click this file again.
) else (
  echo Done. The build helper is running and will start at every login.
)
echo You can close this window.
pause
