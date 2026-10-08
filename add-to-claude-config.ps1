# Adds the "navisworks" MCP server to Claude Desktop's config. Makes a backup first; only inserts one entry.
$ErrorActionPreference = 'Stop'
$nodeCmd = Get-Command node -ErrorAction SilentlyContinue
$nodeExe = if ($nodeCmd) { $nodeCmd.Source } else { 'C:\Program Files\nodejs\node.exe' }
$serverJs = Join-Path $PSScriptRoot 'mcp-server\server.js'
if (-not (Test-Path $nodeExe)) { Write-Host "node.exe not found at $nodeExe" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $serverJs)) { Write-Host "server.js not found at $serverJs" -ForegroundColor Red; exit 1 }

$cands = @()
$cands += Get-ChildItem "$env:LOCALAPPDATA\Packages\Claude_*\LocalCache\Roaming\Claude\claude_desktop_config.json" -ErrorAction SilentlyContinue
$p2 = Join-Path $env:APPDATA 'Claude\claude_desktop_config.json'
if (Test-Path $p2) { $cands += Get-Item $p2 }
if (-not $cands) { Write-Host 'claude_desktop_config.json not found.' -ForegroundColor Red; exit 1 }

# JSON-escape the two paths
$nodeJson = $nodeExe.Replace('\', '\\')
$serverJson = $serverJs.Replace('\', '\\')
$entry = '"navisworks": { "command": "' + $nodeJson + '", "args": ["' + $serverJson + '"] },'

foreach ($f in $cands) {
  $path = $f.FullName
  Write-Host "Config: $path"
  $txt = [IO.File]::ReadAllText($path)
  if ($txt -match '"navisworks"\s*:') { Write-Host '  navisworks entry already present - nothing to do.'; continue }
  $re = [regex]'("mcpServers"\s*:\s*\{)'
  if (-not $re.IsMatch($txt)) { Write-Host '  no "mcpServers" section found - skipping.' -ForegroundColor Yellow; continue }
  $bak = "$path.bak-before-navisworks"
  Copy-Item $path $bak -Force
  $new = $re.Replace($txt, ('$1' + "`r`n    " + $entry), 1)
  try {
    $null = $new | ConvertFrom-Json
  } catch {
    Write-Host '  Result would not be valid JSON - leaving the file untouched.' -ForegroundColor Red
    continue
  }
  [IO.File]::WriteAllText($path, $new, (New-Object System.Text.UTF8Encoding($false)))
  Write-Host "  Added navisworks entry. Backup: $bak" -ForegroundColor Green
}
Write-Host ''
Write-Host 'Now fully quit Claude (system tray -> Quit), reopen it, and check Settings > Developer.'
