@echo off
REM Copies the built plugin into Navisworks' own Plugins folder (needs administrator permission; it will ask).
REM Close Navisworks first. Plugins in user folders / ApplicationPlugins did NOT load in testing; this folder does.
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator permission...
  powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
if "%NAVISDIR%"=="" set "NAVISDIR=C:\Program Files\Autodesk\Navisworks Manage 2027"
set "SRC="
for /d %%D in ("%~dp0NavisBridge\bin\Release\*") do if exist "%%D\NavisBridge.dll" set "SRC=%%D"
set "DST=%NAVISDIR%\Plugins\NavisBridge"
if "%SRC%"=="" (echo Build first: run go.bat & pause & exit /b 1)
mkdir "%DST%" 2>nul
copy /Y "%SRC%\NavisBridge.dll" "%DST%\" 
copy /Y "%SRC%\NavisBridge.pdb" "%DST%\" 
copy /Y "%SRC%\Newtonsoft.Json.dll" "%DST%\"
xcopy /Y /I "%SRC%\Resources\*.png" "%DST%\Resources\" 
copy /Y "%SRC%\ClaudeRibbon.xaml" "%DST%\"
xcopy /Y /I "%SRC%\ClaudeRibbon.xaml" "%DST%\en-US\"
echo.
dir "%DST%"
echo.
echo Done. Start Navisworks and open the "AI Connect" tab.
timeout /t 3 >nul
