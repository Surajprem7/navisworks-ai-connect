# Installs Node.js LTS (needed by the AI Connect connector) if it is missing.
# Used by the installer. Never throws: exit 0 = Node.js is available, exit 1 = still missing.
# Order: 1) winget  2) direct download from nodejs.org with a SHA-256 check and a silent MSI install.
param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
function Say($m) { if (-not $Quiet) { Write-Host $m } }

function Test-Node {
  $c = Get-Command node -ErrorAction SilentlyContinue
  if ($c) { return $true }
  foreach ($p in @("$env:ProgramFiles\nodejs\node.exe", "${env:ProgramFiles(x86)}\nodejs\node.exe")) {
    if ($p -and (Test-Path $p)) { return $true }
  }
  return $false
}

if (Test-Node) { Say 'Node.js already installed.'; exit 0 }

# 1) winget (present on Windows 10 1809+ / 11 with App Installer)
try {
  $w = Get-Command winget -ErrorAction SilentlyContinue
  if ($w) {
    Say 'Installing Node.js LTS with winget...'
    $p = Start-Process -FilePath $w.Source -ArgumentList 'install -e --id OpenJS.NodeJS.LTS --silent --accept-package-agreements --accept-source-agreements' -Wait -PassThru -WindowStyle Hidden
    if (Test-Node) { Say 'Node.js installed (winget).'; exit 0 }
    Say "winget finished with code $($p.ExitCode) but Node.js was not found; trying direct download."
  }
} catch { Say "winget step failed: $($_.Exception.Message)" }

# 2) direct download from nodejs.org, verified with the published SHA-256
try {
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  Say 'Looking up the latest Node.js LTS...'
  $idx = Invoke-RestMethod -Uri 'https://nodejs.org/dist/index.json' -UseBasicParsing
  $lts = $idx | Where-Object { $_.lts } | Select-Object -First 1
  if (-not $lts) { throw 'no LTS entry in index.json' }
  $ver = $lts.version   # like v22.11.0
  $arch = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
  $name = "node-$ver-$arch.msi"
  $base = "https://nodejs.org/dist/$ver"
  $tmp = Join-Path $env:TEMP $name
  Say "Downloading $name ..."
  Invoke-WebRequest -Uri "$base/$name" -OutFile $tmp -UseBasicParsing
  $sums = (Invoke-WebRequest -Uri "$base/SHASUMS256.txt" -UseBasicParsing).Content -split "`n"
  $line = $sums | Where-Object { $_ -match [regex]::Escape($name) } | Select-Object -First 1
  if (-not $line) { throw 'checksum line not found' }
  $want = ($line -split '\s+')[0].Trim().ToLower()
  $got = (Get-FileHash -Algorithm SHA256 -Path $tmp).Hash.ToLower()
  if ($want -ne $got) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue; throw 'checksum mismatch, download discarded' }
  Say 'Checksum OK. Installing...'
  $p = Start-Process -FilePath 'msiexec.exe' -ArgumentList "/i `"$tmp`" /qn /norestart" -Wait -PassThru
  Remove-Item $tmp -Force -ErrorAction SilentlyContinue
  if (Test-Node) { Say 'Node.js installed.'; exit 0 }
  Say "msiexec finished with code $($p.ExitCode) but Node.js was not found."
} catch { Say "Direct download failed: $($_.Exception.Message)" }

exit 1
