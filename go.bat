@echo off
REM One-click: build the plugin, then install it into Navisworks (asks for administrator permission).
REM Close Navisworks first. To use another Navisworks folder:  set NAVISDIR=D:\Autodesk\Navisworks Manage 2027
if "%NAVISDIR%"=="" set "NAVISDIR=C:\Program Files\Autodesk\Navisworks Manage 2027"
where dotnet >nul 2>nul || (echo .NET SDK not found. Run:  winget install Microsoft.DotNet.SDK.8  & pause & exit /b 1)
if not exist "%NAVISDIR%\Autodesk.Navisworks.Api.dll" (echo Navisworks not found in "%NAVISDIR%". Set NAVISDIR to your install folder. & pause & exit /b 1)
cd /d "%~dp0NavisBridge"
dotnet build -c Release -p:NavisDir="%NAVISDIR%" > "%~dp0build-log.txt" 2>&1
findstr /C:"Build succeeded" "%~dp0build-log.txt" >nul && (call "%~dp0install-plugin.bat") || (type "%~dp0build-log.txt" & echo. & echo BUILD FAILED - see build-log.txt and docs\TROUBLESHOOTING.md & pause)
