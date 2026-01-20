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
