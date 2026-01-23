# Export to SQLite Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Export Azure Service Bus messages to SQLite database files for analysis in external tools.

**Architecture:** Add MessageExportService for SQLite schema generation and data insertion. ExportDialog handles user column selection. Export button in MessageListView triggers the flow. Settings store remembers export directory.

**Tech Stack:** Microsoft.Data.Sqlite, Terminal.Gui dialogs, xUnit tests

---

## Task 1: Add Microsoft.Data.Sqlite Dependency

**Files:**
- Modify: `Directory.Packages.props:17` (before closing ItemGroup)
- Modify: `src/AsbExplorer/AsbExplorer.csproj:32` (before closing ItemGroup)

**Step 1: Add package version to Directory.Packages.props**

Add before the `</ItemGroup>` on line 17:

```xml
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="9.0.0" />
```

**Step 2: Add package reference to AsbExplorer.csproj**

Add before the `</ItemGroup>` on line 33:

```xml
    <PackageReference Include="Microsoft.Data.Sqlite" />
```

**Step 3: Restore packages**

Run: `dotnet restore src/AsbExplorer/AsbExplorer.csproj`
Expected: Success, no errors

**Step 4: Commit**

```bash
git add Directory.Packages.props src/AsbExplorer/AsbExplorer.csproj
git commit -m "$(cat <<'EOF'
chore: add Microsoft.Data.Sqlite dependency

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Create ExportColumnHelper for SQL Column Name Normalization

**Files:**
- Create: `src/AsbExplorer/Helpers/ExportColumnHelper.cs`
- Create: `src/AsbExplorer.Tests/Helpers/ExportColumnHelperTests.cs`

**Step 1: Write the failing test**

Create `src/AsbExplorer.Tests/Helpers/ExportColumnHelperTests.cs`:

```csharp
using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class ExportColumnHelperTests
{
    [Theory]
    [InlineData("CustomerId", "prop_customerid")]
    [InlineData("Customer Id", "prop_customer_id")]
    [InlineData("order-type", "prop_order_type")]
    [InlineData("123Invalid", "prop_123invalid")]
    [InlineData("has.dots", "prop_has_dots")]
    public void NormalizePropertyName_VariousInputs_ReturnsValidColumnName(string input, string expected)
    {
        var result = ExportColumnHelper.NormalizePropertyName(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizePropertyName_VeryLongName_TruncatesTo63Chars()
    {
        var longName = new string('a', 100);

        var result = ExportColumnHelper.NormalizePropertyName(longName);

        Assert.Equal(63, result.Length);
        Assert.StartsWith("prop_", result);
    }

    [Theory]
    [InlineData("SequenceNumber", "sequence_number")]
    [InlineData("MessageId", "message_id")]
    [InlineData("Enqueued", "enqueued_time")]
    [InlineData("Subject", "subject")]
    [InlineData("Size", "body_size_bytes")]
    [InlineData("DeliveryCount", "delivery_count")]
    [InlineData("ContentType", "content_type")]
    [InlineData("CorrelationId", "correlation_id")]
    [InlineData("SessionId", "session_id")]
    [InlineData("TimeToLive", "time_to_live_seconds")]
    [InlineData("ScheduledEnqueue", "scheduled_enqueue_time")]
    public void GetSqlColumnName_CoreColumns_ReturnsExpectedName(string displayName, string expected)
    {
        var result = ExportColumnHelper.GetSqlColumnName(displayName);

        Assert.Equal(expected, result);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~ExportColumnHelperTests"`
Expected: FAIL - ExportColumnHelper type not found

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Helpers/ExportColumnHelper.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace AsbExplorer.Helpers;

public static partial class ExportColumnHelper
{
    private static readonly Dictionary<string, string> CoreColumnMapping = new()
    {
        ["SequenceNumber"] = "sequence_number",
        ["MessageId"] = "message_id",
        ["Enqueued"] = "enqueued_time",
        ["Subject"] = "subject",
        ["Size"] = "body_size_bytes",
        ["DeliveryCount"] = "delivery_count",
        ["ContentType"] = "content_type",
        ["CorrelationId"] = "correlation_id",
        ["SessionId"] = "session_id",
        ["TimeToLive"] = "time_to_live_seconds",
        ["ScheduledEnqueue"] = "scheduled_enqueue_time"
    };

    public static string GetSqlColumnName(string displayName)
    {
        return CoreColumnMapping.TryGetValue(displayName, out var mapped)
            ? mapped
            : NormalizePropertyName(displayName);
    }

    public static string NormalizePropertyName(string propertyName)
    {
        // Replace non-alphanumeric with underscore, lowercase
        var normalized = InvalidCharsRegex().Replace(propertyName, "_").ToLowerInvariant();

        // Collapse multiple underscores
        normalized = MultipleUnderscoreRegex().Replace(normalized, "_");

        // Trim leading/trailing underscores
        normalized = normalized.Trim('_');

        // Add prefix and truncate (SQLite max identifier length is 128, but 63 is practical)
        var result = $"prop_{normalized}";
        if (result.Length > 63)
        {
            result = result[..63];
        }

        return result;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"_+")]
    private static partial Regex MultipleUnderscoreRegex();
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~ExportColumnHelperTests"`
Expected: PASS

**Step 5: Commit**

```bash
git add src/AsbExplorer/Helpers/ExportColumnHelper.cs src/AsbExplorer.Tests/Helpers/ExportColumnHelperTests.cs
git commit -m "$(cat <<'EOF'
feat: add ExportColumnHelper for SQL column name normalization

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Create MessageExportService Core Logic

**Files:**
- Create: `src/AsbExplorer/Services/MessageExportService.cs`
- Create: `src/AsbExplorer.Tests/Services/MessageExportServiceTests.cs`

**Step 1: Write the failing test for basic export**

Create `src/AsbExplorer.Tests/Services/MessageExportServiceTests.cs`:

```csharp
using AsbExplorer.Models;
using AsbExplorer.Services;
using Microsoft.Data.Sqlite;

namespace AsbExplorer.Tests.Services;

public class MessageExportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MessageExportService _service;

    public MessageExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"export-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _service = new MessageExportService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static PeekedMessage CreateMessage(
        long sequenceNumber = 1,
        string? messageId = null,
        string? body = "{}",
        Dictionary<string, object>? appProps = null)
    {
        return new PeekedMessage(
            MessageId: messageId ?? Guid.NewGuid().ToString(),
            SequenceNumber: sequenceNumber,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: "Test Subject",
            DeliveryCount: 1,
            ContentType: "application/json",
            CorrelationId: "corr-123",
            SessionId: null,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: appProps ?? new Dictionary<string, object>(),
            Body: BinaryData.FromString(body ?? "{}")
        );
    }

    [Fact]
    public async Task ExportAsync_SingleMessage_CreatesValidDatabase()
    {
        var messages = new[] { CreateMessage(sequenceNumber: 42) };
        var columns = new[] { "SequenceNumber", "MessageId", "Subject" };
        var filePath = Path.Combine(_tempDir, "test.db");

        await _service.ExportAsync(messages, columns, filePath);

        Assert.True(File.Exists(filePath));

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        // Verify table exists
        var tableCmd = connection.CreateCommand();
        tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='messages'";
        var tableName = await tableCmd.ExecuteScalarAsync();
        Assert.Equal("messages", tableName);

        // Verify row count
        var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM messages";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);

        // Verify sequence number
        var seqCmd = connection.CreateCommand();
        seqCmd.CommandText = "SELECT sequence_number FROM messages";
        var seqNum = (long)(await seqCmd.ExecuteScalarAsync())!;
        Assert.Equal(42, seqNum);
    }

    [Fact]
    public async Task ExportAsync_WithBody_IncludesBodyAndEncoding()
    {
        var messages = new[] { CreateMessage(body: """{"test": "value"}""") };
        var columns = new[] { "SequenceNumber" };
        var filePath = Path.Combine(_tempDir, "body.db");

        await _service.ExportAsync(messages, columns, filePath);

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT body, body_encoding FROM messages";
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.Contains("test", reader.GetString(0));
        Assert.Equal("text", reader.GetString(1));
    }

    [Fact]
    public async Task ExportAsync_BinaryBody_Base64Encodes()
    {
        var binaryData = new byte[] { 0x00, 0x01, 0xFF, 0xFE };
        var msg = new PeekedMessage(
            MessageId: "binary-msg",
            SequenceNumber: 1,
            EnqueuedTime: DateTimeOffset.UtcNow,
            Subject: null,
            DeliveryCount: 1,
            ContentType: null,
            CorrelationId: null,
            SessionId: null,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: null,
            ApplicationProperties: new Dictionary<string, object>(),
            Body: BinaryData.FromBytes(binaryData)
        );
        var filePath = Path.Combine(_tempDir, "binary.db");

        await _service.ExportAsync(new[] { msg }, new[] { "SequenceNumber" }, filePath);

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT body, body_encoding FROM messages";
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        var body = reader.GetString(0);
        var encoding = reader.GetString(1);

        Assert.Equal("base64", encoding);
        Assert.Equal(Convert.ToBase64String(binaryData), body);
    }

    [Fact]
    public async Task ExportAsync_WithApplicationProperties_CreatesPropertyColumns()
    {
        var appProps = new Dictionary<string, object>
        {
            ["CustomerId"] = "cust-123",
            ["OrderType"] = "urgent"
        };
        var messages = new[] { CreateMessage(appProps: appProps) };
        var columns = new[] { "SequenceNumber", "CustomerId", "OrderType" };
        var filePath = Path.Combine(_tempDir, "props.db");

        await _service.ExportAsync(messages, columns, filePath);

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT prop_customerid, prop_ordertype FROM messages";
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.Equal("cust-123", reader.GetString(0));
        Assert.Equal("urgent", reader.GetString(1));
    }

    [Fact]
    public async Task ExportAsync_MultipleMessages_InsertsAll()
    {
        var messages = Enumerable.Range(1, 100)
            .Select(i => CreateMessage(sequenceNumber: i))
            .ToList();
        var columns = new[] { "SequenceNumber" };
        var filePath = Path.Combine(_tempDir, "multi.db");

        await _service.ExportAsync(messages, columns, filePath);

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM messages";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.Equal(100, count);
    }

    [Fact]
    public async Task ExportAsync_DateTimeFields_FormattedAsIso8601()
    {
        var enqueuedTime = new DateTimeOffset(2026, 1, 22, 14, 30, 52, TimeSpan.Zero);
        var scheduledTime = new DateTimeOffset(2026, 1, 23, 10, 0, 0, TimeSpan.Zero);
        var msg = new PeekedMessage(
            MessageId: "time-msg",
            SequenceNumber: 1,
            EnqueuedTime: enqueuedTime,
            Subject: null,
            DeliveryCount: 1,
            ContentType: null,
            CorrelationId: null,
            SessionId: null,
            TimeToLive: TimeSpan.FromHours(1),
            ScheduledEnqueueTime: scheduledTime,
            ApplicationProperties: new Dictionary<string, object>(),
            Body: BinaryData.FromString("{}")
        );
        var filePath = Path.Combine(_tempDir, "times.db");

        await _service.ExportAsync(new[] { msg }, new[] { "SequenceNumber", "Enqueued", "ScheduledEnqueue" }, filePath);

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT enqueued_time, scheduled_enqueue_time FROM messages";
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        Assert.Equal("2026-01-22T14:30:52.0000000+00:00", reader.GetString(0));
        Assert.Equal("2026-01-23T10:00:00.0000000+00:00", reader.GetString(1));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~MessageExportServiceTests.ExportAsync_SingleMessage"`
Expected: FAIL - MessageExportService type not found

**Step 3: Write minimal implementation**

Create `src/AsbExplorer/Services/MessageExportService.cs`:

```csharp
using System.Text;
using AsbExplorer.Helpers;
using AsbExplorer.Models;
using Microsoft.Data.Sqlite;

namespace AsbExplorer.Services;

public class MessageExportService
{
    private static readonly HashSet<string> CoreColumns = new()
    {
        "SequenceNumber", "MessageId", "Enqueued", "Subject", "Size",
        "DeliveryCount", "ContentType", "CorrelationId", "SessionId",
        "TimeToLive", "ScheduledEnqueue"
    };

    public async Task ExportAsync(
        IEnumerable<PeekedMessage> messages,
        IEnumerable<string> selectedColumns,
        string filePath)
    {
        var messageList = messages.ToList();
        var columns = selectedColumns.ToList();

        // Separate core columns from application properties
        var coreColumnsToInclude = columns.Where(c => CoreColumns.Contains(c)).ToList();
        var appPropsToInclude = columns.Where(c => !CoreColumns.Contains(c)).ToList();

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        // Create table
        var createTableSql = BuildCreateTableSql(coreColumnsToInclude, appPropsToInclude);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert messages
        var insertSql = BuildInsertSql(coreColumnsToInclude, appPropsToInclude);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = insertSql;

            foreach (var msg in messageList)
            {
                cmd.Parameters.Clear();
                AddParameters(cmd, msg, coreColumnsToInclude, appPropsToInclude);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await transaction.CommitAsync();
    }

    private static string BuildCreateTableSql(List<string> coreColumns, List<string> appProps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE messages (");

        var columnDefs = new List<string>();

        foreach (var col in coreColumns)
        {
            var sqlName = ExportColumnHelper.GetSqlColumnName(col);
            var sqlType = GetSqlType(col);
            var constraint = col == "SequenceNumber" ? " PRIMARY KEY" : "";
            columnDefs.Add($"    {sqlName} {sqlType}{constraint}");
        }

        // Body columns are always included
        columnDefs.Add("    body TEXT");
        columnDefs.Add("    body_encoding TEXT");

        foreach (var prop in appProps)
        {
            var sqlName = ExportColumnHelper.NormalizePropertyName(prop);
            columnDefs.Add($"    {sqlName} TEXT");
        }

        sb.AppendLine(string.Join(",\n", columnDefs));
        sb.Append(')');

        return sb.ToString();
    }

    private static string GetSqlType(string column) => column switch
    {
        "SequenceNumber" => "INTEGER",
        "DeliveryCount" => "INTEGER",
        "Size" => "INTEGER",
        "TimeToLive" => "REAL",
        _ => "TEXT"
    };

    private static string BuildInsertSql(List<string> coreColumns, List<string> appProps)
    {
        var columnNames = new List<string>();

        foreach (var col in coreColumns)
        {
            columnNames.Add(ExportColumnHelper.GetSqlColumnName(col));
        }

        columnNames.Add("body");
        columnNames.Add("body_encoding");

        foreach (var prop in appProps)
        {
            columnNames.Add(ExportColumnHelper.NormalizePropertyName(prop));
        }

        var paramNames = columnNames.Select(c => $"@{c}").ToList();

        return $"INSERT INTO messages ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
    }

    private static void AddParameters(
        SqliteCommand cmd,
        PeekedMessage msg,
        List<string> coreColumns,
        List<string> appProps)
    {
        foreach (var col in coreColumns)
        {
            var sqlName = ExportColumnHelper.GetSqlColumnName(col);
            var value = GetCoreColumnValue(msg, col);
            cmd.Parameters.AddWithValue($"@{sqlName}", value ?? DBNull.Value);
        }

        // Body - detect encoding
        var (bodyContent, bodyEncoding) = EncodeBody(msg.Body);
        cmd.Parameters.AddWithValue("@body", bodyContent);
        cmd.Parameters.AddWithValue("@body_encoding", bodyEncoding);

        foreach (var prop in appProps)
        {
            var sqlName = ExportColumnHelper.NormalizePropertyName(prop);
            var value = msg.ApplicationProperties.TryGetValue(prop, out var v) ? v?.ToString() : null;
            cmd.Parameters.AddWithValue($"@{sqlName}", value ?? DBNull.Value);
        }
    }

    private static object? GetCoreColumnValue(PeekedMessage msg, string column) => column switch
    {
        "SequenceNumber" => msg.SequenceNumber,
        "MessageId" => msg.MessageId,
        "Enqueued" => msg.EnqueuedTime.ToString("o"),
        "Subject" => msg.Subject,
        "Size" => msg.BodySizeBytes,
        "DeliveryCount" => msg.DeliveryCount,
        "ContentType" => msg.ContentType,
        "CorrelationId" => msg.CorrelationId,
        "SessionId" => msg.SessionId,
        "TimeToLive" => msg.TimeToLive.TotalSeconds,
        "ScheduledEnqueue" => msg.ScheduledEnqueueTime?.ToString("o"),
        _ => null
    };

    private static (string Content, string Encoding) EncodeBody(BinaryData body)
    {
        try
        {
            var bytes = body.ToArray();
            var text = Encoding.UTF8.GetString(bytes);

            // Check for invalid UTF-8 sequences (replacement char)
            if (text.Contains('\uFFFD'))
            {
                return (Convert.ToBase64String(bytes), "base64");
            }

            return (text, "text");
        }
        catch
        {
            return (Convert.ToBase64String(body.ToArray()), "base64");
        }
    }
}
```

**Step 4: Run all tests to verify they pass**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~MessageExportServiceTests"`
Expected: All 6 tests PASS

**Step 5: Commit**

```bash
git add src/AsbExplorer/Services/MessageExportService.cs src/AsbExplorer.Tests/Services/MessageExportServiceTests.cs
git commit -m "$(cat <<'EOF'
feat: add MessageExportService for SQLite export

- Creates SQLite database with messages table
- Handles core columns and application properties
- Detects binary vs text body content
- Uses transactions for bulk insert

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Add ExportDirectory to AppSettings

**Files:**
- Modify: `src/AsbExplorer/Models/AppSettings.cs:9`
- Modify: `src/AsbExplorer/Services/SettingsStore.cs:88`

**Step 1: Add ExportDirectory property to AppSettings**

In `src/AsbExplorer/Models/AppSettings.cs`, add after line 9 (before closing brace):

```csharp
    public string? ExportDirectory { get; set; }
```

**Step 2: Add SetExportDirectoryAsync method to SettingsStore**

In `src/AsbExplorer/Services/SettingsStore.cs`, add after line 88 (after `SaveEntityColumnsAsync`):

```csharp
    public async Task SetExportDirectoryAsync(string? directory)
    {
        Settings.ExportDirectory = directory;
        await SaveAsync();
    }
```

**Step 3: Run existing tests to verify no regressions**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: PASS (existing tests should still work)

**Step 4: Commit**

```bash
git add src/AsbExplorer/Models/AppSettings.cs src/AsbExplorer/Services/SettingsStore.cs
git commit -m "$(cat <<'EOF'
feat: add ExportDirectory setting for SQLite export

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Create ExportOptions Model

**Files:**
- Create: `src/AsbExplorer/Models/ExportOptions.cs`

**Step 1: Create the model**

Create `src/AsbExplorer/Models/ExportOptions.cs`:

```csharp
namespace AsbExplorer.Models;

public record ExportOptions(
    bool ExportAll,
    List<string> SelectedColumns
);
```

**Step 2: Verify build succeeds**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/AsbExplorer/Models/ExportOptions.cs
git commit -m "$(cat <<'EOF'
feat: add ExportOptions model for export dialog result

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Create ExportDialog

**Files:**
- Create: `src/AsbExplorer/Views/ExportDialog.cs`

**Step 1: Create ExportDialog**

Create `src/AsbExplorer/Views/ExportDialog.cs`:

```csharp
using System.Drawing;
using Terminal.Gui;
using AsbExplorer.Models;

namespace AsbExplorer.Views;

public class ExportDialog : Dialog
{
    private readonly RadioGroup _scopeRadio;
    private readonly List<CheckBox> _columnCheckboxes = [];
    private readonly List<string> _columnNames;

    public ExportOptions? Result { get; private set; }

    public ExportDialog(int totalCount, int selectedCount, List<ColumnConfig> columns)
    {
        Title = "Export to SQLite";
        Width = 50;

        // Calculate height: scope(3) + separator(1) + columns header(1) + columns + buttons(2) + padding
        var columnCount = columns.Count + 1; // +1 for Body
        var contentHeight = 3 + 1 + 1 + columnCount + 4;
        Height = Math.Min(contentHeight, 30);

        _columnNames = columns.Select(c => c.Name).ToList();
        _columnNames.Add("Body"); // Body is always available

        // Scope selection
        var scopeLabel = new Label
        {
            Text = "Export scope:",
            X = 1,
            Y = 1
        };

        var scopeOptions = new[]
        {
            $"All loaded messages ({totalCount})",
            $"Selected messages ({selectedCount})"
        };

        _scopeRadio = new RadioGroup
        {
            X = 1,
            Y = 2,
            RadioLabels = scopeOptions
        };

        // Disable "Selected" option if nothing selected
        if (selectedCount == 0)
        {
            _scopeRadio.SelectedItem = 0;
        }

        // Separator
        var separator = new Label
        {
            Text = new string('─', 46),
            X = 1,
            Y = 4
        };

        // Columns section
        var columnsLabel = new Label
        {
            Text = "Columns to export:",
            X = 1,
            Y = 5
        };

        // Scrollable container for checkboxes
        var checkboxContainer = new ScrollableView
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3),
            CanFocus = true
        };
        checkboxContainer.VerticalScrollBar.AutoShow = true;
        checkboxContainer.SetContentSize(new Size(45, _columnNames.Count));

        for (var i = 0; i < _columnNames.Count; i++)
        {
            var name = _columnNames[i];
            var isAppProp = columns.Any(c => c.Name == name && c.IsApplicationProperty);
            var suffix = isAppProp ? " (app)" : "";

            var cb = new CheckBox
            {
                Text = $"{name}{suffix}",
                X = 0,
                Y = i,
                CheckedState = CheckState.Checked
            };
            _columnCheckboxes.Add(cb);
            checkboxContainer.Add(cb);
        }

        // Buttons
        var exportButton = new Button
        {
            Text = "Export",
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(1),
            IsDefault = true
        };
        exportButton.Accepting += OnExport;

        var cancelButton = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(1)
        };
        cancelButton.Accepting += (s, e) => Application.RequestStop();

        Add(scopeLabel, _scopeRadio, separator, columnsLabel, checkboxContainer, exportButton, cancelButton);
    }

    private void OnExport(object? sender, CommandEventArgs e)
    {
        var selectedColumns = new List<string>();
        for (var i = 0; i < _columnCheckboxes.Count; i++)
        {
            if (_columnCheckboxes[i].CheckedState == CheckState.Checked)
            {
                selectedColumns.Add(_columnNames[i]);
            }
        }

        if (selectedColumns.Count == 0)
        {
            MessageBox.ErrorQuery("Error", "Select at least one column to export.", "OK");
            return;
        }

        Result = new ExportOptions(
            ExportAll: _scopeRadio.SelectedItem == 0,
            SelectedColumns: selectedColumns
        );
        Application.RequestStop();
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == KeyCode.Esc)
        {
            Application.RequestStop();
            return true;
        }
        return base.OnKeyDown(key);
    }
}

// Re-use ScrollableView from ColumnConfigDialog
internal class ScrollableView : View
{
    public ScrollableView()
    {
        AddCommand(Command.ScrollDown, () => { ScrollVertical(1); return true; });
        AddCommand(Command.ScrollUp, () => { ScrollVertical(-1); return true; });
        MouseBindings.Add(MouseFlags.WheeledDown, Command.ScrollDown);
        MouseBindings.Add(MouseFlags.WheeledUp, Command.ScrollUp);
    }
}
```

**Step 2: Verify build succeeds**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded (may need to remove duplicate ScrollableView - see next step)

**Step 3: Fix duplicate ScrollableView if needed**

The `ScrollableView` class already exists in `ColumnConfigDialog.cs`. Remove the duplicate from `ExportDialog.cs` by deleting the last `internal class ScrollableView : View { ... }` block at the bottom of the file.

**Step 4: Verify build succeeds after fix**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 5: Commit**

```bash
git add src/AsbExplorer/Views/ExportDialog.cs
git commit -m "$(cat <<'EOF'
feat: add ExportDialog for SQLite export options

- Radio buttons for all/selected messages
- Scrollable checkbox list for column selection
- All columns checked by default

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Add Export Button to MessageListView

**Files:**
- Modify: `src/AsbExplorer/Views/MessageListView.cs`

**Step 1: Add Export button field and event**

In `src/AsbExplorer/Views/MessageListView.cs`:

After line 16 (`private readonly Button _clearAllButton;`), add:
```csharp
    private readonly Button _exportButton;
```

After line 36 (`public event Action<int>? LimitChanged;`), add:
```csharp
    public event Action? ExportRequested;
```

**Step 2: Initialize Export button in constructor**

After line 156 (`_clearAllButton.Accepting += (s, e) => ClearSelection();`), add:

```csharp
        _exportButton = new Button
        {
            Text = "Export",
            X = 0,
            Y = 0,
            Visible = false
        };

        _exportButton.Accepting += (s, e) => ExportRequested?.Invoke();
```

**Step 3: Add Export button to Add() call**

Modify line 279 to include `_exportButton`:

Change:
```csharp
        Add(_messageCountLabel, _limitButton, _autoRefreshCheckbox, _countdownLabel, _requeueButton, _clearAllButton, _tableView);
```

To:
```csharp
        Add(_messageCountLabel, _limitButton, _autoRefreshCheckbox, _countdownLabel, _exportButton, _requeueButton, _clearAllButton, _tableView);
```

**Step 4: Add method to update Export button visibility**

After the `UpdateRequeueButtonVisibility` method (around line 535), add:

```csharp
    private void UpdateExportButtonVisibility()
    {
        _exportButton.Visible = _messages.Count > 0;
    }
```

**Step 5: Call UpdateExportButtonVisibility in SetMessages**

In the `SetMessages` method, after line 601 (`UpdateMessageCountLabel();`), add:

```csharp
        UpdateExportButtonVisibility();
```

**Step 6: Update Clear method**

In the `Clear` method (around line 654), after `UpdateRequeueButtonVisibility();`, add:

```csharp
        UpdateExportButtonVisibility();
```

**Step 7: Add methods to get export data**

After `GetSelectedMessages` method (around line 510), add:

```csharp
    public IReadOnlyList<PeekedMessage> GetAllMessages() => _messages;

    public List<ColumnConfig> GetCurrentColumns()
    {
        return _currentColumnSettings?.Columns ?? _columnConfigService.GetDefaultColumns();
    }
```

**Step 8: Verify build succeeds**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 9: Commit**

```bash
git add src/AsbExplorer/Views/MessageListView.cs
git commit -m "$(cat <<'EOF'
feat: add Export button to MessageListView

- Button visible when messages are loaded
- ExportRequested event for MainWindow to handle
- GetAllMessages and GetCurrentColumns helpers

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Register MessageExportService in DI

**Files:**
- Modify: `src/AsbExplorer/Program.cs:17`

**Step 1: Add service registration**

In `src/AsbExplorer/Program.cs`, after line 17 (`services.AddSingleton<ApplicationPropertyScanner>();`), add:

```csharp
services.AddSingleton<MessageExportService>();
```

**Step 2: Verify build succeeds**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/AsbExplorer/Program.cs
git commit -m "$(cat <<'EOF'
chore: register MessageExportService in DI container

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Wire Export Flow in MainWindow

**Files:**
- Modify: `src/AsbExplorer/Views/MainWindow.cs`

**Step 1: Add MessageExportService field**

After line 23 (`private readonly ApplicationPropertyScanner _propertyScanner;`), add:

```csharp
    private readonly MessageExportService _exportService;
```

**Step 2: Add constructor parameter**

Modify the constructor signature (line 44-53) to add the parameter:

```csharp
    public MainWindow(
        ServiceBusConnectionService connectionService,
        ConnectionStore connectionStore,
        MessagePeekService peekService,
        IMessageRequeueService requeueService,
        FavoritesStore favoritesStore,
        SettingsStore settingsStore,
        MessageFormatter formatter,
        ColumnConfigService columnConfigService,
        ApplicationPropertyScanner propertyScanner,
        MessageExportService exportService)
```

**Step 3: Assign the field**

After line 64 (`_propertyScanner = propertyScanner;`), add:

```csharp
        _exportService = exportService;
```

**Step 4: Wire the ExportRequested event**

After line 145 (`_messageList.LimitChanged += OnMessageLimitChanged;`), add:

```csharp
        _messageList.ExportRequested += OnExportRequested;
```

**Step 5: Add OnExportRequested handler**

After the `OnMessageLimitChanged` method (around line 672), add:

```csharp
    private async void OnExportRequested()
    {
        var allMessages = _messageList.GetAllMessages();
        var selectedMessages = _messageList.GetSelectedMessages();
        var columns = _messageList.GetCurrentColumns();

        if (allMessages.Count == 0)
        {
            MessageBox.ErrorQuery("Error", "No messages to export.", "OK");
            return;
        }

        _isModalOpen = true;
        var dialog = new ExportDialog(allMessages.Count, selectedMessages.Count, columns);
        Application.Run(dialog);
        _isModalOpen = false;

        if (dialog.Result is null)
        {
            return;
        }

        var messagesToExport = dialog.Result.ExportAll
            ? allMessages
            : selectedMessages;

        // Get or prompt for export directory
        var exportDir = _settingsStore.Settings.ExportDirectory;
        if (string.IsNullOrEmpty(exportDir))
        {
            var openDialog = new OpenDialog
            {
                Title = "Select Export Directory",
                OpenMode = OpenDialog.OpenDialogMode.Directory
            };
            Application.Run(openDialog);

            if (openDialog.Canceled || openDialog.Path is null)
            {
                return;
            }

            exportDir = openDialog.Path;
            await _settingsStore.SetExportDirectoryAsync(exportDir);
        }

        // Generate filename: {entity-name}-{timestamp}.db
        var entityName = SanitizeFilename(_currentNode?.EntityPath ?? "messages");
        var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmss");
        var filename = $"{entityName}-{timestamp}.db";
        var filePath = Path.Combine(exportDir, filename);

        try
        {
            await _exportService.ExportAsync(
                messagesToExport,
                dialog.Result.SelectedColumns,
                filePath);

            MessageBox.Query("Export Complete", $"Exported {messagesToExport.Count} messages to:\n{filePath}", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Export Failed", $"Failed to export: {ex.Message}", "OK");
        }
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return sanitized.ToLowerInvariant();
    }
```

**Step 6: Verify build succeeds**

Run: `dotnet build src/AsbExplorer/AsbExplorer.csproj`
Expected: Build succeeded

**Step 7: Commit**

```bash
git add src/AsbExplorer/Views/MainWindow.cs
git commit -m "$(cat <<'EOF'
feat: wire export flow in MainWindow

- Show ExportDialog on Export button click
- Prompt for directory on first export
- Generate timestamped filename
- Show success/error message

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Run Full Test Suite and Manual Testing

**Step 1: Run all tests**

Run: `dotnet test src/AsbExplorer.Tests/AsbExplorer.Tests.csproj`
Expected: All tests pass

**Step 2: Manual testing**

Run: `dotnet run --project src/AsbExplorer/AsbExplorer.csproj`

Test the following:
1. Connect to a Service Bus namespace
2. Select a queue/topic with messages
3. Verify Export button appears
4. Click Export
5. Verify dialog shows with scope options and column checkboxes
6. Click Export
7. Verify directory picker appears (first time)
8. Select a directory
9. Verify success message with file path
10. Open the .db file in a SQLite viewer and verify data

**Step 3: Commit any fixes if needed**

---

## Task 11: Final Commit with Feature Complete Message

**Step 1: Verify all changes committed**

Run: `git status`
Expected: Nothing to commit, working tree clean

**Step 2: If any uncommitted changes, commit them**

If there are uncommitted files:
```bash
git add -A
git commit -m "$(cat <<'EOF'
feat: complete Export to SQLite feature (#11)

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Summary

This implementation plan covers:

1. **Task 1:** Add SQLite NuGet dependency
2. **Task 2:** Create column name normalization helper (with tests)
3. **Task 3:** Create MessageExportService core logic (with tests)
4. **Task 4:** Add ExportDirectory to settings
5. **Task 5:** Create ExportOptions model
6. **Task 6:** Create ExportDialog UI
7. **Task 7:** Add Export button to MessageListView
8. **Task 8:** Register service in DI
9. **Task 9:** Wire export flow in MainWindow
10. **Task 10:** Run tests and manual verification
11. **Task 11:** Final commit

Each task follows TDD where applicable and includes exact file paths, line numbers, and complete code.
