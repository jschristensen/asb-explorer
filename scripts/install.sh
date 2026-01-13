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
