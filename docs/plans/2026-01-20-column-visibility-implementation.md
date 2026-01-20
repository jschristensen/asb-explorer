# Column Visibility Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow users to show/hide and reorder columns in the message list, with per-entity settings that persist across sessions.

**Architecture:** New `ColumnConfigService` handles column logic (testable), `ApplicationPropertyScanner` extracts property keys from messages. Settings stored per-entity in `AppSettings.EntityColumns`. Right-click on table header opens `ColumnConfigDialog`.

**Tech Stack:** .NET 10, Terminal.Gui, xUnit, System.Text.Json

---

## Task 1: Create ColumnConfig Model

**Files:**
- Create: `src/AsbExplorer/Models/ColumnConfig.cs`

**Step 1: Create the model file**

```csharp
namespace AsbExplorer.Models;

public record ColumnConfig(string Name, bool Visible, bool IsApplicationProperty = false);
```

**Step 2: Build to verify**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/AsbExplorer/Models/ColumnConfig.cs
git commit -m "feat: add ColumnConfig model"
```

---

## Task 2: Create EntityColumnSettings Model

**Files:**
- Create: `src/AsbExplorer/Models/EntityColumnSettings.cs`

**Step 1: Create the model file**

```csharp
namespace AsbExplorer.Models;

public class EntityColumnSettings
{
    public List<ColumnConfig> Columns { get; set; } = [];
    public HashSet<string> DiscoveredProperties { get; set; } = [];
}
```

**Step 2: Build to verify**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/AsbExplorer/Models/EntityColumnSettings.cs
git commit -m "feat: add EntityColumnSettings model"
```

---

## Task 3: Add ColumnConfigService with GetDefaultColumns (TDD)

**Files:**
- Create: `src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs`
- Create: `src/AsbExplorer/Services/ColumnConfigService.cs`

**Step 1: Write the failing test**

```csharp
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class ColumnConfigServiceTests
{
    private readonly ColumnConfigService _service = new();

    [Fact]
    public void GetDefaultColumns_ReturnsAllCoreColumns()
    {
        var columns = _service.GetDefaultColumns();

        Assert.Equal(11, columns.Count);
        Assert.Equal("SequenceNumber", columns[0].Name);
        Assert.Equal("MessageId", columns[1].Name);
        Assert.Equal("Enqueued", columns[2].Name);
        Assert.Equal("Subject", columns[3].Name);
        Assert.Equal("Size", columns[4].Name);
        Assert.Equal("DeliveryCount", columns[5].Name);
        Assert.Equal("ContentType", columns[6].Name);
        Assert.Equal("CorrelationId", columns[7].Name);
        Assert.Equal("SessionId", columns[8].Name);
        Assert.Equal("TimeToLive", columns[9].Name);
        Assert.Equal("ScheduledEnqueue", columns[10].Name);
    }

    [Fact]
    public void GetDefaultColumns_FirstSevenVisible_LastFourHidden()
    {
        var columns = _service.GetDefaultColumns();

        // First 7 visible
        Assert.True(columns[0].Visible); // SequenceNumber
        Assert.True(columns[1].Visible); // MessageId
        Assert.True(columns[2].Visible); // Enqueued
        Assert.True(columns[3].Visible); // Subject
        Assert.True(columns[4].Visible); // Size
        Assert.True(columns[5].Visible); // DeliveryCount
        Assert.True(columns[6].Visible); // ContentType

        // Last 4 hidden
        Assert.False(columns[7].Visible); // CorrelationId
        Assert.False(columns[8].Visible); // SessionId
        Assert.False(columns[9].Visible); // TimeToLive
        Assert.False(columns[10].Visible); // ScheduledEnqueue
    }

    [Fact]
    public void GetDefaultColumns_NoneAreApplicationProperties()
    {
        var columns = _service.GetDefaultColumns();

        Assert.All(columns, c => Assert.False(c.IsApplicationProperty));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "ColumnConfigServiceTests"`
Expected: FAIL - ColumnConfigService does not exist

**Step 3: Write minimal implementation**

