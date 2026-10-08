@echo off
REM Quick health check for AI Connect. Run it any time something doesn't work.
if "%NAVISDIR%"=="" set "NAVISDIR=C:\Program Files\Autodesk\Navisworks Manage 2027"
echo === AI Connect setup check ===
where dotnet >nul 2>nul && (echo [OK]   .NET SDK found) || echo [FAIL] .NET SDK missing - run: winget install Microsoft.DotNet.SDK.8
where node   >nul 2>nul && (echo [OK]   Node.js found) || echo [FAIL] Node.js missing - run: winget install OpenJS.NodeJS.LTS
if exist "%NAVISDIR%\Autodesk.Navisworks.Api.dll" (echo [OK]   Navisworks found: %NAVISDIR%) else echo [FAIL] Navisworks not found in %NAVISDIR% - set NAVISDIR
if exist "%NAVISDIR%\Plugins\NavisBridge\NavisBridge.dll" (echo [OK]   Plugin installed) else echo [FAIL] Plugin not installed - run go.bat
netstat -ano | findstr "127.0.0.1:47800" | findstr LISTENING >nul && (echo [OK]   Bridge is ON - port 47800 is listening) || echo [INFO] Bridge is OFF - in Navisworks click AI Connect tab ^> AI Connect
echo.
pause
