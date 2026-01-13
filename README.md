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
