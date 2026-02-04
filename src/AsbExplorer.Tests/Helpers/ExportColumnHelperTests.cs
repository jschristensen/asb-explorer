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
    public void NormalizePropertyName_VeryLongName_PreservesFullLength()
    {
        var longName = new string('a', 100);

        var result = ExportColumnHelper.NormalizePropertyName(longName);

        Assert.Equal("prop_" + longName, result);
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
