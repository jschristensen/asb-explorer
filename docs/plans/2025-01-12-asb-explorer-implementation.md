# Azure Service Bus Explorer TUI - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Terminal.Gui TUI for debugging Azure Service Bus queues and subscriptions with peek-only operations.

**Architecture:** Two-panel layout with tree navigation (left) and message list/detail split (right). Uses Azure Resource Manager for discovery and Service Bus SDK for message operations. DefaultAzureCredential for authentication.

**Tech Stack:** .NET 10, Terminal.Gui 2.x, Azure.Identity, Azure.ResourceManager.ServiceBus, Azure.Messaging.ServiceBus, xUnit

---

## TDD Approach

Starting from Task 7, all new code follows **Test-Driven Development**:

1. **RED:** Write a failing test that describes the desired behavior
2. **Verify RED:** Run test, confirm it fails for the right reason
3. **GREEN:** Write minimal code to make the test pass
4. **Verify GREEN:** Run test, confirm it passes
5. **REFACTOR:** Clean up while keeping tests green
6. **Commit:** Commit working code with tests

**Test project:** `src/AsbExplorer.Tests/` (xUnit)

**Run tests:**
```bash
dotnet test
```

**Testability strategy for Views:**
- Extract pure logic (formatting, calculations) into testable helper classes
- Views remain thin wrappers around Terminal.Gui components
- Test the extracted logic, not the UI wiring

---

## Completed Tasks (Pre-TDD)

### Task 1: Project Scaffolding ✅
- Created solution, project, CPM (Directory.Packages.props)
- Minimal Terminal.Gui window with Ctrl+Q quit

### Task 2: Models ✅
- TreeNodeType, TreeNodeModel, PeekedMessage, Favorite

### Task 3: MessageFormatter Service ✅
- JSON/XML/hex formatting for message bodies

### Task 4: FavoritesStore Service ✅
- Persistent favorites to ~/.config/asb-explorer/favorites.json

### Task 5: AzureDiscoveryService ✅
- ARM-based discovery of subscriptions, namespaces, queues, topics

### Task 6: MessagePeekService ✅
- Peek messages from queues and topic subscriptions

---

## Task 7: Retroactive Tests for Services

Add tests for already-implemented services to establish baseline coverage.

**Files:**
- Create: `src/AsbExplorer.Tests/Services/MessageFormatterTests.cs`
- Create: `src/AsbExplorer.Tests/Services/FavoritesStoreTests.cs`
- Delete: `src/AsbExplorer.Tests/UnitTest1.cs` (template file)

### Step 1: Delete template test

```bash
rm src/AsbExplorer.Tests/UnitTest1.cs
```

### Step 2: Create MessageFormatterTests

Create `src/AsbExplorer.Tests/Services/MessageFormatterTests.cs`:

```csharp
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class MessageFormatterTests
{
    private readonly MessageFormatter _formatter = new();

    [Fact]
    public void Format_ValidJson_ReturnsPrettyPrintedJson()
    {
        var body = BinaryData.FromString("""{"name":"test","value":42}""");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\"name\": \"test\"", content);
        Assert.Contains("\"value\": 42", content);
    }

    [Fact]
    public void Format_ValidXml_ReturnsFormattedXml()
    {
        var body = BinaryData.FromString("<root><item>test</item></root>");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("xml", format);
        Assert.Contains("<root>", content);
        Assert.Contains("<item>test</item>", content);
    }

    [Fact]
    public void Format_PlainText_ReturnsTextFormat()
    {
        var body = BinaryData.FromString("Hello, World!");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("text", format);
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public void Format_BinaryData_ReturnsHexDump()
    {
        var bytes = new byte[] { 0x00, 0x01, 0xFF, 0xFE };
        var body = BinaryData.FromBytes(bytes);

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("hex", format);
        Assert.Contains("00 01 FF FE", content);
    }

    [Fact]
    public void Format_JsonContentType_TreatsAsJson()
    {
        var body = BinaryData.FromString("not valid json");

        var (content, format) = _formatter.Format(body, "application/json");

        Assert.Equal("json", format);
        Assert.Equal("not valid json", content);
    }

    [Fact]
    public void Format_XmlContentType_TreatsAsXml()
    {
        var body = BinaryData.FromString("not valid xml");

        var (content, format) = _formatter.Format(body, "application/xml");

        Assert.Equal("xml", format);
        Assert.Equal("not valid xml", content);
    }

    [Fact]
    public void Format_EmptyBody_ReturnsEmptyText()
    {
        var body = BinaryData.FromString("");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("text", format);
        Assert.Equal("", content);
    }

    [Fact]
    public void Format_JsonArray_ReturnsPrettyPrintedArray()
    {
        var body = BinaryData.FromString("[1,2,3]");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("1", content);
    }
}
```

