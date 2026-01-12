# Azure Service Bus Explorer TUI - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Terminal.Gui TUI for debugging Azure Service Bus queues and subscriptions with peek-only operations.

**Architecture:** Two-panel layout with tree navigation (left) and message list/detail split (right). Uses Azure Resource Manager for discovery and Service Bus SDK for message operations. DefaultAzureCredential for authentication.

**Tech Stack:** .NET 10, Terminal.Gui 2.x, Azure.Identity, Azure.ResourceManager.ServiceBus, Azure.Messaging.ServiceBus

---

## Task 1: Project Scaffolding

**Files:**
- Create: `src/AsbExplorer/AsbExplorer.csproj`
- Create: `src/AsbExplorer/Program.cs`
- Create: `AsbExplorer.sln`
- Create: `Directory.Packages.props`

**Step 1: Create solution and project structure**

```bash
cd /Users/jonas/repos/puma/labs/queue-exlorer-cc/.worktrees/asb-explorer
mkdir -p src/AsbExplorer
dotnet new sln -n AsbExplorer
dotnet new console -n AsbExplorer -o src/AsbExplorer -f net10.0
dotnet sln add src/AsbExplorer/AsbExplorer.csproj
```

**Step 2: Create Directory.Packages.props for CPM**

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Terminal.Gui" Version="2.0.0" />
    <PackageVersion Include="Azure.Identity" Version="1.13.2" />
    <PackageVersion Include="Azure.ResourceManager.ServiceBus" Version="1.1.0" />
    <PackageVersion Include="Azure.Messaging.ServiceBus" Version="7.18.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
  </ItemGroup>
</Project>
```

**Step 3: Update csproj with package references**

Replace `src/AsbExplorer/AsbExplorer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
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

**Step 4: Create minimal Program.cs**

Replace `src/AsbExplorer/Program.cs`:

```csharp
using Terminal.Gui;

Application.Init();

try
{
    var window = new Window("Azure Service Bus Explorer")
    {
        X = 0,
        Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill()
    };

    var label = new Label("Press Ctrl+Q to quit")
    {
        X = Pos.Center(),
        Y = Pos.Center()
    };

    window.Add(label);
    Application.Top.Add(window);
    Application.Run();
}
finally
{
    Application.Shutdown();
}
```

**Step 5: Build and verify**

```bash
dotnet restore
dotnet build
```

Expected: Build succeeded.

**Step 6: Run quick smoke test**

```bash
dotnet run --project src/AsbExplorer &
sleep 2
kill %1 2>/dev/null || true
```

Expected: App starts without crash.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: scaffold .NET 10 project with Terminal.Gui"
```

---

## Task 2: Models

**Files:**
- Create: `src/AsbExplorer/Models/TreeNodeType.cs`
- Create: `src/AsbExplorer/Models/TreeNodeModel.cs`
- Create: `src/AsbExplorer/Models/PeekedMessage.cs`
- Create: `src/AsbExplorer/Models/Favorite.cs`

**Step 1: Create TreeNodeType enum**

Create `src/AsbExplorer/Models/TreeNodeType.cs`:

```csharp
namespace AsbExplorer.Models;

public enum TreeNodeType
{
    FavoritesRoot,
    Favorite,
    SubscriptionsRoot,
    Subscription,
    ResourceGroup,
    Namespace,
    Queue,
    QueueDeadLetter,
    Topic,
    TopicSubscription,
    TopicSubscriptionDeadLetter
}
```

**Step 2: Create TreeNodeModel**

Create `src/AsbExplorer/Models/TreeNodeModel.cs`:

```csharp
namespace AsbExplorer.Models;

public record TreeNodeModel(
    string Id,
    string DisplayName,
    TreeNodeType NodeType,
    string? SubscriptionId = null,
    string? ResourceGroupName = null,
    string? NamespaceName = null,
    string? NamespaceFqdn = null,
    string? EntityPath = null,
    string? ParentEntityPath = null
)
{
    public bool CanHaveChildren => NodeType is
        TreeNodeType.FavoritesRoot or
        TreeNodeType.SubscriptionsRoot or
        TreeNodeType.Subscription or
        TreeNodeType.ResourceGroup or
        TreeNodeType.Namespace or
        TreeNodeType.Topic;

    public bool CanPeekMessages => NodeType is
        TreeNodeType.Queue or
        TreeNodeType.QueueDeadLetter or
        TreeNodeType.TopicSubscription or
        TreeNodeType.TopicSubscriptionDeadLetter or
        TreeNodeType.Favorite;
}
```

**Step 3: Create PeekedMessage**

Create `src/AsbExplorer/Models/PeekedMessage.cs`:

```csharp
namespace AsbExplorer.Models;

