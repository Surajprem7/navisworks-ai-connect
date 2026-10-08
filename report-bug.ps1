# Collects non-sensitive diagnostics, copies them to the clipboard and opens the GitHub bug form.
$ErrorActionPreference = 'SilentlyContinue'
$repo = 'Surajprem7/navisworks-ai-connect'
$o = New-Object System.Collections.Generic.List[string]
$o.Add('AI Connect diagnostics ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
$o.Add('Windows: ' + [Environment]::OSVersion.VersionString)
$o.Add('Node: ' + ((& node --version 2>$null) -join ''))
$o.Add('.NET SDK: ' + ((& dotnet --version 2>$null) -join ''))
$pf = ${env:ProgramFiles}
foreach ($d in Get-ChildItem "$pf\Autodesk" -Directory -Filter 'Navisworks *') {
  $dll = Join-Path $d.FullName 'Plugins\NavisBridge\NavisBridge.dll'
  $v = if (Test-Path $dll) { (Get-Item $dll).VersionInfo.FileVersion } else { 'not installed' }
  $o.Add("$($d.Name): plugin $v")
}
$o.Add('Bridge port 47800 listening: ' + [bool](netstat -ano | Select-String '127.0.0.1:47800' | Select-String 'LISTENING'))
$log = Join-Path $env:TEMP 'navisbridge.log'
if (Test-Path $log) { $o.Add('--- last log lines ---'); $o.AddRange([string[]](Get-Content $log -Tail 30)) }
$text = ($o -join "`r`n").Replace($env:USERNAME, '<user>')
Set-Clipboard -Value $text
Write-Host $text
Write-Host ''
Write-Host 'Diagnostics copied to the clipboard. The GitHub form opens next: describe the problem and paste (Ctrl+V) into "Diagnostics".' -ForegroundColor Green
Start-Process "https://github.com/$repo/issues/new?template=bug_report.yml"
Start-Sleep 4
