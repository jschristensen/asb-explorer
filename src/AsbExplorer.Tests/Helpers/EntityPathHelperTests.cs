using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class EntityPathHelperTests
{
    [Fact]
    public void GetOriginalEntityPath_QueueDlq_ReturnsQueueName()
    {
        var result = EntityPathHelper.GetOriginalEntityPath("orders-queue", isSubscription: false);
        Assert.Equal("orders-queue", result);
    }

    [Fact]
    public void GetOriginalEntityPath_SubscriptionDlq_ReturnsSubscriptionName()
    {
        var result = EntityPathHelper.GetOriginalEntityPath("my-subscription", isSubscription: true);
        Assert.Equal("my-subscription", result);
    }

    [Fact]
    public void GetOriginalEntityPath_NullPath_ReturnsNull()
    {
        var result = EntityPathHelper.GetOriginalEntityPath(null, isSubscription: false);
        Assert.Null(result);
    }

    [Fact]
    public void GetOriginalEntityPath_EmptyPath_ReturnsEmpty()
    {
        var result = EntityPathHelper.GetOriginalEntityPath("", isSubscription: false);
        Assert.Equal("", result);
    }
}