### Step 3: Create FavoritesStoreTests

Create `src/AsbExplorer.Tests/Services/FavoritesStoreTests.cs`:

```csharp
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class FavoritesStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly FavoritesStore _store;

    public FavoritesStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"asb-explorer-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        // Use reflection or make FavoritesStore accept a path for testing
        _store = new FavoritesStoreForTesting(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsEmptyList()
    {
        await _store.LoadAsync();

        Assert.Empty(_store.Favorites);
    }

    [Fact]
    public async Task AddAsync_NewFavorite_AddsTofavorites()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);

        await _store.AddAsync(favorite);

        Assert.Single(_store.Favorites);
        Assert.Equal("queue1", _store.Favorites[0].EntityPath);
    }

    [Fact]
    public async Task AddAsync_DuplicateFavorite_DoesNotAddAgain()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);

        await _store.AddAsync(favorite);
        await _store.AddAsync(favorite);

        Assert.Single(_store.Favorites);
    }

    [Fact]
    public async Task RemoveAsync_ExistingFavorite_RemovesIt()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);
        await _store.AddAsync(favorite);

        await _store.RemoveAsync(favorite);

        Assert.Empty(_store.Favorites);
    }

    [Fact]
    public async Task IsFavorite_ExistingFavorite_ReturnsTrue()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);
        await _store.AddAsync(favorite);

        var result = _store.IsFavorite("ns.servicebus.windows.net", "queue1", null);

        Assert.True(result);
    }

    [Fact]
    public async Task IsFavorite_NonExistingFavorite_ReturnsFalse()
    {
        var result = _store.IsFavorite("ns.servicebus.windows.net", "queue1", null);

        Assert.False(result);
    }

    [Fact]
    public async Task Persistence_SaveAndLoad_RestoresFavorites()
    {
        var favorite = new Favorite("ns.servicebus.windows.net", "queue1", TreeNodeType.Queue);
        await _store.AddAsync(favorite);

        // Create new store instance pointing to same directory
        var store2 = new FavoritesStoreForTesting(_testDir);
        await store2.LoadAsync();

        Assert.Single(store2.Favorites);
        Assert.Equal("queue1", store2.Favorites[0].EntityPath);
    }
}

// Test helper that allows injecting config directory
internal class FavoritesStoreForTesting : FavoritesStore
{
    public FavoritesStoreForTesting(string configDir) : base(configDir) { }
}
```

### Step 4: Update FavoritesStore to support testing

Modify `src/AsbExplorer/Services/FavoritesStore.cs` to add a constructor that accepts a config directory:

```csharp
// Add this constructor after the existing one:
protected FavoritesStore(string configDir)
{
    Directory.CreateDirectory(configDir);
    _filePath = Path.Combine(configDir, "favorites.json");
}
```

### Step 5: Run tests

```bash
dotnet test
```

Expected: All tests pass.

### Step 6: Commit

```bash
git add -A
git commit -m "test: add retroactive tests for MessageFormatter and FavoritesStore"
```

---

## Task 8: Display Helpers (TDD)

Extract testable display logic from Views into helper classes.

**Files:**
- Create: `src/AsbExplorer.Tests/Helpers/DisplayHelpersTests.cs`
- Create: `src/AsbExplorer/Helpers/DisplayHelpers.cs`

### RED: Write failing tests

Create `src/AsbExplorer.Tests/Helpers/DisplayHelpersTests.cs`:

