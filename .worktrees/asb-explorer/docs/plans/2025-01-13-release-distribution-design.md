# Release & Distribution Design

A distribution strategy for ASB Explorer as a cross-platform CLI tool.

## Goals

- Distribute self-contained executables for Windows, macOS, and Linux
- Provide simple one-liner install scripts
- Automate builds via GitHub Actions
- No package manager setup initially (Homebrew/winget can come later)

## Platforms

| RID | OS Runner | Output |
|-----|-----------|--------|
| `win-x64` | `windows-latest` | `asb-explorer-win-x64.zip` |
| `osx-x64` | `macos-latest` | `asb-explorer-osx-x64.tar.gz` |
| `osx-arm64` | `macos-latest` | `asb-explorer-osx-arm64.tar.gz` |
| `linux-x64` | `ubuntu-latest` | `asb-explorer-linux-x64.tar.gz` |

## Build Configuration

**Publish command:**

```bash
dotnet publish src/AsbExplorer/AsbExplorer.csproj \
  -c Release \
  -r <RID> \
  --self-contained true \
  -p:PublishTrimmed=true \
  -p:PublishSingleFile=true \
  -o ./publish
```

**csproj additions:**

```xml
<PropertyGroup>
  <AssemblyName>asb-explorer</AssemblyName>
  <Version>0.1.0</Version>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

## GitHub Actions Workflow

**Trigger:** Push of version tags (`v*`)

**Matrix build:** Builds all 4 platform variants in parallel, uploads to GitHub Release.

**File:** `.github/workflows/release.yml`

## Installation Scripts

### macOS / Linux (`scripts/install.sh`)

```bash
#!/bin/bash
set -e

VERSION="${1:-latest}"
OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
ARCH="$(uname -m)"

# Map architecture
case "$ARCH" in
  x86_64) ARCH="x64" ;;
  arm64|aarch64) ARCH="arm64" ;;
esac

# Map OS
case "$OS" in
  darwin) OS="osx" ;;
  linux) OS="linux" ;;
esac

ASSET="asb-explorer-${OS}-${ARCH}.tar.gz"
INSTALL_DIR="${INSTALL_DIR:-/usr/local/bin}"

# Download and install
curl -sL "https://github.com/jschristensen/asb-explorer/releases/${VERSION}/download/${ASSET}" | \
  tar xz -C "$INSTALL_DIR"

echo "Installed asb-explorer to $INSTALL_DIR"
```

**Usage:**

```bash
curl -fsSL https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.sh | bash
```

### Windows (`scripts/install.ps1`)

```powershell
$Version = if ($args[0]) { $args[0] } else { "latest" }
$Asset = "asb-explorer-win-x64.zip"
$InstallDir = "$env:LOCALAPPDATA\Programs\asb-explorer"

# Download and extract
Invoke-WebRequest -Uri "https://github.com/jschristensen/asb-explorer/releases/$Version/download/$Asset" -OutFile "$env:TEMP\$Asset"
Expand-Archive -Path "$env:TEMP\$Asset" -DestinationPath $InstallDir -Force

# Add to PATH for current user
$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($UserPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$UserPath;$InstallDir", "User")
}

Write-Host "Installed asb-explorer to $InstallDir"
```

**Usage:**

```powershell
irm https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.ps1 | iex
```

## Project Structure

New files to add:

```
├── .github/
│   └── workflows/
│       └── release.yml
├── scripts/
│   ├── install.sh
│   └── install.ps1
```

## README Installation Section

```markdown
## Installation

### macOS / Linux

curl -fsSL https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.sh | bash

Or with a specific version:

curl -fsSL https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.sh | bash -s v0.1.0

### Windows

irm https://raw.githubusercontent.com/jschristensen/asb-explorer/main/scripts/install.ps1 | iex

### Manual Download

Download the appropriate archive from GitHub Releases:

| Platform | File |
|----------|------|
| Windows x64 | asb-explorer-win-x64.zip |
| macOS x64 (Intel) | asb-explorer-osx-x64.tar.gz |
| macOS ARM (Apple Silicon) | asb-explorer-osx-arm64.tar.gz |
| Linux x64 | asb-explorer-linux-x64.tar.gz |

Extract and add to your PATH.

## Usage

Run asb-explorer and click "+ Add connection" to configure your Service Bus connection string.
```

## Release Process

1. Update version in `AsbExplorer.csproj`
2. Commit and tag: `git tag v0.1.0 && git push --tags`
3. GitHub Actions builds all platforms
4. GitHub Release created with all assets attached
5. Install scripts automatically fetch latest release

## Future Enhancements (Not In Scope)

- Homebrew tap for macOS/Linux
- winget manifest for Windows
- Chocolatey package
- Code signing certificates
- Licensing/monetization
