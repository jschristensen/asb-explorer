using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class CoreColumnRegistryTests
{
    [Fact]
    public void All_Returns11ColumnsInOrder()
    {
        var columns = CoreColumnRegistry.All;

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

    [Theory]
    [InlineData("SequenceNumber", true)]
    [InlineData("MessageId", true)]
    [InlineData("Enqueued", true)]
    [InlineData("Subject", true)]
    [InlineData("Size", true)]
    [InlineData("DeliveryCount", true)]
    [InlineData("ContentType", true)]
    [InlineData("CorrelationId", true)]
    [InlineData("SessionId", true)]
    [InlineData("TimeToLive", true)]
    [InlineData("ScheduledEnqueue", true)]
    [InlineData("SomeAppProp", false)]
    [InlineData("Unknown", false)]
    [InlineData("", false)]
    public void IsCore_ReturnsExpected(string name, bool expected)
    {
        var result = CoreColumnRegistry.IsCore(name);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Get_ReturnsDefinition_WhenColumnExists()
    {
        var result = CoreColumnRegistry.Get("SequenceNumber");

        Assert.NotNull(result);
        Assert.Equal("SequenceNumber", result.Name);
        Assert.Equal("#", result.Header);
        Assert.Equal("sequence_number", result.SqlName);
        Assert.Equal("INTEGER", result.SqlType);
    }

    [Fact]
    public void Get_ReturnsNull_WhenColumnDoesNotExist()
    {
        var result = CoreColumnRegistry.Get("Unknown");

        Assert.Null(result);
    }

    [Fact]
    public void All_HasCorrectDefaultVisibility()
    {
        var columns = CoreColumnRegistry.All;

        // First 7 visible by default
        Assert.True(columns[0].DefaultVisible);  // SequenceNumber
        Assert.True(columns[1].DefaultVisible);  // MessageId
        Assert.True(columns[2].DefaultVisible);  // Enqueued
        Assert.True(columns[3].DefaultVisible);  // Subject
        Assert.True(columns[4].DefaultVisible);  // Size
        Assert.True(columns[5].DefaultVisible);  // DeliveryCount
        Assert.True(columns[6].DefaultVisible);  // ContentType

        // Last 4 hidden by default
        Assert.False(columns[7].DefaultVisible); // CorrelationId
        Assert.False(columns[8].DefaultVisible); // SessionId
        Assert.False(columns[9].DefaultVisible); // TimeToLive
        Assert.False(columns[10].DefaultVisible);// ScheduledEnqueue
    }

    [Fact]
    public void All_HasCorrectHeaders()
    {
        var columns = CoreColumnRegistry.All;

        Assert.Equal("#", columns[0].Header);           // SequenceNumber
        Assert.Equal("MessageId", columns[1].Header);   // MessageId (no change)
        Assert.Equal("Enqueued", columns[2].Header);
        Assert.Equal("Subject", columns[3].Header);
        Assert.Equal("Size", columns[4].Header);
        Assert.Equal("Delivery", columns[5].Header);    // DeliveryCount -> Delivery
        Assert.Equal("ContentType", columns[6].Header);
        Assert.Equal("CorrelationId", columns[7].Header);
        Assert.Equal("SessionId", columns[8].Header);
        Assert.Equal("TimeToLive", columns[9].Header);
        Assert.Equal("Scheduled", columns[10].Header);  // ScheduledEnqueue -> Scheduled
    }

    [Fact]
    public void All_HasCorrectSqlNames()
    {
        var columns = CoreColumnRegistry.All;

        Assert.Equal("sequence_number", columns[0].SqlName);
        Assert.Equal("message_id", columns[1].SqlName);
        Assert.Equal("enqueued_time", columns[2].SqlName);
        Assert.Equal("subject", columns[3].SqlName);
        Assert.Equal("body_size_bytes", columns[4].SqlName);
        Assert.Equal("delivery_count", columns[5].SqlName);
        Assert.Equal("content_type", columns[6].SqlName);
        Assert.Equal("correlation_id", columns[7].SqlName);
        Assert.Equal("session_id", columns[8].SqlName);
        Assert.Equal("time_to_live_seconds", columns[9].SqlName);
        Assert.Equal("scheduled_enqueue_time", columns[10].SqlName);
    }

    [Fact]
    public void All_HasCorrectSqlTypes()
    {
        var columns = CoreColumnRegistry.All;

        Assert.Equal("INTEGER", columns[0].SqlType);  // SequenceNumber
        Assert.Equal("TEXT", columns[1].SqlType);     // MessageId
        Assert.Equal("TEXT", columns[2].SqlType);     // Enqueued
        Assert.Equal("TEXT", columns[3].SqlType);     // Subject
        Assert.Equal("INTEGER", columns[4].SqlType);  // Size
        Assert.Equal("INTEGER", columns[5].SqlType);  // DeliveryCount
        Assert.Equal("TEXT", columns[6].SqlType);     // ContentType
        Assert.Equal("TEXT", columns[7].SqlType);     // CorrelationId
        Assert.Equal("TEXT", columns[8].SqlType);     // SessionId
        Assert.Equal("REAL", columns[9].SqlType);     // TimeToLive
        Assert.Equal("TEXT", columns[10].SqlType);    // ScheduledEnqueue
    }

    [Fact]
    public void All_HasCorrectWidths()
    {
        var columns = CoreColumnRegistry.All;

        // SequenceNumber
        Assert.Equal(3, columns[0].MinWidth);
        Assert.Equal(12, columns[0].MaxWidth);

        // MessageId
        Assert.Equal(12, columns[1].MinWidth);
        Assert.Equal(14, columns[1].MaxWidth);

        // Enqueued
        Assert.Equal(10, columns[2].MinWidth);
        Assert.Equal(12, columns[2].MaxWidth);

        // Subject
        Assert.Equal(10, columns[3].MinWidth);
        Assert.Equal(30, columns[3].MaxWidth);

        // Size
        Assert.Equal(6, columns[4].MinWidth);
        Assert.Equal(8, columns[4].MaxWidth);

        // DeliveryCount
        Assert.Equal(3, columns[5].MinWidth);
        Assert.Equal(8, columns[5].MaxWidth);

        // ContentType
        Assert.Equal(8, columns[6].MinWidth);
        Assert.Equal(20, columns[6].MaxWidth);

        // CorrelationId
        Assert.Equal(10, columns[7].MinWidth);
        Assert.Equal(14, columns[7].MaxWidth);

        // SessionId
        Assert.Equal(8, columns[8].MinWidth);
        Assert.Equal(14, columns[8].MaxWidth);

        // TimeToLive
        Assert.Equal(6, columns[9].MinWidth);
        Assert.Equal(10, columns[9].MaxWidth);

        // ScheduledEnqueue
        Assert.Equal(8, columns[10].MinWidth);
        Assert.Equal(12, columns[10].MaxWidth);
    }
}
