# Adds (or with -Remove, removes) the "navisworks" MCP server in Claude Desktop's config. Makes a backup first.
param(
  [string]$ServerJs = (Join-Path $PSScriptRoot 'mcp-server\server.js'),
  [switch]$Remove,
  [switch]$Quiet
)
$ErrorActionPreference = 'Stop'
function Say($m, $c = 'Gray') { if (-not $Quiet) { Write-Host $m -ForegroundColor $c } }

$cands = @()
$cands += Get-ChildItem "$env:LOCALAPPDATA\Packages\Claude_*\LocalCache\Roaming\Claude\claude_desktop_config.json" -ErrorAction SilentlyContinue
$p2 = Join-Path $env:APPDATA 'Claude\claude_desktop_config.json'
if (Test-Path $p2) { $cands += Get-Item $p2 }
if (-not $cands) { Say 'claude_desktop_config.json not found (is Claude Desktop installed and opened once?).' Yellow; exit 0 }

if ($Remove) {
  foreach ($f in $cands) {
    $txt = [IO.File]::ReadAllText($f.FullName)
    $new = [regex]::Replace($txt, '\s*"navisworks"\s*:\s*\{[^{}]*\}\s*,?', '', 1)
    if ($new -ne $txt) {
      try { $null = $new | ConvertFrom-Json } catch { Say "  Removal would break $($f.FullName) - left untouched." Red; continue }
      [IO.File]::WriteAllText($f.FullName, $new, (New-Object System.Text.UTF8Encoding($false)))
      Say "Removed navisworks entry from $($f.FullName)" Green
    }
  }
  exit 0
}

$nodeCmd = Get-Command node -ErrorAction SilentlyContinue
$nodeExe = if ($nodeCmd) { $nodeCmd.Source } else { 'C:\Program Files\nodejs\node.exe' }
if (-not (Test-Path $nodeExe)) { Say "node.exe not found. Install Node.js (winget install OpenJS.NodeJS.LTS), then run add-to-claude-config.bat again." Red; exit 1 }
if (-not (Test-Path $ServerJs)) { Say "server.js not found at $ServerJs" Red; exit 1 }

$nodeJson = $nodeExe.Replace('\', '\\')
$serverJson = $ServerJs.Replace('\', '\\')
$entry = '"navisworks": { "command": "' + $nodeJson + '", "args": ["' + $serverJson + '"] }'

foreach ($f in $cands) {
  $path = $f.FullName
  Say "Config: $path"
  $txt = [IO.File]::ReadAllText($path)
  if ($txt -match '"navisworks"\s*:') {
    # Upgrade: refresh the paths of the existing entry.
    $new = [regex]::Replace($txt, '"navisworks"\s*:\s*\{[^{}]*\}', { param($m) $entry }, 1)
    if ($new -eq $txt) { Say '  navisworks entry already up to date.'; continue }
    try { $null = $new | ConvertFrom-Json } catch { Say '  Update would not be valid JSON - left untouched.' Red; continue }
    Copy-Item $path "$path.bak-before-navisworks" -Force
    [IO.File]::WriteAllText($path, $new, (New-Object System.Text.UTF8Encoding($false)))
    Say '  Updated navisworks entry.' Green
    continue
  }
  $re = [regex]'("mcpServers"\s*:\s*\{)'
  if (-not $re.IsMatch($txt)) {
    # No mcpServers section yet: add one at the top level.
    $re2 = [regex]'^\s*\{'
    if (-not $re2.IsMatch($txt)) { Say '  config is not a JSON object - skipping.' Yellow; continue }
    $hasKeys = $txt -match '"'
    $new = $re2.Replace($txt, ('{' + "`r`n  " + '"mcpServers": { ' + $entry + ' }' + $(if ($hasKeys) { ',' } else { '' })), 1)
  } else {
    $hasOther = [regex]::IsMatch($txt, '"mcpServers"\s*:\s*\{\s*"')
    $new = $re.Replace($txt, ('$1' + "`r`n    " + $entry + $(if ($hasOther) { ',' } else { '' })), 1)
  }
  try { $null = $new | ConvertFrom-Json } catch { Say '  Result would not be valid JSON - leaving the file untouched.' Red; continue }
  Copy-Item $path "$path.bak-before-navisworks" -Force
  [IO.File]::WriteAllText($path, $new, (New-Object System.Text.UTF8Encoding($false)))
  Say "  Added navisworks entry. Backup: $path.bak-before-navisworks" Green
}
Say ''
Say 'Now fully quit Claude (system tray -> Quit), reopen it, and check Settings > Developer.'