```csharp
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class ColumnConfigService
{
    private static readonly List<(string Name, bool DefaultVisible)> CoreColumns =
    [
        ("SequenceNumber", true),
        ("MessageId", true),
        ("Enqueued", true),
        ("Subject", true),
        ("Size", true),
        ("DeliveryCount", true),
        ("ContentType", true),
        ("CorrelationId", false),
        ("SessionId", false),
        ("TimeToLive", false),
        ("ScheduledEnqueue", false)
    ];

    public List<ColumnConfig> GetDefaultColumns()
    {
        return CoreColumns
            .Select(c => new ColumnConfig(c.Name, c.DefaultVisible))
            .ToList();
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "ColumnConfigServiceTests"`
Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/ColumnConfigService.cs src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs
git commit -m "feat: add ColumnConfigService with GetDefaultColumns"
```

---

## Task 4: Add MergeDiscoveredProperties to ColumnConfigService (TDD)

**Files:**
- Modify: `src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs`
- Modify: `src/AsbExplorer/Services/ColumnConfigService.cs`

**Step 1: Write the failing tests**

Add to `ColumnConfigServiceTests.cs`:

```csharp
    [Fact]
    public void MergeDiscoveredProperties_AddsNewKeysAsHidden()
    {
        var settings = new EntityColumnSettings
        {
            Columns = _service.GetDefaultColumns(),
            DiscoveredProperties = []
        };

        _service.MergeDiscoveredProperties(settings, ["OrderId", "CustomerId"]);

        Assert.Contains(settings.DiscoveredProperties, p => p == "OrderId");
        Assert.Contains(settings.DiscoveredProperties, p => p == "CustomerId");

        var orderIdCol = settings.Columns.Single(c => c.Name == "OrderId");
        Assert.False(orderIdCol.Visible);
        Assert.True(orderIdCol.IsApplicationProperty);
    }

    [Fact]
    public void MergeDiscoveredProperties_PreservesExistingVisibility()
    {
        var settings = new EntityColumnSettings
        {
            Columns =
            [
                .._service.GetDefaultColumns(),
                new ColumnConfig("OrderId", true, true)
            ],
            DiscoveredProperties = ["OrderId"]
        };

        _service.MergeDiscoveredProperties(settings, ["OrderId", "CustomerId"]);

        var orderIdCol = settings.Columns.Single(c => c.Name == "OrderId");
        Assert.True(orderIdCol.Visible); // Preserved
    }

    [Fact]
    public void MergeDiscoveredProperties_PreservesColumnOrder()
    {
        var settings = new EntityColumnSettings
        {
            Columns =
            [
                .._service.GetDefaultColumns(),
                new ColumnConfig("ExistingProp", true, true)
            ],
            DiscoveredProperties = ["ExistingProp"]
        };

        _service.MergeDiscoveredProperties(settings, ["NewProp"]);

        var existingIndex = settings.Columns.FindIndex(c => c.Name == "ExistingProp");
        var newIndex = settings.Columns.FindIndex(c => c.Name == "NewProp");
        Assert.True(newIndex > existingIndex); // New props added at end
    }
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "MergeDiscoveredProperties"`
Expected: FAIL - MergeDiscoveredProperties does not exist

**Step 3: Write minimal implementation**

Add to `ColumnConfigService.cs`:

```csharp
    public void MergeDiscoveredProperties(EntityColumnSettings settings, IEnumerable<string> newKeys)
    {
        foreach (var key in newKeys)
        {
            if (settings.DiscoveredProperties.Add(key))
            {
                // New property - add as hidden column at end
                settings.Columns.Add(new ColumnConfig(key, false, true));
            }
        }
    }
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "MergeDiscoveredProperties"`
Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/ColumnConfigService.cs src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs
git commit -m "feat: add MergeDiscoveredProperties to ColumnConfigService"
```

---

## Task 5: Add ValidateConfig to ColumnConfigService (TDD)

**Files:**
- Modify: `src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs`
- Modify: `src/AsbExplorer/Services/ColumnConfigService.cs`

**Step 1: Write the failing tests**

Add to `ColumnConfigServiceTests.cs`:

```csharp
    [Fact]
    public void ValidateConfig_RequiresAtLeastOneVisible()
    {
        var columns = new List<ColumnConfig>
        {
            new("SequenceNumber", false),
            new("MessageId", false)
        };

        var (isValid, error) = _service.ValidateConfig(columns);

        Assert.False(isValid);
        Assert.Contains("at least one", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateConfig_SequenceNumberMustBeFirst()
    {
        var columns = new List<ColumnConfig>
        {
            new("MessageId", true),
            new("SequenceNumber", true)
        };

        var (isValid, error) = _service.ValidateConfig(columns);

        Assert.False(isValid);
        Assert.Contains("SequenceNumber", error);
    }

    [Fact]
    public void ValidateConfig_SequenceNumberMustBeVisible()
    {
        var columns = new List<ColumnConfig>
        {
            new("SequenceNumber", false),
            new("MessageId", true)
        };

        var (isValid, error) = _service.ValidateConfig(columns);

        Assert.False(isValid);
        Assert.Contains("SequenceNumber", error);
    }

    [Fact]
    public void ValidateConfig_ValidConfigReturnsTrue()
    {
        var columns = _service.GetDefaultColumns();

        var (isValid, error) = _service.ValidateConfig(columns);

        Assert.True(isValid);
        Assert.Null(error);
    }
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "ValidateConfig"`
Expected: FAIL - ValidateConfig does not exist

**Step 3: Write minimal implementation**

Add to `ColumnConfigService.cs`:

```csharp
    public (bool IsValid, string? Error) ValidateConfig(List<ColumnConfig> columns)
    {
        if (!columns.Any(c => c.Visible))
        {
            return (false, "At least one column must be visible.");
        }

        var seqNumIndex = columns.FindIndex(c => c.Name == "SequenceNumber");
        if (seqNumIndex != 0)
        {
            return (false, "SequenceNumber must be the first column.");
        }

        if (!columns[0].Visible)
        {
            return (false, "SequenceNumber must be visible.");
        }

        return (true, null);
    }
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "ValidateConfig"`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/ColumnConfigService.cs src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs
git commit -m "feat: add ValidateConfig to ColumnConfigService"
```

---

## Task 6: Add GetVisibleColumns to ColumnConfigService (TDD)

**Files:**
- Modify: `src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs`
- Modify: `src/AsbExplorer/Services/ColumnConfigService.cs`

**Step 1: Write the failing tests**

Add to `ColumnConfigServiceTests.cs`:

```csharp
    [Fact]
    public void GetVisibleColumns_FiltersHiddenColumns()
    {
        var columns = new List<ColumnConfig>
        {
            new("SequenceNumber", true),
            new("MessageId", false),
            new("Subject", true)
        };

        var visible = _service.GetVisibleColumns(columns);

        Assert.Equal(2, visible.Count);
        Assert.Equal("SequenceNumber", visible[0].Name);
        Assert.Equal("Subject", visible[1].Name);
    }

    [Fact]
    public void GetVisibleColumns_PreservesOrder()
    {
        var columns = new List<ColumnConfig>
        {
            new("SequenceNumber", true),
            new("Subject", true),
            new("MessageId", true)
        };

        var visible = _service.GetVisibleColumns(columns);

        Assert.Equal("SequenceNumber", visible[0].Name);
        Assert.Equal("Subject", visible[1].Name);
        Assert.Equal("MessageId", visible[2].Name);
    }
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "GetVisibleColumns"`
Expected: FAIL - GetVisibleColumns does not exist

**Step 3: Write minimal implementation**

Add to `ColumnConfigService.cs`:

```csharp
    public List<ColumnConfig> GetVisibleColumns(List<ColumnConfig> columns)
    {
        return columns.Where(c => c.Visible).ToList();
    }
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "GetVisibleColumns"`
Expected: PASS (2 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/ColumnConfigService.cs src/AsbExplorer.Tests/Services/ColumnConfigServiceTests.cs
git commit -m "feat: add GetVisibleColumns to ColumnConfigService"
```

---

## Task 7: Add ApplicationPropertyScanner (TDD)

**Files:**
- Create: `src/AsbExplorer.Tests/Services/ApplicationPropertyScannerTests.cs`
- Create: `src/AsbExplorer/Services/ApplicationPropertyScanner.cs`

