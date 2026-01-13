#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

$Repo = "jschristensen/asb-explorer"
$Asset = "asb-explorer-win-x64.zip"
$InstallDir = "$env:LOCALAPPDATA\Programs\asb-explorer"

# Resolve latest version if needed
if ($Version -eq "latest") {
    $LatestRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest"
    $Version = $LatestRelease.tag_name
}

Write-Host "Installing asb-explorer $Version..."

# Create install directory
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Download
$DownloadUrl = "https://github.com/$Repo/releases/download/$Version/$Asset"
$TempFile = Join-Path $env:TEMP $Asset
Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempFile

# Extract
Expand-Archive -Path $TempFile -DestinationPath $InstallDir -Force
Remove-Item $TempFile

# Add to PATH if not already present
$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($UserPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$UserPath;$InstallDir", "User")
    Write-Host "Added $InstallDir to PATH (restart terminal to use)"
}

Write-Host "Installed asb-explorer to $InstallDir\asb-explorer.exe"
Write-Host "Run 'asb-explorer' to start."
