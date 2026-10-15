@echo off
REM Run this ONCE. Starts the build helper now and every time you log in to Windows. No admin rights needed.
setlocal
set "SCRIPT=%~dp0build-watcher.ps1"
set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "VBS=%STARTUP%\AI Connect Build Watcher.vbs"
> "%VBS%" echo CreateObject("WScript.Shell").Run "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ""%SCRIPT%""", 0, False
if not exist "%VBS%" (echo Could not write to the Startup folder. & pause & exit /b 1)
wscript.exe "%VBS%"
echo Done. The build helper is running. You can close this window.
echo To remove it later: delete "%VBS%"
pause
