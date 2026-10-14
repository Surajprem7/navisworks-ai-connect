# Background helper: when Claude writes build-request.txt in this folder, it runs the build and writes build-status.txt.
# Only runs build-all.bat and installer\build-installer.bat from this folder. Started at logon by install-watcher.bat.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$req = Join-Path $root 'build-request.txt'
$status = Join-Path $root 'build-status.txt'
Set-Content -Path $status -Value ("idle " + (Get-Date -Format s)) -Encoding ascii
while ($true) {
  if (Test-Path $req) {
    Remove-Item $req -Force -ErrorAction SilentlyContinue
    Set-Content -Path $status -Value ("running " + (Get-Date -Format s)) -Encoding ascii
    try {
      Remove-Item (Join-Path $root 'installer\Output\AI-Connect-Setup.exe') -Force -ErrorAction SilentlyContinue
      Remove-Item (Join-Path $root 'installer\Output\AI-Connect-Setup.exe.sha256') -Force -ErrorAction SilentlyContinue
      & cmd.exe /c "echo.|`"$root\build-all.bat`"" | Out-Null
      & cmd.exe /c "echo.|`"$root\installer\build-installer.bat`"" | Out-Null
      if (Test-Path (Join-Path $root 'installer\Output\AI-Connect-Setup.exe')) {
        Set-Content -Path $status -Value ("done " + (Get-Date -Format s)) -Encoding ascii
      } else {
        Set-Content -Path $status -Value ("failed " + (Get-Date -Format s) + " installer not built, see installer\iscc.log and dist-build-*.log") -Encoding ascii
      }
    } catch {
      Set-Content -Path $status -Value ("failed " + (Get-Date -Format s) + " " + $_.Exception.Message) -Encoding ascii
    }
  }
  Start-Sleep -Seconds 5
}