public record PeekedMessage(
    string MessageId,
    long SequenceNumber,
    DateTimeOffset EnqueuedTime,
    int DeliveryCount,
    string? ContentType,
    string? CorrelationId,
    string? SessionId,
    TimeSpan TimeToLive,
    DateTimeOffset? ScheduledEnqueueTime,
    IReadOnlyDictionary<string, object> ApplicationProperties,
    BinaryData Body
)
{
    public long BodySizeBytes => Body.ToMemory().Length;
}
```

**Step 4: Create Favorite**

Create `src/AsbExplorer/Models/Favorite.cs`:

```csharp
namespace AsbExplorer.Models;

public record Favorite(
    string NamespaceFqdn,
    string EntityPath,
    TreeNodeType EntityType,
    string? ParentEntityPath = null
)
{
    public string DisplayName => ParentEntityPath is null
        ? $"{NamespaceFqdn}/{EntityPath}"
        : $"{NamespaceFqdn}/{ParentEntityPath}/{EntityPath}";
}
```

**Step 5: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add domain models for tree, messages, and favorites"
```

---

## Task 3: MessageFormatter Service

**Files:**
- Create: `src/AsbExplorer/Services/MessageFormatter.cs`

**Step 1: Create MessageFormatter**

Create `src/AsbExplorer/Services/MessageFormatter.cs`:

```csharp
using System.Text;
using System.Text.Json;
using System.Xml;

namespace AsbExplorer.Services;

public class MessageFormatter
{
    public (string Content, string Format) Format(BinaryData body, string? contentType)
    {
        // Try UTF-8 string first
        string? text = TryGetUtf8String(body);

        if (text is null)
        {
            return (FormatAsHex(body), "hex");
        }

        // Try JSON
        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true ||
            TryFormatJson(text, out var json))
        {
            return (json ?? text, "json");
        }

        // Try XML
        if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true ||
            TryFormatXml(text, out var xml))
        {
            return (xml ?? text, "xml");
        }

        return (text, "text");
    }

    private static string? TryGetUtf8String(BinaryData body)
    {
        try
        {
            var bytes = body.ToArray();
            var text = Encoding.UTF8.GetString(bytes);

            // Check for invalid UTF-8 sequences (replacement char)
            if (text.Contains('\uFFFD'))
            {
                return null;
            }

            return text;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryFormatJson(string text, out string? formatted)
    {
        formatted = null;
        var trimmed = text.TrimStart();

        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            formatted = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFormatXml(string text, out string? formatted)
    {
        formatted = null;
        var trimmed = text.TrimStart();

        if (!trimmed.StartsWith('<'))
        {
            return false;
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(text);

            using var sw = new StringWriter();
            using var xw = new XmlTextWriter(sw)
            {
                Formatting = System.Xml.Formatting.Indented,
                Indentation = 2
            };
            doc.WriteTo(xw);
            formatted = sw.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatAsHex(BinaryData body)
    {
        var bytes = body.ToArray();
        var sb = new StringBuilder();
        const int bytesPerLine = 16;

        for (int i = 0; i < bytes.Length; i += bytesPerLine)
        {
            // Offset
            sb.Append($"{i:X8}  ");

            // Hex bytes
            for (int j = 0; j < bytesPerLine; j++)
            {
                if (i + j < bytes.Length)
                {
                    sb.Append($"{bytes[i + j]:X2} ");
                }
                else
                {
                    sb.Append("   ");
                }

                if (j == 7) sb.Append(' ');
            }

            sb.Append(" |");

            // ASCII
            for (int j = 0; j < bytesPerLine && i + j < bytes.Length; j++)
            {
                var b = bytes[i + j];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }

            sb.AppendLine("|");
        }

        return sb.ToString();
    }
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MessageFormatter with JSON/XML/hex support"
```

---

## Task 4: FavoritesStore Service

**Files:**
- Create: `src/AsbExplorer/Services/FavoritesStore.cs`

**Step 1: Create FavoritesStore**

Create `src/AsbExplorer/Services/FavoritesStore.cs`:

