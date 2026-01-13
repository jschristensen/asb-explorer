# Release & Distribution Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Set up automated cross-platform builds and one-liner installation scripts for ASB Explorer.

**Architecture:** GitHub Actions workflow triggered by version tags builds self-contained executables for 4 platforms. Install scripts download from GitHub Releases.

**Tech Stack:** .NET 10, GitHub Actions, Bash, PowerShell

---

## Task 1: Update csproj with Publish Settings

**Files:**
- Modify: `src/AsbExplorer/AsbExplorer.csproj`

**Step 1: Add publish properties to csproj**

Update `src/AsbExplorer/AsbExplorer.csproj` to add assembly name, version, and publish settings:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- Distribution settings -->
    <AssemblyName>asb-explorer</AssemblyName>
    <Version>0.1.0</Version>

    <!-- Publish settings (used by CI) -->
    <PublishTrimmed>true</PublishTrimmed>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Terminal.Gui" />
    <PackageReference Include="Azure.Identity" />
    <PackageReference Include="Azure.ResourceManager.ServiceBus" />
    <PackageReference Include="Azure.Messaging.ServiceBus" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
</Project>
```

**Step 2: Verify build still works**

```bash
dotnet build src/AsbExplorer/AsbExplorer.csproj
```

Expected: Build succeeded.

**Step 3: Test local publish for one platform**

```bash
dotnet publish src/AsbExplorer/AsbExplorer.csproj -c Release -r osx-arm64 -o ./publish-test
ls -lh ./publish-test/
rm -rf ./publish-test
```

Expected: Single executable `asb-explorer` created, size ~20-50MB.

**Step 4: Commit**

```bash
git add src/AsbExplorer/AsbExplorer.csproj
git commit -m "build: add publish settings for self-contained distribution"
```

---

## Task 2: Create GitHub Actions Release Workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Step 1: Create workflows directory**

```bash
mkdir -p .github/workflows
```

**Step 2: Create release workflow**

Create `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build:
    strategy:
      matrix:
        include:
          - os: windows-latest
            rid: win-x64
            artifact: asb-explorer-win-x64.zip
          - os: macos-latest
            rid: osx-x64
            artifact: asb-explorer-osx-x64.tar.gz
          - os: macos-latest
            rid: osx-arm64
            artifact: asb-explorer-osx-arm64.tar.gz
          - os: ubuntu-latest
            rid: linux-x64
            artifact: asb-explorer-linux-x64.tar.gz

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish
        run: |
          dotnet publish src/AsbExplorer/AsbExplorer.csproj \
            -c Release \
            -r ${{ matrix.rid }} \
            -o ./publish

      - name: Package (Windows)
        if: matrix.os == 'windows-latest'
        run: |
          Compress-Archive -Path ./publish/* -DestinationPath ${{ matrix.artifact }}

      - name: Package (Unix)
        if: matrix.os != 'windows-latest'
        run: |
          tar -czvf ${{ matrix.artifact }} -C ./publish .

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.artifact }}
          path: ${{ matrix.artifact }}

  release:
    needs: build
    runs-on: ubuntu-latest

    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: ./artifacts
          merge-multiple: true

      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: ./artifacts/*
          generate_release_notes: true
```

**Step 3: Verify YAML syntax**

```bash
cat .github/workflows/release.yml | head -20
```

Expected: Valid YAML displayed without errors.

**Step 4: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add GitHub Actions release workflow for cross-platform builds"
```

---

## Task 3: Create macOS/Linux Install Script

**Files:**
- Create: `scripts/install.sh`

**Step 1: Create scripts directory**

```bash
mkdir -p scripts
```

**Step 2: Create install script**

Create `scripts/install.sh`:

```bash
#!/bin/bash
set -e

REPO="jschristensen/asb-explorer"
VERSION="${1:-latest}"

# Detect OS and architecture
OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
ARCH="$(uname -m)"

# Map architecture names
case "$ARCH" in
  x86_64) ARCH="x64" ;;
  arm64|aarch64) ARCH="arm64" ;;
  *)
    echo "Unsupported architecture: $ARCH"
    exit 1
    ;;
esac

# Map OS names
case "$OS" in
  darwin) OS="osx" ;;
  linux) OS="linux" ;;
  *)
    echo "Unsupported OS: $OS"
    exit 1
    ;;
esac

ASSET="asb-explorer-${OS}-${ARCH}.tar.gz"
INSTALL_DIR="${INSTALL_DIR:-/usr/local/bin}"

# Resolve latest version if needed
if [ "$VERSION" = "latest" ]; then
  VERSION=$(curl -sI "https://github.com/${REPO}/releases/latest" | grep -i "location:" | sed 's/.*tag\///' | tr -d '\r\n')
fi

echo "Installing asb-explorer ${VERSION} for ${OS}-${ARCH}..."

# Download and extract
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${VERSION}/${ASSET}"
curl -fsSL "$DOWNLOAD_URL" | tar xz -C "$INSTALL_DIR"

echo "Installed asb-explorer to $INSTALL_DIR/asb-explorer"
echo "Run 'asb-explorer' to start."
```

**Step 3: Make executable**

```bash
chmod +x scripts/install.sh
```

**Step 4: Verify script syntax**

```bash
bash -n scripts/install.sh && echo "Syntax OK"
```

Expected: "Syntax OK"

**Step 5: Commit**

```bash
git add scripts/install.sh
git commit -m "build: add macOS/Linux install script"
```

---

## Task 4: Create Windows Install Script

**Files:**
- Create: `scripts/install.ps1`

**Step 1: Create install script**

Create `scripts/install.ps1`:

```powershell
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
```

**Step 2: Verify script syntax**

```powershell
# If on Windows:
# powershell -Command "Get-Content scripts/install.ps1 | Out-Null; Write-Host 'Syntax OK'"
# If on macOS/Linux, just check it was created:
cat scripts/install.ps1 | head -10
```

Expected: Script content displayed.

**Step 3: Commit**

```bash
git add scripts/install.ps1
git commit -m "build: add Windows install script"
```

---

## Task 5: Create README with Installation Instructions

**Files:**
- Create: `README.md`

**Step 1: Create README**

Create `README.md`:

```markdown
# ASB Explorer

A terminal UI for exploring Azure Service Bus queues, topics, and subscriptions.

## Installation

### macOS / Linux

```bash
curl -fsSL https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.sh | bash
```

Install a specific version:

```bash
curl -fsSL https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.sh | bash -s v0.1.0
```

### Windows

```powershell
irm https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.ps1 | iex
```

### Manual Download

Download from [GitHub Releases](https://github.com/jschristensen/asb-explorer/releases):

| Platform | File |
|----------|------|
| Windows x64 | `asb-explorer-win-x64.zip` |
| macOS Intel | `asb-explorer-osx-x64.tar.gz` |
| macOS Apple Silicon | `asb-explorer-osx-arm64.tar.gz` |
| Linux x64 | `asb-explorer-linux-x64.tar.gz` |

Extract and add to your PATH.

## Usage

Run `asb-explorer` and click "+ Add connection" to configure your Service Bus connection string.

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Tab` | Switch panels |
| `Enter` | Select / Expand |
| `Esc` | Back / Close |
| `Ctrl+Q` | Quit |

## License

MIT
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add README with installation instructions"
```

---

## Task 6: Update .gitignore for Publish Artifacts

**Files:**
- Modify: `.gitignore`

**Step 1: Add publish directory to gitignore**

Append to `.gitignore`:

```
# Publish output
publish/
publish-test/
```

**Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: ignore publish output directories"
```

---

## Task 7: Test Release Workflow Locally (Optional)

**Step 1: Test publish for all platforms**

```bash
# Test one platform locally
dotnet publish src/AsbExplorer/AsbExplorer.csproj -c Release -r osx-arm64 -o ./publish-test
ls -lh ./publish-test/
./publish-test/asb-explorer --version 2>/dev/null || echo "No --version flag, that's OK"
rm -rf ./publish-test
```

Expected: Executable created and runs.

**Step 2: Verify all files are committed**

```bash
git status
```

Expected: Clean working tree.

---

## Summary

After completing all tasks:
- `dotnet publish` creates self-contained, trimmed single-file executable
- Pushing a `v*` tag triggers GitHub Actions to build all 4 platforms
- GitHub Release created automatically with all artifacts
- One-liner install scripts for macOS/Linux and Windows
- README documents installation and usage

**To create a release:**

```bash
git tag v0.1.0
git push origin v0.1.0
```

GitHub Actions will build and publish the release automatically.