**Step 1: Write the failing tests**

```csharp
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class ApplicationPropertyScannerTests
{
    private readonly ApplicationPropertyScanner _scanner = new();

    private static PeekedMessage CreateMessage(Dictionary<string, object>? props = null)
    {
        return new PeekedMessage(
            MessageId: Guid.NewGuid().ToString(),
            SequenceNumber: 1,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: null,
            DeliveryCount: 1,
            ContentType: null,
            CorrelationId: null,
            SessionId: null,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: props ?? new Dictionary<string, object>(),
            Body: BinaryData.FromString("{}")
        );
    }

    [Fact]
    public void ScanMessages_ReturnsDistinctKeys()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(new Dictionary<string, object> { ["OrderId"] = "123", ["CustomerId"] = "456" }),
            CreateMessage(new Dictionary<string, object> { ["OrderId"] = "789", ["Status"] = "pending" })
        };

        var keys = _scanner.ScanMessages(messages);

        Assert.Equal(3, keys.Count);
        Assert.Contains("OrderId", keys);
        Assert.Contains("CustomerId", keys);
        Assert.Contains("Status", keys);
    }

    [Fact]
    public void ScanMessages_RespectsLimit()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(new Dictionary<string, object> { ["Prop1"] = "1" }),
            CreateMessage(new Dictionary<string, object> { ["Prop2"] = "2" }),
            CreateMessage(new Dictionary<string, object> { ["Prop3"] = "3" })
        };

        var keys = _scanner.ScanMessages(messages, limit: 2);

        Assert.Equal(2, keys.Count);
        Assert.Contains("Prop1", keys);
        Assert.Contains("Prop2", keys);
        Assert.DoesNotContain("Prop3", keys);
    }

    [Fact]
    public void ScanMessages_HandlesEmptyMessages()
    {
        var keys = _scanner.ScanMessages([]);

        Assert.Empty(keys);
    }

    [Fact]
    public void ScanMessages_HandlesNullProperties()
    {
        var messages = new List<PeekedMessage>
        {
            CreateMessage(null),
            CreateMessage(new Dictionary<string, object> { ["Valid"] = "value" })
        };

        var keys = _scanner.ScanMessages(messages);

        Assert.Single(keys);
        Assert.Contains("Valid", keys);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "ApplicationPropertyScannerTests"`
Expected: FAIL - ApplicationPropertyScanner does not exist

**Step 3: Write minimal implementation**

```csharp
using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class ApplicationPropertyScanner
{
    public IReadOnlySet<string> ScanMessages(IEnumerable<PeekedMessage> messages, int limit = 20)
    {
        return messages
            .Take(limit)
            .SelectMany(m => m.ApplicationProperties?.Keys ?? [])
            .ToHashSet();
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "ApplicationPropertyScannerTests"`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/ApplicationPropertyScanner.cs src/AsbExplorer.Tests/Services/ApplicationPropertyScannerTests.cs
git commit -m "feat: add ApplicationPropertyScanner"
```

---

## Task 8: Extend AppSettings with EntityColumns

**Files:**
- Modify: `src/AsbExplorer/Models/AppSettings.cs`
- Modify: `src/AsbExplorer/Services/JsonContext.cs`

**Step 1: Update AppSettings**

```csharp
namespace AsbExplorer.Models;