```csharp
using System.Text.Json;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class FavoritesStore
{
    private readonly string _filePath;
    private List<Favorite> _favorites = [];

    public FavoritesStore()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "asb-explorer"
        );

        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "favorites.json");
    }

    public IReadOnlyList<Favorite> Favorites => _favorites.AsReadOnly();

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _favorites = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _favorites = JsonSerializer.Deserialize<List<Favorite>>(json) ?? [];
        }
        catch
        {
            _favorites = [];
        }
    }

    public async Task AddAsync(Favorite favorite)
    {
        if (_favorites.Any(f =>
            f.NamespaceFqdn == favorite.NamespaceFqdn &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath))
        {
            return;
        }

        _favorites.Add(favorite);
        await SaveAsync();
    }

    public async Task RemoveAsync(Favorite favorite)
    {
        _favorites.RemoveAll(f =>
            f.NamespaceFqdn == favorite.NamespaceFqdn &&
            f.EntityPath == favorite.EntityPath &&
            f.ParentEntityPath == favorite.ParentEntityPath);

        await SaveAsync();
    }

    public bool IsFavorite(string namespaceFqdn, string entityPath, string? parentEntityPath)
    {
        return _favorites.Any(f =>
            f.NamespaceFqdn == namespaceFqdn &&
            f.EntityPath == entityPath &&
            f.ParentEntityPath == parentEntityPath);
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_filePath, json);
    }
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add FavoritesStore for persistent favorites"
```

---

## Task 5: AzureDiscoveryService

**Files:**
- Create: `src/AsbExplorer/Services/AzureDiscoveryService.cs`

**Step 1: Create AzureDiscoveryService**

Create `src/AsbExplorer/Services/AzureDiscoveryService.cs`:

```csharp
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class AzureDiscoveryService
{
    private readonly TokenCredential _credential;
    private readonly ArmClient _armClient;

    public AzureDiscoveryService()
    {
        _credential = new DefaultAzureCredential();
        _armClient = new ArmClient(_credential);
    }

    public TokenCredential Credential => _credential;

    public async Task<string?> GetCurrentUserAsync()
    {
        try
        {
            var context = new TokenRequestContext(["https://management.azure.com/.default"]);
            var token = await _credential.GetTokenAsync(context, default);

            // Decode JWT to get upn/email (simplified - just check if we can get a token)
            return token.Token.Length > 0 ? "Authenticated" : null;
        }
        catch
        {
            return null;
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetSubscriptionsAsync()
    {
        await foreach (var sub in _armClient.GetSubscriptions())
        {
            yield return new TreeNodeModel(
                Id: sub.Data.SubscriptionId,
                DisplayName: $"{sub.Data.DisplayName} ({sub.Data.SubscriptionId[..8]}...)",
                NodeType: TreeNodeType.Subscription,
                SubscriptionId: sub.Data.SubscriptionId
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetNamespacesAsync(string subscriptionId)
    {
        var sub = _armClient.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));

        await foreach (var ns in sub.GetServiceBusNamespacesAsync())
        {
            var resourceGroup = ns.Id.ResourceGroupName;
            var fqdn = $"{ns.Data.Name}.servicebus.windows.net";

            yield return new TreeNodeModel(
                Id: ns.Id.ToString(),
                DisplayName: ns.Data.Name,
                NodeType: TreeNodeType.Namespace,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: ns.Data.Name,
                NamespaceFqdn: fqdn
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetQueuesAsync(
        string subscriptionId,
        string resourceGroup,
        string namespaceName,
        string namespaceFqdn)
    {
        var nsId = ServiceBusNamespaceResource.CreateResourceIdentifier(
            subscriptionId, resourceGroup, namespaceName);
        var ns = _armClient.GetServiceBusNamespaceResource(nsId);

        await foreach (var queue in ns.GetServiceBusQueues())
        {
            // Main queue
            yield return new TreeNodeModel(
                Id: queue.Id.ToString(),
                DisplayName: queue.Data.Name,
                NodeType: TreeNodeType.Queue,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: queue.Data.Name
            );

            // Dead-letter queue
            yield return new TreeNodeModel(
                Id: $"{queue.Id}/$deadletterqueue",
                DisplayName: $"{queue.Data.Name} (DLQ)",
                NodeType: TreeNodeType.QueueDeadLetter,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: $"{queue.Data.Name}/$deadletterqueue"
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetTopicsAsync(
        string subscriptionId,
        string resourceGroup,
        string namespaceName,
        string namespaceFqdn)
    {
        var nsId = ServiceBusNamespaceResource.CreateResourceIdentifier(
            subscriptionId, resourceGroup, namespaceName);
        var ns = _armClient.GetServiceBusNamespaceResource(nsId);

        await foreach (var topic in ns.GetServiceBusTopics())
        {
            yield return new TreeNodeModel(
                Id: topic.Id.ToString(),
                DisplayName: topic.Data.Name,
                NodeType: TreeNodeType.Topic,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: topic.Data.Name
            );
        }
    }

    public async IAsyncEnumerable<TreeNodeModel> GetTopicSubscriptionsAsync(
        string subscriptionId,
        string resourceGroup,
        string namespaceName,
        string namespaceFqdn,
        string topicName)
    {
        var topicId = ServiceBusTopicResource.CreateResourceIdentifier(
            subscriptionId, resourceGroup, namespaceName, topicName);
        var topic = _armClient.GetServiceBusTopicResource(topicId);

        await foreach (var sub in topic.GetServiceBusSubscriptions())
        {
            // Main subscription
            yield return new TreeNodeModel(
                Id: sub.Id.ToString(),
                DisplayName: sub.Data.Name,
                NodeType: TreeNodeType.TopicSubscription,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: sub.Data.Name,
                ParentEntityPath: topicName
            );

            // Dead-letter queue
            yield return new TreeNodeModel(
                Id: $"{sub.Id}/$deadletterqueue",
                DisplayName: $"{sub.Data.Name} (DLQ)",
                NodeType: TreeNodeType.TopicSubscriptionDeadLetter,
                SubscriptionId: subscriptionId,
                ResourceGroupName: resourceGroup,
                NamespaceName: namespaceName,
                NamespaceFqdn: namespaceFqdn,
                EntityPath: $"{sub.Data.Name}/$deadletterqueue",
                ParentEntityPath: topicName
            );
        }
    }
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add AzureDiscoveryService for subscription/namespace discovery"
```

