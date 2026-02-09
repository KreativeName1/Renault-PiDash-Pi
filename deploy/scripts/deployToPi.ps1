# This script builds the PiDash project and deploys it to a Raspberry Pi over SSH.
# Run this from the dev machine, passing the Pi's SSH connection details and the path to the project.
# This is the Windows version; see deployToPi.sh for a Linux/Mac version.


# ---------- 1) Parse parameters ----------
param(
  [Parameter(Mandatory = $true)]
  [string]$PiHost,

  [Parameter(Mandatory = $true)]
  [string]$PiUser,

  [Parameter(Mandatory = $true)]
  [string]$ProjectDir,

  [ValidateSet("linux-arm64", "linux-arm")]
  [string]$Rid = "linux-arm64",

  [string]$Configuration = "Release",
  [string]$ServiceName = "pidash",
  [string]$RemoteStageDir = "/tmp/pidash-publish",
  [string]$RemoteInstallDir = "/opt/pidash",
  [string]$SshKeyPath = ""
)

# ---------- 2) Check required commands ----------
function Require-Cmd([string]$Name) {
  $cmd = Get-Command $Name -ErrorAction SilentlyContinue
  if (-not $cmd) { throw "Required command not found: $Name" }
}

Require-Cmd "dotnet"
Require-Cmd "ssh"
Require-Cmd "scp"


# ---------- 3) Build the project ----------
$ErrorActionPreference = "Stop"

$projectDirFull = (Resolve-Path $ProjectDir).Path
$srcDir = Join-Path $projectDirFull "src"

if (-not (Test-Path $srcDir)) {
  throw "Could not find '$srcDir'. Pass the repo root folder that contains 'src'."
}

Write-Host "`n=== Building PiDash ($Configuration, $Rid) ===" -ForegroundColor Cyan

Push-Location $srcDir
try {
  dotnet publish -c $Configuration -r $Rid --self-contained false
}
finally {
  Pop-Location
}

$publishDir = Join-Path $srcDir "bin\$Configuration\net10.0\$Rid\publish"
if (-not (Test-Path $publishDir)) {
  throw "Publish directory not found: $publishDir"
}

# ---------- 4) Upload to Pi ----------
Write-Host "`n=== Uploading to Pi ===" -ForegroundColor Cyan

$sshTarget = "$PiUser@$PiHost"
$sshArgs = @()
$scpArgs = @()
if ($SshKeyPath -ne "") {
  $sshArgs += @("-i", $SshKeyPath)
  $scpArgs += @("-i", $SshKeyPath)
}

ssh @sshArgs $sshTarget "sudo rm -rf '$RemoteStageDir' && mkdir -p '$RemoteStageDir'"
scp @scpArgs -r "$publishDir\*" "$sshTarget:$RemoteStageDir/"

# ---------- 5) Deploy & restart service ----------
Write-Host "`n=== Deploying & restarting service ===" -ForegroundColor Cyan

$remoteCmd = @"
set -e
sudo systemctl stop $ServiceName || true
sudo mkdir -p '$RemoteInstallDir'
sudo rm -rf '$RemoteInstallDir'/*
sudo cp -a '$RemoteStageDir'/. '$RemoteInstallDir'/.
sudo chmod -R a+rX '$RemoteInstallDir'
sudo systemctl start $ServiceName
sudo systemctl status $ServiceName --no-pager
"@

ssh @sshArgs $sshTarget $remoteCmd

Write-Host "`n✅ Deploy complete. Tail logs with:" -ForegroundColor Green
Write-Host "ssh $sshTarget 'sudo journalctl -u $ServiceName -f'" -ForegroundColor Green
