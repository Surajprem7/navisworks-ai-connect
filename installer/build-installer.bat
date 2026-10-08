@echo off
REM Compiles AI-Connect-Setup.exe. Run build-all.bat first. Needs Inno Setup 6 (winget install JRSoftware.InnoSetup).
setlocal
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (echo Inno Setup 6 not found. Run: winget install JRSoftware.InnoSetup & pause & exit /b 1)
if not exist "%~dp0..\dist\2027\NavisBridge.dll" (echo Run build-all.bat first. & pause & exit /b 1)
"%ISCC%" "%~dp0AI-Connect.iss"
echo.
echo Output: %~dp0Output\AI-Connect-Setup.exe
pause