---

## Task 6: MessagePeekService

**Files:**
- Create: `src/AsbExplorer/Services/MessagePeekService.cs`

**Step 1: Create MessagePeekService**

Create `src/AsbExplorer/Services/MessagePeekService.cs`:

```csharp
using Azure.Core;
using Azure.Messaging.ServiceBus;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class MessagePeekService : IAsyncDisposable
{
    private readonly TokenCredential _credential;
    private ServiceBusClient? _client;
    private string? _currentNamespace;

    public MessagePeekService(TokenCredential credential)
    {
        _credential = credential;
    }

    public async Task<IReadOnlyList<PeekedMessage>> PeekMessagesAsync(
        string namespaceFqdn,
        string entityPath,
        string? topicName,
        bool isDeadLetter,
        int maxMessages = 50,
        long? fromSequenceNumber = null)
    {
        var client = GetOrCreateClient(namespaceFqdn);

        ServiceBusReceiver receiver;

        if (topicName is not null)
        {
            // Topic subscription
            var subName = isDeadLetter
                ? entityPath.Replace("/$deadletterqueue", "")
                : entityPath;

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = isDeadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            receiver = client.CreateReceiver(topicName, subName, options);
        }
        else
        {
            // Queue
            var queueName = isDeadLetter
                ? entityPath.Replace("/$deadletterqueue", "")
                : entityPath;

            var options = new ServiceBusReceiverOptions
            {
                SubQueue = isDeadLetter ? SubQueue.DeadLetter : SubQueue.None
            };

            receiver = client.CreateReceiver(queueName, options);
        }

        await using (receiver)
        {
            var messages = fromSequenceNumber.HasValue
                ? await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber.Value)
                : await receiver.PeekMessagesAsync(maxMessages);

            return messages.Select(m => new PeekedMessage(
                MessageId: m.MessageId,
                SequenceNumber: m.SequenceNumber,
                EnqueuedTime: m.EnqueuedTime,
                DeliveryCount: m.DeliveryCount,
                ContentType: m.ContentType,
                CorrelationId: m.CorrelationId,
                SessionId: m.SessionId,
                TimeToLive: m.TimeToLive,
                ScheduledEnqueueTime: m.ScheduledEnqueueTime == default
                    ? null
                    : m.ScheduledEnqueueTime,
                ApplicationProperties: m.ApplicationProperties,
                Body: m.Body
            )).ToList();
        }
    }

    private ServiceBusClient GetOrCreateClient(string namespaceFqdn)
    {
        if (_client is null || _currentNamespace != namespaceFqdn)
        {
            _client?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client = new ServiceBusClient(namespaceFqdn, _credential);
            _currentNamespace = namespaceFqdn;
        }

        return _client;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MessagePeekService for peeking messages"
```

