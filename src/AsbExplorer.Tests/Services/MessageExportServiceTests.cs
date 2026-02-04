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
        IReadOnlyDictionary<string, object>? appProps = null)
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