```csharp
using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class DisplayHelpersTests
{
    public class TruncateIdTests
    {
        [Fact]
        public void TruncateId_ShortId_ReturnsUnchanged()
        {
            var result = DisplayHelpers.TruncateId("short", 12);
            Assert.Equal("short", result);
        }

        [Fact]
        public void TruncateId_LongId_TruncatesWithEllipsis()
        {
            var result = DisplayHelpers.TruncateId("this-is-a-very-long-id", 12);
            Assert.Equal("this-is-a-ve...", result);
        }

        [Fact]
        public void TruncateId_ExactLength_ReturnsUnchanged()
        {
            var result = DisplayHelpers.TruncateId("exactly12chr", 12);
            Assert.Equal("exactly12chr", result);
        }
    }

    public class FormatRelativeTimeTests
    {
        [Fact]
        public void FormatRelativeTime_JustNow_ReturnsJustNow()
        {
            var time = DateTimeOffset.UtcNow.AddSeconds(-30);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("just now", result);
        }

        [Fact]
        public void FormatRelativeTime_MinutesAgo_ReturnsMinutes()
        {
            var time = DateTimeOffset.UtcNow.AddMinutes(-5);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("5m ago", result);
        }

        [Fact]
        public void FormatRelativeTime_HoursAgo_ReturnsHours()
        {
            var time = DateTimeOffset.UtcNow.AddHours(-3);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("3h ago", result);
        }

        [Fact]
        public void FormatRelativeTime_DaysAgo_ReturnsDays()
        {
            var time = DateTimeOffset.UtcNow.AddDays(-2);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("2d ago", result);
        }
    }

    public class FormatSizeTests
    {
        [Fact]
        public void FormatSize_Bytes_ReturnsBytes()
        {
            var result = DisplayHelpers.FormatSize(500);
            Assert.Equal("500B", result);
        }

        [Fact]
        public void FormatSize_Kilobytes_ReturnsKB()
        {
            var result = DisplayHelpers.FormatSize(2048);
            Assert.Equal("2.0KB", result);
        }

        [Fact]
        public void FormatSize_Megabytes_ReturnsMB()
        {
            var result = DisplayHelpers.FormatSize(2 * 1024 * 1024);
            Assert.Equal("2.0MB", result);
        }
    }
}
```

### Verify RED

```bash
dotnet test
```

