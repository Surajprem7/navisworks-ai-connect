@echo off
REM One click: builds all versions, builds the installer, and publishes the GitHub release.
REM Before running: bump the version (NavisBridge.csproj, installer\AI-Connect.iss, mcp-server), add a CHANGELOG entry, commit and push.
setlocal
cd /d "%~dp0"

for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "([xml](Get-Content 'NavisBridge\NavisBridge.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"`) do set VER=%%V
if "%VER%"=="" (echo Could not read the version from NavisBridge.csproj. & pause & exit /b 1)
echo === Releasing v%VER% ===

where gh >nul 2>nul || (
  echo Installing GitHub CLI...
  winget install -e --id GitHub.cli --accept-package-agreements --accept-source-agreements --silent
  echo GitHub CLI installed. Close this window and run release.bat again.
  pause & exit /b 0
)
gh auth status >nul 2>nul || (
  echo Log in to GitHub once. Choose GitHub.com, HTTPS, and "Login with a web browser".
  gh auth login
)
gh auth status >nul 2>nul || (echo GitHub login failed. & pause & exit /b 1)

git status --porcelain > "%TEMP%\aic-git.txt"
for %%F in ("%TEMP%\aic-git.txt") do if %%~zF GTR 0 (echo You have uncommitted changes. Commit and push first. & pause & exit /b 1)
git fetch -q
for /f %%C in ('git rev-list --count origin/main..HEAD') do if not "%%C"=="0" (echo You have commits that are not pushed. Run git push first. & pause & exit /b 1)

del /q "installer\Output\AI-Connect-Setup.exe" 2>nul
del /q "installer\Output\AI-Connect-Setup.exe.sha256" 2>nul
echo.|cmd /c build-all.bat
echo.|cmd /c installer\build-installer.bat
if not exist "installer\Output\AI-Connect-Setup.exe" (echo Installer was not built. See installer\iscc.log & pause & exit /b 1)

powershell -NoProfile -Command "$t=Get-Content 'CHANGELOG.md' -Raw; if($t -match '(?ms)^## %VER%\s*\r?\n(.*?)(?=^## |\z)'){ $n=$matches[1].Trim() } else { $n='Release %VER%' }; $n += \"`r`n`r`nInstall: close Navisworks and Claude, run AI-Connect-Setup.exe, restart both. Installed v1.1.x offers this update inside Navisworks.`r`n`r`nVerify the download: SHA-256 is in AI-Connect-Setup.exe.sha256.\"; Set-Content -Encoding utf8 '%TEMP%\aic-notes.md' $n"

gh release create "v%VER%" "installer\Output\AI-Connect-Setup.exe" "installer\Output\AI-Connect-Setup.exe.sha256" --title "AI Connect v%VER%" --notes-file "%TEMP%\aic-notes.md" --target main --latest
if errorlevel 1 (echo Publishing failed. & pause & exit /b 1)
echo.
echo Done. v%VER% is live.
pause
