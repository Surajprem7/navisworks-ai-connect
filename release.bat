@echo off
REM One click: builds the plug-in for every Navisworks version and the installer.
REM Then tell Claude "built" and it publishes the release on GitHub.
REM Before running: the version must already be bumped (Claude does this).
setlocal
cd /d "%~dp0"
del /q "installer\Output\AI-Connect-Setup.exe" 2>nul
del /q "installer\Output\AI-Connect-Setup.exe.sha256" 2>nul
echo.|cmd /c build-all.bat
echo.|cmd /c installer\build-installer.bat
echo.
if exist "installer\Output\AI-Connect-Setup.exe" (
  echo DONE. The installer is built. Tell Claude: built
) else (
  echo FAILED. See installer\iscc.log and dist-build-*.log
)
pause