public class AppSettings
{
    public string Theme { get; set; } = "dark";
    public bool AutoRefreshTreeCounts { get; set; } = false;
    public bool AutoRefreshMessageList { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 10;
    public Dictionary<string, EntityColumnSettings> EntityColumns { get; set; } = [];
}
```

**Step 2: Update JsonContext for AOT serialization**

```csharp
using System.Text.Json.Serialization;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

[JsonSerializable(typeof(List<ServiceBusConnection>))]
[JsonSerializable(typeof(List<Favorite>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(EntityColumnSettings))]
[JsonSerializable(typeof(ColumnConfig))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
```

**Step 3: Build to verify**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 4: Run all tests to verify nothing broke**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/AsbExplorer/Models/AppSettings.cs src/AsbExplorer/Services/JsonContext.cs
git commit -m "feat: extend AppSettings with EntityColumns"
```

---

## Task 9: Add Entity Column Methods to SettingsStore (TDD)

**Files:**
- Modify: `src/AsbExplorer.Tests/Services/SettingsStoreTests.cs`
- Modify: `src/AsbExplorer/Services/SettingsStore.cs`

**Step 1: Write the failing tests**

Add to `SettingsStoreTests.cs`:

```csharp
    [Fact]
    public async Task GetEntityColumns_NoEntry_ReturnsNull()
    {
        await _store.LoadAsync();

        var result = _store.GetEntityColumns("mybus.servicebus.windows.net", "orders");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveEntityColumnsAsync_SavesAndPersists()
    {
        await _store.LoadAsync();
        var settings = new EntityColumnSettings
        {
            Columns = [new ColumnConfig("SequenceNumber", true)],
            DiscoveredProperties = ["OrderId"]
        };

        await _store.SaveEntityColumnsAsync("mybus.servicebus.windows.net", "orders", settings);

        var newStore = new SettingsStore(_tempDir);
        await newStore.LoadAsync();
        var loaded = newStore.GetEntityColumns("mybus.servicebus.windows.net", "orders");

        Assert.NotNull(loaded);
        Assert.Single(loaded.Columns);
        Assert.Contains("OrderId", loaded.DiscoveredProperties);
    }

    [Fact]
    public async Task GetEntityColumns_UsesCorrectKeyFormat()
    {
        await _store.LoadAsync();
        var settings = new EntityColumnSettings { Columns = [] };

        await _store.SaveEntityColumnsAsync("ns.servicebus.windows.net", "topic/subscriptions/sub", settings);

        var loaded = _store.GetEntityColumns("ns.servicebus.windows.net", "topic/subscriptions/sub");
        Assert.NotNull(loaded);

        // Different entity should return null
        var other = _store.GetEntityColumns("ns.servicebus.windows.net", "other");
        Assert.Null(other);
    }
```

Add using at top: `using AsbExplorer.Models;`

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "EntityColumns"`
Expected: FAIL - GetEntityColumns does not exist

**Step 3: Write minimal implementation**

Add to `SettingsStore.cs`:

```csharp
    private static string GetEntityKey(string @namespace, string entityPath)
        => $"{@namespace}|{entityPath}";

    public EntityColumnSettings? GetEntityColumns(string @namespace, string entityPath)
    {
        var key = GetEntityKey(@namespace, entityPath);
        return Settings.EntityColumns.TryGetValue(key, out var settings) ? settings : null;
    }

    public async Task SaveEntityColumnsAsync(string @namespace, string entityPath, EntityColumnSettings settings)
    {
        var key = GetEntityKey(@namespace, entityPath);
        Settings.EntityColumns[key] = settings;
        await SaveAsync();
    }
```

Add using at top: `using AsbExplorer.Models;` (if not present)

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "EntityColumns"`
Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/SettingsStore.cs src/AsbExplorer.Tests/Services/SettingsStoreTests.cs
git commit -m "feat: add entity column methods to SettingsStore"
```

---

## Task 10: Add DisplayHelpers for New Column Types (TDD)

**Files:**
- Modify: `src/AsbExplorer.Tests/Helpers/DisplayHelpersTests.cs`
- Modify: `src/AsbExplorer/Helpers/DisplayHelpers.cs`

**Step 1: Write the failing tests**

Add to `DisplayHelpersTests.cs`:

```csharp
    [Theory]
    [InlineData(30, "30s")]
    [InlineData(90, "1m 30s")]
    [InlineData(3600, "1h")]
    [InlineData(3661, "1h 1m")]
    [InlineData(86400, "1d")]
    [InlineData(90061, "1d 1h")]
    public void FormatTimeSpan_FormatsCorrectly(int totalSeconds, string expected)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        Assert.Equal(expected, DisplayHelpers.FormatTimeSpan(ts));
    }

    [Fact]
    public void FormatScheduledTime_Null_ReturnsDash()
    {
        Assert.Equal("-", DisplayHelpers.FormatScheduledTime(null));
    }

    [Fact]
    public void FormatScheduledTime_Future_ReturnsRelative()
    {
        var future = DateTimeOffset.UtcNow.AddMinutes(5);
        var result = DisplayHelpers.FormatScheduledTime(future);
        Assert.Contains("in", result);
    }

    [Fact]
    public void FormatScheduledTime_Past_ReturnsRelative()
    {
        var past = DateTimeOffset.UtcNow.AddMinutes(-5);
        var result = DisplayHelpers.FormatScheduledTime(past);
        Assert.Contains("ago", result);
    }
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FormatTimeSpan or FormatScheduledTime"`
Expected: FAIL - methods do not exist

**Step 3: Write minimal implementation**

Add to `DisplayHelpers.cs`:

```csharp
    public static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
        {
            var days = (int)ts.TotalDays;
            var hours = ts.Hours;
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }
        if (ts.TotalHours >= 1)
        {
            var hours = (int)ts.TotalHours;
            var minutes = ts.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }
        if (ts.TotalMinutes >= 1)
        {
            var minutes = (int)ts.TotalMinutes;
            var seconds = ts.Seconds;
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }
        return $"{(int)ts.TotalSeconds}s";
    }

    public static string FormatScheduledTime(DateTimeOffset? time)
    {
        if (!time.HasValue)
            return "-";

        var diff = time.Value - DateTimeOffset.UtcNow;
        if (diff.TotalSeconds > 0)
        {
            // Future
            return diff.TotalMinutes switch
            {
                < 1 => "in <1m",
                < 60 => $"in {(int)diff.TotalMinutes}m",
                < 1440 => $"in {(int)diff.TotalHours}h",
                _ => $"in {(int)diff.TotalDays}d"
            };
        }
        else
        {
            // Past - use existing FormatRelativeTime
            return FormatRelativeTime(time.Value);
        }
    }
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FormatTimeSpan or FormatScheduledTime"`
Expected: PASS (9 tests)

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/DisplayHelpers.cs src/AsbExplorer.Tests/Helpers/DisplayHelpersTests.cs
git commit -m "feat: add FormatTimeSpan and FormatScheduledTime helpers"
```

---

## Task 11: Create ColumnConfigDialog

**Files:**
- Create: `src/AsbExplorer/Views/ColumnConfigDialog.cs`

**Step 1: Create the dialog**

```csharp
using Terminal.Gui;
using AsbExplorer.Models;
using AsbExplorer.Services;

namespace AsbExplorer.Views;

public class ColumnConfigDialog : Dialog
{
    private readonly ListView _listView;
    private readonly List<ColumnConfig> _columns;
    private readonly ColumnConfigService _configService;
    private readonly Label _errorLabel;

    public List<ColumnConfig>? Result { get; private set; }

    public ColumnConfigDialog(List<ColumnConfig> columns, ColumnConfigService configService)
    {
        Title = "Configure Columns";
        Width = 45;
        Height = 20;

        _columns = columns.Select(c => c with { }).ToList(); // Clone
        _configService = configService;

        var upButton = new Button { Text = "Up", X = 1, Y = 1 };
        var downButton = new Button { Text = "Down", X = Pos.Right(upButton) + 1, Y = 1 };

        _listView = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
            AllowsMarking = true,
            AllowsMultipleSelection = false
        };

        UpdateListView();

        _listView.OpenSelectedItem += (s, e) => ToggleSelected();

        upButton.Accepting += (s, e) => MoveUp();
        downButton.Accepting += (s, e) => MoveDown();

        _errorLabel = new Label
        {
            X = 1,
            Y = Pos.Bottom(_listView),
            Width = Dim.Fill(1),
            ColorScheme = Colors.ColorSchemes["Error"]
        };

        var applyButton = new Button { Text = "Apply", X = Pos.Center() - 10, Y = Pos.AnchorEnd(1) };
        var cancelButton = new Button { Text = "Cancel", X = Pos.Center() + 2, Y = Pos.AnchorEnd(1) };

        applyButton.Accepting += (s, e) =>
        {
            var (isValid, error) = _configService.ValidateConfig(_columns);
            if (!isValid)
            {
                _errorLabel.Text = error;
                return;
            }
            Result = _columns;
            Application.RequestStop();
        };

        cancelButton.Accepting += (s, e) => Application.RequestStop();

        Add(upButton, downButton, _listView, _errorLabel, applyButton, cancelButton);
    }

    private void UpdateListView()
    {
        var items = _columns.Select(c =>
        {
            var marker = c.Visible ? "[x]" : "[ ]";
            var suffix = c.IsApplicationProperty ? " (app)" : "";
            return $"{marker} {c.Name}{suffix}";
        }).ToList();

        _listView.SetSource(items);

        // Set marks based on visibility
        for (var i = 0; i < _columns.Count; i++)
        {
            _listView.Source.SetMark(i, _columns[i].Visible);
        }
    }

    private void ToggleSelected()
    {
        var idx = _listView.SelectedItem;
        if (idx < 0 || idx >= _columns.Count)
            return;

        // Don't allow hiding SequenceNumber
        if (_columns[idx].Name == "SequenceNumber")
            return;

        _columns[idx] = _columns[idx] with { Visible = !_columns[idx].Visible };
        _errorLabel.Text = "";
        UpdateListView();
        _listView.SelectedItem = idx;
    }

    private void MoveUp()
    {
        var idx = _listView.SelectedItem;
        // Can't move first item or SequenceNumber
        if (idx <= 1)
            return;

        (_columns[idx], _columns[idx - 1]) = (_columns[idx - 1], _columns[idx]);
        UpdateListView();
        _listView.SelectedItem = idx - 1;
    }

    private void MoveDown()
    {
        var idx = _listView.SelectedItem;
        // Can't move last item or SequenceNumber (index 0)
        if (idx < 1 || idx >= _columns.Count - 1)
            return;

        (_columns[idx], _columns[idx + 1]) = (_columns[idx + 1], _columns[idx]);
        UpdateListView();
        _listView.SelectedItem = idx + 1;
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/AsbExplorer/Views/ColumnConfigDialog.cs
git commit -m "feat: add ColumnConfigDialog for column visibility UI"
```

---

## Task 12: Integrate Column Config into MessageListView

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

This is a larger task. The key changes:

1. Add dependencies for `SettingsStore`, `ColumnConfigService`, `ApplicationPropertyScanner`
2. Track current entity namespace and path
3. Modify `SetMessages` to use column configuration
4. Add right-click handler on header to open dialog

**Step 1: Update MessageListView constructor and fields**

Add fields after line 23:

```csharp
    private readonly SettingsStore _settingsStore;
    private readonly ColumnConfigService _columnConfigService;
    private readonly ApplicationPropertyScanner _propertyScanner;
    private string? _currentNamespace;
    private string? _currentEntityPath;
    private EntityColumnSettings? _currentColumnSettings;
```

Update constructor signature:

```csharp
    public MessageListView(SettingsStore settingsStore, ColumnConfigService columnConfigService, ApplicationPropertyScanner propertyScanner)
```

Initialize fields in constructor:

```csharp
        _settingsStore = settingsStore;
        _columnConfigService = columnConfigService;
        _propertyScanner = propertyScanner;
```

**Step 2: Add SetEntity method**

Add after `SetEntityName`:

```csharp
    public void SetEntity(string? @namespace, string? entityPath, string? displayName)
    {
        // Clear selection when switching to a different entity
        if (@namespace != _currentNamespace || entityPath != _currentEntityPath)
        {
            _selectedSequenceNumbers.Clear();
            RefreshCheckboxDisplay();
            UpdateRequeueButtonVisibility();
        }

        _currentNamespace = @namespace;
        _currentEntityPath = entityPath;
        _currentEntityName = displayName;
        Title = string.IsNullOrEmpty(displayName) ? "Messages" : $"Messages: {displayName}";

        // Load column settings for this entity
        _currentColumnSettings = @namespace != null && entityPath != null
            ? _settingsStore.GetEntityColumns(@namespace, entityPath)
            : null;

        _currentColumnSettings ??= new EntityColumnSettings
        {
            Columns = _columnConfigService.GetDefaultColumns(),
            DiscoveredProperties = []
        };
    }
```

**Step 3: Update SetMessages to use column configuration**

Replace the `SetMessages` method (lines 401-463) with the new implementation that respects column settings. This is complex - see the full replacement in implementation.

**Step 4: Add header right-click handler**

In constructor, after the existing `_tableView.MouseClick` handler (around line 194), add:

```csharp
        // Right-click on header to configure columns
        _tableView.MouseClick += (s, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.Button3Clicked))
            {
                var cell = _tableView.ScreenToCell(e.Position, out int? headerCol);
                if (headerCol.HasValue)
                {
                    ShowColumnConfigDialog();
                    e.Handled = true;
                }
            }
        };
```

**Step 5: Add ShowColumnConfigDialog method**

```csharp
    private void ShowColumnConfigDialog()
    {
        if (_currentColumnSettings == null || _currentNamespace == null || _currentEntityPath == null)
            return;

        // Discover new properties from current messages
        var newProps = _propertyScanner.ScanMessages(_messages);
        _columnConfigService.MergeDiscoveredProperties(_currentColumnSettings, newProps);

        var dialog = new ColumnConfigDialog(
            _currentColumnSettings.Columns,
            _columnConfigService
        );

        Application.Run(dialog);

        if (dialog.Result != null)
        {
            _currentColumnSettings.Columns = dialog.Result;
            _ = _settingsStore.SaveEntityColumnsAsync(_currentNamespace, _currentEntityPath, _currentColumnSettings);
            SetMessages(_messages); // Refresh display
        }
    }
```

**Step 6: Build and run tests**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build errors (MainWindow needs to pass new deps)

**Step 7: Commit partial progress**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "wip: integrate column config into MessageListView"
```

---

## Task 13: Update MainWindow and Program.cs for DI

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`
- Modify: `src/AsbExplorer/Program.cs`

**Step 1: Register new services in Program.cs**

Add after existing service registrations:

```csharp
services.AddSingleton<ColumnConfigService>();
services.AddSingleton<ApplicationPropertyScanner>();
```

**Step 2: Update MainWindow constructor**

Update to receive new dependencies and pass to MessageListView.

**Step 3: Update MessageListView instantiation in MainWindow**

Pass the new dependencies when creating MessageListView.

**Step 4: Build to verify**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 5: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`
Expected: All tests pass

**Step 6: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs src/AsbExplorer/Program.cs
git commit -m "feat: wire up column config DI in MainWindow and Program"
```

---

## Task 14: Update MainWindow to Pass Entity Info

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Update the tree selection handler**

When a queue/topic/subscription is selected, call `SetEntity` with namespace and path instead of just `SetEntityName`.

Need to extract namespace from connection string. Add helper method or use existing connection info.

**Step 2: Build and test manually**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

Test manually by running the app and right-clicking on the message list header.

**Step 3: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "feat: pass entity namespace and path to MessageListView"
```

---

## Task 15: Final Integration and Testing

**Files:**
- All modified files

**Step 1: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`
Expected: All tests pass (should be ~120+ tests now)

**Step 2: Manual testing checklist**

- [ ] Right-click on table header opens column config dialog
- [ ] Toggling visibility updates the table
- [ ] Reordering columns updates the table
- [ ] SequenceNumber cannot be hidden or moved
- [ ] Settings persist after restarting the app
- [ ] Different entities maintain separate settings
- [ ] DLQ mode still works with custom column config
- [ ] New columns (CorrelationId, SessionId, etc.) can be shown

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat: complete column visibility configuration (closes #17)"
```

---

## Summary

| Task | Component | Tests Added |
|------|-----------|-------------|
| 1-2 | Data models | 0 |
| 3-6 | ColumnConfigService | 12 |
| 7 | ApplicationPropertyScanner | 4 |
| 8 | AppSettings extension | 0 |
| 9 | SettingsStore extension | 3 |
| 10 | DisplayHelpers extension | 9 |
| 11 | ColumnConfigDialog | 0 (UI) |
| 12-14 | Integration | 0 (manual) |
| 15 | Final testing | 0 |

**Total new tests:** ~28