---

## Task 7: TreePanel View

**Files:**
- Create: `src/AsbExplorer/Views/TreePanel.cs`

**Step 1: Create TreePanel**

Create `src/AsbExplorer/Views/TreePanel.cs`:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class TreePanel : FrameView
{
    private readonly TreeView<TreeNodeModel> _treeView;
    private readonly AzureDiscoveryService _discoveryService;
    private readonly FavoritesStore _favoritesStore;

    public event Action<TreeNodeModel>? NodeSelected;

    public TreePanel(AzureDiscoveryService discoveryService, FavoritesStore favoritesStore)
        : base("Explorer")
    {
        _discoveryService = discoveryService;
        _favoritesStore = favoritesStore;

        _treeView = new TreeView<TreeNodeModel>
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DelegateTreeBuilder<TreeNodeModel>(GetChildrenAsync)
        };

        _treeView.SelectionChanged += (s, e) =>
        {
            if (e.NewValue is not null)
            {
                NodeSelected?.Invoke(e.NewValue);
            }
        };

        Add(_treeView);
    }

    public async Task LoadRootNodesAsync()
    {
        var roots = new List<TreeNodeModel>
        {
            new(
                Id: "favorites",
                DisplayName: "⭐ Favorites",
                NodeType: TreeNodeType.FavoritesRoot
            ),
            new(
                Id: "subscriptions",
                DisplayName: "📁 Azure Subscriptions",
                NodeType: TreeNodeType.SubscriptionsRoot
            )
        };

        foreach (var root in roots)
        {
            _treeView.AddObject(root);
        }

        _treeView.SetNeedsDisplay();
    }

    private IEnumerable<TreeNodeModel> GetChildrenAsync(TreeNodeModel node)
    {
        return node.NodeType switch
        {
            TreeNodeType.FavoritesRoot => GetFavoriteNodes(),
            TreeNodeType.SubscriptionsRoot => GetSubscriptions(),
            TreeNodeType.Subscription => GetNamespaces(node),
            TreeNodeType.Namespace => GetQueuesAndTopics(node),
            TreeNodeType.Topic => GetTopicSubscriptions(node),
            _ => []
        };
    }

    private IEnumerable<TreeNodeModel> GetFavoriteNodes()
    {
        return _favoritesStore.Favorites.Select(f => new TreeNodeModel(
            Id: $"fav:{f.NamespaceFqdn}/{f.EntityPath}",
            DisplayName: f.DisplayName,
            NodeType: TreeNodeType.Favorite,
            NamespaceFqdn: f.NamespaceFqdn,
            EntityPath: f.EntityPath,
            ParentEntityPath: f.ParentEntityPath
        ));
    }

    private IEnumerable<TreeNodeModel> GetSubscriptions()
    {
        return _discoveryService.GetSubscriptionsAsync()
            .ToBlockingEnumerable();
    }

    private IEnumerable<TreeNodeModel> GetNamespaces(TreeNodeModel sub)
    {
        return _discoveryService.GetNamespacesAsync(sub.SubscriptionId!)
            .ToBlockingEnumerable();
    }

    private IEnumerable<TreeNodeModel> GetQueuesAndTopics(TreeNodeModel ns)
    {
        var queues = _discoveryService.GetQueuesAsync(
            ns.SubscriptionId!,
            ns.ResourceGroupName!,
            ns.NamespaceName!,
            ns.NamespaceFqdn!
        ).ToBlockingEnumerable();

        var topics = _discoveryService.GetTopicsAsync(
            ns.SubscriptionId!,
            ns.ResourceGroupName!,
            ns.NamespaceName!,
            ns.NamespaceFqdn!
        ).ToBlockingEnumerable();

        return queues.Concat(topics);
    }

    private IEnumerable<TreeNodeModel> GetTopicSubscriptions(TreeNodeModel topic)
    {
        return _discoveryService.GetTopicSubscriptionsAsync(
            topic.SubscriptionId!,
            topic.ResourceGroupName!,
            topic.NamespaceName!,
            topic.NamespaceFqdn!,
            topic.EntityPath!
        ).ToBlockingEnumerable();
    }
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add TreePanel view for navigation"
```

---

## Task 8: MessageListView

**Files:**
- Create: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Create MessageListView**

Create `src/AsbExplorer/Views/MessageListView.cs`:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;

namespace AsbExplorer.Views;

public class MessageListView : FrameView
{
    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private IReadOnlyList<PeekedMessage> _messages = [];

    public event Action<PeekedMessage>? MessageSelected;

    public MessageListView() : base("Messages")
    {
        _dataTable = new DataTable();
        _dataTable.Columns.Add("MessageId", typeof(string));
        _dataTable.Columns.Add("Enqueued", typeof(string));
        _dataTable.Columns.Add("Size", typeof(string));
        _dataTable.Columns.Add("Delivery", typeof(int));
        _dataTable.Columns.Add("ContentType", typeof(string));

        _tableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_dataTable),
            FullRowSelect = true
        };

        _tableView.CellActivated += (s, e) =>
        {
            if (e.Row >= 0 && e.Row < _messages.Count)
            {
                MessageSelected?.Invoke(_messages[e.Row]);
            }
        };

        _tableView.SelectedCellChanged += (s, e) =>
        {
            if (e.NewRow >= 0 && e.NewRow < _messages.Count)
            {
                MessageSelected?.Invoke(_messages[e.NewRow]);
            }
        };

        Add(_tableView);
    }

    public void SetMessages(IReadOnlyList<PeekedMessage> messages)
    {
        _messages = messages;
        _dataTable.Rows.Clear();

        foreach (var msg in messages)
        {
            _dataTable.Rows.Add(
                TruncateId(msg.MessageId),
                FormatRelativeTime(msg.EnqueuedTime),
                FormatSize(msg.BodySizeBytes),
                msg.DeliveryCount,
                msg.ContentType ?? "-"
            );
        }

        _tableView.Table = new DataTableSource(_dataTable);
        _tableView.SetNeedsDisplay();
    }

    public void Clear()
    {
        _messages = [];
        _dataTable.Rows.Clear();
        _tableView.Table = new DataTableSource(_dataTable);
        _tableView.SetNeedsDisplay();
    }

    private static string TruncateId(string id)
    {
        return id.Length > 12 ? $"{id[..12]}..." : id;
    }

    private static string FormatRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;

        return diff.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)diff.TotalMinutes}m ago",
            < 1440 => $"{(int)diff.TotalHours}h ago",
            _ => $"{(int)diff.TotalDays}d ago"
        };
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1}MB"
        };
    }
}

// Simple DataTable implementation for Terminal.Gui
public class DataTable
{
    public List<DataColumn> Columns { get; } = [];
    public List<DataRow> Rows { get; } = [];

    public class DataColumn
    {
        public string Name { get; set; } = "";
        public Type Type { get; set; } = typeof(string);
    }

    public class DataRow
    {
        public object[] Values { get; set; } = [];
    }
}

public static class DataTableExtensions
{
    public static void Add(this List<DataTable.DataColumn> columns, string name, Type type)
    {
        columns.Add(new DataTable.DataColumn { Name = name, Type = type });
    }

    public static void Add(this List<DataTable.DataRow> rows, params object[] values)
    {
        rows.Add(new DataTable.DataRow { Values = values });
    }
}

public class DataTableSource : ITableSource
{
    private readonly DataTable _table;

    public DataTableSource(DataTable table)
    {
        _table = table;
    }

    public int Rows => _table.Rows.Count;
    public int Columns => _table.Columns.Count;

    public object this[int row, int col] => _table.Rows[row].Values[col];

    public string GetColumnName(int col) => _table.Columns[col].Name;
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MessageListView with table display"
```

