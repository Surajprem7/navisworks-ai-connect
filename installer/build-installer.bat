@echo off
REM Compiles AI-Connect-Setup.exe. Run build-all.bat first. Installs Inno Setup 6 (free) via winget if missing.
setlocal
call :find
if not exist "%ISCC%" (
  echo Inno Setup not found - installing with winget...
  winget install -e --id JRSoftware.InnoSetup --accept-package-agreements --accept-source-agreements --silent
  call :find
)
if not exist "%ISCC%" (echo Inno Setup could not be installed. Install it from https://jrsoftware.org/isdl.php & pause & exit /b 1)
if not exist "%~dp0..\dist\2027\NavisBridge.dll" (echo Run build-all.bat first. & pause & exit /b 1)
"%ISCC%" "%~dp0AI-Connect.iss" > "%~dp0iscc.log" 2>&1
type "%~dp0iscc.log"
echo.
if exist "%~dp0Output\AI-Connect-Setup.exe" (echo Output: %~dp0Output\AI-Connect-Setup.exe) else (echo FAILED - see iscc.log)
timeout /t 5 >nul
exit /b 0
:find
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
exit /b 0