Expected: Tests fail (DisplayHelpers class doesn't exist).

### GREEN: Implement DisplayHelpers

Create `src/AsbExplorer/Helpers/DisplayHelpers.cs`:

```csharp
namespace AsbExplorer.Helpers;

public static class DisplayHelpers
{
    public static string TruncateId(string id, int maxLength)
    {
        return id.Length > maxLength ? $"{id[..maxLength]}..." : id;
    }

    public static string FormatRelativeTime(DateTimeOffset time)
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

    public static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1}MB"
        };
    }
}
```

### Verify GREEN

```bash
dotnet test
```

Expected: All tests pass.

### Commit

```bash
git add -A
git commit -m "feat: add DisplayHelpers with TDD for formatting logic"
```

---

## Task 9: TreePanel View

**Files:**
- Create: `src/AsbExplorer/Views/TreePanel.cs`

Since TreePanel is tightly coupled to Terminal.Gui TreeView, we implement it directly (UI wiring is hard to unit test). The data fetching logic is already tested via AzureDiscoveryService.

### Step 1: Create TreePanel

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
    {
        Title = "Explorer";
        _discoveryService = discoveryService;
        _favoritesStore = favoritesStore;

        _treeView = new TreeView<TreeNodeModel>
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = new DelegateTreeBuilder<TreeNodeModel>(GetChildren)
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
                DisplayName: "Favorites",
                NodeType: TreeNodeType.FavoritesRoot
            ),
            new(
                Id: "subscriptions",
                DisplayName: "Azure Subscriptions",
                NodeType: TreeNodeType.SubscriptionsRoot
            )
        };

        foreach (var root in roots)
        {
            _treeView.AddObject(root);
        }

        _treeView.SetNeedsDisplay();
    }

    private IEnumerable<TreeNodeModel> GetChildren(TreeNodeModel node)
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
        return _discoveryService.GetSubscriptionsAsync().ToBlockingEnumerable();
    }

    private IEnumerable<TreeNodeModel> GetNamespaces(TreeNodeModel sub)
    {
        return _discoveryService.GetNamespacesAsync(sub.SubscriptionId!).ToBlockingEnumerable();
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

### Step 2: Build to verify

```bash
dotnet build
```

Expected: Build succeeded.

### Step 3: Commit

```bash
git add -A
git commit -m "feat: add TreePanel view for navigation"
```

---

## Task 10: MessageListView

**Files:**
- Create: `src/AsbExplorer/Views/MessageListView.cs`

Uses DisplayHelpers for formatting (already tested).

### Step 1: Create MessageListView

Create `src/AsbExplorer/Views/MessageListView.cs`:

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Helpers;

namespace AsbExplorer.Views;

public class MessageListView : FrameView
{
    private readonly TableView _tableView;
    private readonly DataTable _dataTable;
    private IReadOnlyList<PeekedMessage> _messages = [];

    public event Action<PeekedMessage>? MessageSelected;

    public MessageListView()
    {
        Title = "Messages";

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
                DisplayHelpers.TruncateId(msg.MessageId, 12),
                DisplayHelpers.FormatRelativeTime(msg.EnqueuedTime),
                DisplayHelpers.FormatSize(msg.BodySizeBytes),
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

### Step 2: Build and run tests

```bash
dotnet build && dotnet test
```

Expected: Build succeeded, all tests pass.

### Step 3: Commit

```bash
git add -A
git commit -m "feat: add MessageListView with table display"
```

---

## Task 11: MessageDetailView

**Files:**
- Create: `src/AsbExplorer/Views/MessageDetailView.cs`

### Step 1: Create MessageDetailView

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

    public MessageDetailView(MessageFormatter formatter)
    {
        Title = "Details";
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

### Step 2: Build and run tests

```bash
dotnet build && dotnet test
```

Expected: Build succeeded, all tests pass.

### Step 3: Commit

```bash
git add -A
git commit -m "feat: add MessageDetailView with properties and body tabs"
```

---

## Task 12: MainWindow Integration

**Files:**
- Create: `src/AsbExplorer/Views/MainWindow.cs`
- Modify: `src/AsbExplorer/Program.cs`

### Step 1: Create MainWindow

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
    private readonly MessagePeekService _peekService;
    private readonly FavoritesStore _favoritesStore;

    private TreeNodeModel? _currentNode;

    public MainWindow(
        AzureDiscoveryService discoveryService,
        MessagePeekService peekService,
        FavoritesStore favoritesStore,
        MessageFormatter formatter)
    {
        Title = "Azure Service Bus Explorer";
        _peekService = peekService;
        _favoritesStore = favoritesStore;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill() - 1;

        // Left panel - Tree (30% width)
        _treePanel = new TreePanel(discoveryService, favoritesStore)
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
        _messageDetail = new MessageDetailView(formatter)
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

    private void RefreshCurrentNode()
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
            TreeNodeType.Favorite => TreeNodeType.Queue,
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

### Step 2: Update Program.cs

Replace `src/AsbExplorer/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
using AsbExplorer.Services;
using AsbExplorer.Views;

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

    Application.Run(mainWindow);
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

### Step 3: Build and run tests

```bash
dotnet build && dotnet test
```

Expected: Build succeeded, all tests pass.

### Step 4: Commit

```bash
git add -A
git commit -m "feat: integrate all views in MainWindow with DI"
```

---

## Task 13: Final Verification

### Step 1: Full build and tests

```bash
dotnet build --configuration Release
dotnet test
```

Expected: Build succeeded, all tests pass.

### Step 2: Smoke test (manual)

```bash
dotnet run --project src/AsbExplorer
```

Expected: TUI launches, shows tree with Favorites and Azure Subscriptions nodes.

### Step 3: Commit

```bash
git add -A
git commit -m "chore: verify release build and tests pass"
```

---

## Summary

After completing all tasks:
- Solution builds with `dotnet build`
- All tests pass with `dotnet test`
- TUI launches with tree navigation
- Azure subscriptions discoverable via DefaultAzureCredential
- Messages can be peeked from queues/subscriptions
- Favorites persist to `~/.config/asb-explorer/favorites.json`

**Test coverage:**
- MessageFormatter: JSON, XML, hex, text formatting
- FavoritesStore: CRUD operations, persistence
- DisplayHelpers: ID truncation, relative time, size formatting