---

## Task 9: MessageDetailView

**Files:**
- Create: `src/AsbExplorer/Views/MessageDetailView.cs`

**Step 1: Create MessageDetailView**

Create `src/AsbExplorer/Views/MessageDetailView.cs`:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class MessageDetailView : FrameView
{
    private readonly TabView _tabView;
    private readonly TableView _propertiesTable;
    private readonly TextView _bodyView;
    private readonly MessageFormatter _formatter;
    private readonly DataTable _propsDataTable;

    public MessageDetailView(MessageFormatter formatter) : base("Details")
    {
        _formatter = formatter;

        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Properties tab
        _propsDataTable = new DataTable();
        _propsDataTable.Columns.Add("Property", typeof(string));
        _propsDataTable.Columns.Add("Value", typeof(string));

        _propertiesTable = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new DataTableSource(_propsDataTable),
            FullRowSelect = true
        };

        var propsTab = new TabView.Tab("Properties", _propertiesTable);

        // Body tab
        _bodyView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        var bodyTab = new TabView.Tab("Body", _bodyView);

        _tabView.AddTab(propsTab, true);
        _tabView.AddTab(bodyTab, false);

        Add(_tabView);
    }

    public void SetMessage(PeekedMessage message)
    {
        // Properties
        _propsDataTable.Rows.Clear();

        _propsDataTable.Rows.Add("MessageId", message.MessageId);
        _propsDataTable.Rows.Add("SequenceNumber", message.SequenceNumber.ToString());
        _propsDataTable.Rows.Add("EnqueuedTime", message.EnqueuedTime.ToString("O"));
        _propsDataTable.Rows.Add("DeliveryCount", message.DeliveryCount.ToString());
        _propsDataTable.Rows.Add("ContentType", message.ContentType ?? "-");
        _propsDataTable.Rows.Add("CorrelationId", message.CorrelationId ?? "-");
        _propsDataTable.Rows.Add("SessionId", message.SessionId ?? "-");
        _propsDataTable.Rows.Add("TimeToLive", message.TimeToLive.ToString());

        if (message.ScheduledEnqueueTime.HasValue)
        {
            _propsDataTable.Rows.Add("ScheduledEnqueueTime",
                message.ScheduledEnqueueTime.Value.ToString("O"));
        }

        _propsDataTable.Rows.Add("BodySize", $"{message.BodySizeBytes} bytes");
        _propsDataTable.Rows.Add("", ""); // Separator

        foreach (var prop in message.ApplicationProperties)
        {
            _propsDataTable.Rows.Add($"[App] {prop.Key}", prop.Value?.ToString() ?? "null");
        }

        _propertiesTable.Table = new DataTableSource(_propsDataTable);
        _propertiesTable.SetNeedsDisplay();

        // Body
        var (content, format) = _formatter.Format(message.Body, message.ContentType);
        _bodyView.Text = $"[{format.ToUpper()}]\n\n{content}";
        _bodyView.SetNeedsDisplay();
    }

    public void Clear()
    {
        _propsDataTable.Rows.Clear();
        _propertiesTable.Table = new DataTableSource(_propsDataTable);
        _propertiesTable.SetNeedsDisplay();

        _bodyView.Text = "";
        _bodyView.SetNeedsDisplay();
    }
}
```

**Step 2: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MessageDetailView with properties and body tabs"
```

