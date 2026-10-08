@echo off
REM Builds the plug-in for every supported Navisworks version (2022-2027) into dist\<year>\.
REM 2022-2026 build from public NuGet API packages (no Navisworks needed). 2027 needs Navisworks 2027 installed (NAVISDIR).
REM Requires the .NET SDK (winget install Microsoft.DotNet.SDK.8).
setlocal
if "%NAVISDIR%"=="" set "NAVISDIR=C:\Program Files\Autodesk\Navisworks Manage 2027"
where dotnet >nul 2>nul || (echo .NET SDK not found. Run: winget install Microsoft.DotNet.SDK.8 & pause & exit /b 1)
cd /d "%~dp0NavisBridge"
set FAILED=
for %%Y in (2022 2023 2024 2025 2026) do (
  echo === Building %%Y ===
  dotnet build -c Release --no-incremental -p:NavisVersion=%%Y -o "%~dp0dist\%%Y" > "%~dp0dist-build-%%Y.log" 2>&1
  findstr /C:"Build succeeded" "%~dp0dist-build-%%Y.log" >nul && (echo     OK) || (echo     FAILED - see dist-build-%%Y.log & set FAILED=1)
)
if exist "%NAVISDIR%\Autodesk.Navisworks.Api.dll" (
  echo === Building 2027 ===
  dotnet build -c Release --no-incremental -p:NavisDir="%NAVISDIR%" -o "%~dp0dist\2027" > "%~dp0dist-build-2027.log" 2>&1
  findstr /C:"Build succeeded" "%~dp0dist-build-2027.log" >nul && (echo     OK) || (echo     FAILED - see dist-build-2027.log & set FAILED=1)
) else (
  echo Navisworks 2027 not found in "%NAVISDIR%" - skipping 2027.
)
echo.
if defined FAILED (echo Some builds failed.) else (echo All builds done: dist\)
pause
