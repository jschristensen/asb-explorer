# Azure Service Bus Explorer TUI - Design

A terminal-based tool for debugging and troubleshooting Azure Service Bus queues, topics, and subscriptions.

## Goals

- **Primary use case:** Debugging and troubleshooting - peek at messages, inspect dead-letter queues, diagnose issues
- **Non-destructive:** Peek-only operations, no risk of accidental message loss
- **Quick access:** Favorites and recently used connections for fast navigation
- **Full discovery:** Browse all accessible namespaces across Azure subscriptions

## Technology Stack

- .NET 10
- Terminal.Gui (v2.x) for TUI
- Azure.Identity for DefaultAzureCredential
- Azure.ResourceManager.ServiceBus for namespace/queue discovery
- Azure.Messaging.ServiceBus for message operations

## Authentication

Uses `DefaultAzureCredential` which chains through:
1. Environment variables
2. Azure CLI (`az login`)
3. Visual Studio / VS Code credentials
4. Managed Identity (when running in Azure)

Required RBAC roles:
- `Reader` on subscription/resource group for discovery
- `Azure Service Bus Data Reader` (or Owner) for message peek operations

## UI Layout

```
┌─────────────────────────────────────────────────────────────┐
│                        TUI Shell                            │
│  ┌──────────────┐  ┌──────────────────────────────────────┐ │
│  │              │  │  Message List                        │ │
│  │  Tree View   │  │  ─────────────────────────────────── │ │
│  │              │  │  MessageId | Enqueued | Size | ...   │ │
│  │  - Sub 1     │  ├──────────────────────────────────────┤ │
│  │    - NS1     │  │  Message Details                     │ │
│  │      - queue │  │  ─────────────────────────────────── │ │
│  │      - q DLQ │  │  Properties | Body (JSON/XML/Hex)    │ │
│  │    - NS2     │  │                                      │ │
│  └──────────────┘  └──────────────────────────────────────┘ │
│  [Status Bar: Connected | Messages: 42 | Peek mode]         │
└─────────────────────────────────────────────────────────────┘
```

- **Left panel:** Tree browser for navigation
- **Top-right panel:** Message list with sortable columns
- **Bottom-right panel:** Message details (properties + body tabs)
- **Status bar:** Connection status, message count, current identity

## Tree Navigation

### Hierarchy

```
⭐ Favorites
  └── my-namespace/orders-queue
  └── my-namespace/payments-queue (DLQ)

📁 Azure Subscriptions
  └── Production (sub-id-1234)
        └── rg-messaging
              └── sb-prod-namespace
                    ├── orders-queue
                    ├── orders-queue (DLQ)
                    ├── payments-topic
                    │     ├── subscription-1
                    │     ├── subscription-1 (DLQ)
                    │     └── subscription-2
                    └── notifications-queue
```

- Dead-letter queues appear as separate entries with "(DLQ)" suffix
- Favorites section at top for quick access
- Lazy loading: subscriptions and namespaces loaded on expand

### Keyboard Navigation

| Key | Action |
|-----|--------|
| Arrow keys | Navigate tree |
| Enter | Select and load messages |
| `f` | Toggle favorite |
| `r` | Refresh current node |
| `/` | Filter/search within tree |

### Mouse Support

- Click to select nodes
- Double-click to expand/collapse
- Scroll wheel navigation
- Right-click context menu for favorites

## Message List

### Columns

| Column | Description |
|--------|-------------|
| MessageId | Truncated with full ID on hover |
| Enqueued | Relative time ("2m ago") |
| Size | Human-readable (1.2KB) |
| DeliveryCount | Number of delivery attempts |
| ContentType | MIME type if set |

- Columns resizable and sortable
- Loads 50 messages at a time with "Load more" pagination
- Keyboard: `j/k` or arrows to navigate

## Message Detail View

### Properties Tab

Table showing all message properties:
- System properties: MessageId, CorrelationId, SessionId, EnqueuedTime, ScheduledEnqueueTime, DeliveryCount, etc.
- Custom properties: Full dictionary of application properties

### Body Tab

Auto-format detection:

1. Attempt UTF-8 decode
   - Success → Try JSON parse → Pretty-print with syntax highlighting
   - Success → Try XML parse → Formatted XML
   - Success → Plain text display
   - Failure → Hex dump with ASCII sidebar

Features:
- Line numbers
- Word wrap toggle (`w`)
- Copy to clipboard (`c`)
- Search within body (`/`)

### Edge Cases

| Scenario | Handling |
|----------|----------|
| Empty body | Show "(empty)" placeholder |
| Binary/non-UTF8 | Hex dump view |
| Large message (>50KB) | Truncate with "Show full" button |
| Locked message | Show "locked" indicator in list |

## Error Handling

### Authentication

- No valid credential: "Run `az login` to authenticate"
- Status bar shows current identity

### Permissions

- Discovery denied: Show "Access denied" with required role hint
- Peek denied: Clear message about Service Bus Data Reader role
- Partial access: Show accessible items, grey out denied ones

### Network/Transient

- Retry with exponential backoff (3 attempts)
- Spinner during operations
- Inline error with "Retry" option
- Favorites load even when offline, showing connection errors per item

### Queue Issues

- Empty queue: "No messages" with refresh timestamp
- Deleted queue: Remove from tree, prompt to remove from favorites

## Project Structure

```
AsbExplorer/
├── AsbExplorer.sln
├── src/
│   └── AsbExplorer/
│       ├── Program.cs                 # Entry point, DI setup
│       ├── App.cs                     # Main Terminal.Gui application
│       ├── Views/
│       │   ├── MainWindow.cs          # Shell layout
│       │   ├── TreePanel.cs           # Left panel
│       │   ├── MessageListView.cs     # Top-right
│       │   └── MessageDetailView.cs   # Bottom-right
│       ├── Services/
│       │   ├── AzureDiscoveryService.cs
│       │   ├── MessagePeekService.cs
│       │   ├── MessageFormatter.cs
│       │   └── FavoritesStore.cs
│       └── Models/
│           ├── TreeNode.cs
│           ├── PeekedMessage.cs
│           └── Favorite.cs
└── Directory.Packages.props           # CPM for package versions
```

## Configuration

- **Favorites:** `~/.config/asb-explorer/favorites.json`
- **No config file required:** Uses DefaultAzureCredential defaults
- **Future:** Optional config for subscription filters, themes

## Dependencies

Managed via Central Package Management (Directory.Packages.props):

- Terminal.Gui (2.x)
- Azure.Identity
- Azure.ResourceManager.ServiceBus
- Azure.Messaging.ServiceBus
- Microsoft.Extensions.DependencyInjection

## Out of Scope (YAGNI)

- Message modification (complete, abandon, dead-letter)
- Sending messages
- Bulk operations (purge, resubmit)
- Multiple authentication methods (connection strings)
- Themes/customization
- Export functionality
