@echo off
REM Run this ONCE. It starts the build helper now and every time you log in to Windows. No admin rights needed.
setlocal
set "SCRIPT=%~dp0build-watcher.ps1"
schtasks /Create /F /TN "AI Connect Build Watcher" /SC ONLOGON /TR "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"%SCRIPT%\"" >nul
if errorlevel 1 (echo Could not create the scheduled task. & pause & exit /b 1)
schtasks /Run /TN "AI Connect Build Watcher" >nul
echo Done. The build helper is running. You can close this window.
echo To remove it later: schtasks /Delete /TN "AI Connect Build Watcher" /F
pause