---

## Task 10: MainWindow Integration

**Files:**
- Create: `src/AsbExplorer/Views/MainWindow.cs`
- Modify: `src/AsbExplorer/Program.cs`

**Step 1: Create MainWindow**

Create `src/AsbExplorer/Views/MainWindow.cs`:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class MainWindow : Window
{
    private readonly TreePanel _treePanel;
    private readonly MessageListView _messageList;
    private readonly MessageDetailView _messageDetail;
    private readonly StatusBar _statusBar;
    private readonly AzureDiscoveryService _discoveryService;
    private readonly MessagePeekService _peekService;
    private readonly FavoritesStore _favoritesStore;
    private readonly MessageFormatter _formatter;

    private TreeNodeModel? _currentNode;

    public MainWindow(
        AzureDiscoveryService discoveryService,
        MessagePeekService peekService,
        FavoritesStore favoritesStore,
        MessageFormatter formatter) : base("Azure Service Bus Explorer")
    {
        _discoveryService = discoveryService;
        _peekService = peekService;
        _favoritesStore = favoritesStore;
        _formatter = formatter;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill() - 1; // Leave room for status bar

        // Left panel - Tree (30% width)
        _treePanel = new TreePanel(_discoveryService, _favoritesStore)
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(30),
            Height = Dim.Fill()
        };

        // Right panel container
        var rightPanel = new View
        {
            X = Pos.Right(_treePanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Message list (top 40% of right panel)
        _messageList = new MessageListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(40)
        };

        // Message detail (bottom 60% of right panel)
        _messageDetail = new MessageDetailView(_formatter)
        {
            X = 0,
            Y = Pos.Bottom(_messageList),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        rightPanel.Add(_messageList, _messageDetail);
        Add(_treePanel, rightPanel);

        // Status bar
        _statusBar = new StatusBar([
            new Shortcut(Key.Q.WithCtrl, "Quit", () => Application.RequestStop()),
            new Shortcut(Key.R, "Refresh", RefreshCurrentNode),
            new Shortcut(Key.F, "Toggle Favorite", ToggleFavorite)
        ]);

        // Wire up events
        _treePanel.NodeSelected += OnNodeSelected;
        _messageList.MessageSelected += OnMessageSelected;
    }

    public StatusBar StatusBar => _statusBar;

    public async Task InitializeAsync()
    {
        await _favoritesStore.LoadAsync();
        await _treePanel.LoadRootNodesAsync();
    }

    private async void OnNodeSelected(TreeNodeModel node)
    {
        _currentNode = node;
        _messageList.Clear();
        _messageDetail.Clear();

        if (!node.CanPeekMessages || node.NamespaceFqdn is null)
        {
            return;
        }

        try
        {
            var isDeadLetter = node.NodeType is
                TreeNodeType.QueueDeadLetter or
                TreeNodeType.TopicSubscriptionDeadLetter;

            var messages = await _peekService.PeekMessagesAsync(
                node.NamespaceFqdn,
                node.EntityPath!,
                node.ParentEntityPath,
                isDeadLetter
            );

            _messageList.SetMessages(messages);
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to peek messages: {ex.Message}", "OK");
        }
    }

    private void OnMessageSelected(PeekedMessage message)
    {
        _messageDetail.SetMessage(message);
    }

    private async void RefreshCurrentNode()
    {
        if (_currentNode is not null)
        {
            OnNodeSelected(_currentNode);
        }
    }

    private async void ToggleFavorite()
    {
        if (_currentNode is null ||
            !_currentNode.CanPeekMessages ||
            _currentNode.NamespaceFqdn is null)
        {
            return;
        }

        var entityType = _currentNode.NodeType switch
        {
            TreeNodeType.Favorite => TreeNodeType.Queue, // Best guess
            _ => _currentNode.NodeType
        };

        var favorite = new Favorite(
            _currentNode.NamespaceFqdn,
            _currentNode.EntityPath!,
            entityType,
            _currentNode.ParentEntityPath
        );

        if (_favoritesStore.IsFavorite(
            _currentNode.NamespaceFqdn,
            _currentNode.EntityPath!,
            _currentNode.ParentEntityPath))
        {
            await _favoritesStore.RemoveAsync(favorite);
            MessageBox.Query("Favorites", "Removed from favorites", "OK");
        }
        else
        {
            await _favoritesStore.AddAsync(favorite);
            MessageBox.Query("Favorites", "Added to favorites", "OK");
        }
    }
}
```

**Step 2: Update Program.cs**

Replace `src/AsbExplorer/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
using AsbExplorer.Services;
using AsbExplorer.Views;

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<AzureDiscoveryService>();
services.AddSingleton<FavoritesStore>();
services.AddSingleton<MessageFormatter>();
services.AddSingleton(sp =>
    new MessagePeekService(sp.GetRequiredService<AzureDiscoveryService>().Credential));
services.AddSingleton<MainWindow>();

var provider = services.BuildServiceProvider();

Application.Init();

try
{
    var mainWindow = provider.GetRequiredService<MainWindow>();

    Application.Top.Add(mainWindow);
    Application.Top.Add(mainWindow.StatusBar);

    // Initialize async data
    await mainWindow.InitializeAsync();

    Application.Run();
}
finally
{
    Application.Shutdown();

    if (provider is IAsyncDisposable asyncDisposable)
    {
        await asyncDisposable.DisposeAsync();
    }
}
```

**Step 3: Build to verify**

```bash
dotnet build
```

Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: integrate all views in MainWindow with DI"
```

---

## Task 11: Final Verification

**Step 1: Full build**

```bash
dotnet build --configuration Release
```

Expected: Build succeeded.

**Step 2: Smoke test (manual)**

```bash
dotnet run --project src/AsbExplorer
```

Expected: TUI launches, shows tree with Favorites and Azure Subscriptions nodes.

**Step 3: Commit release-ready state**

```bash
git add -A
git commit -m "chore: verify release build" --allow-empty
```

---

## Summary

After completing all tasks:
- Solution builds with `dotnet build`
- TUI launches with tree navigation
- Azure subscriptions discoverable via DefaultAzureCredential
- Messages can be peeked from queues/subscriptions
- Favorites persist to `~/.config/asb-explorer/favorites.json`
