using AsbExplorer.Helpers;
using AsbExplorer.Models;

namespace AsbExplorer.Tests.Helpers;

public class AutoRefreshStateHelperTests
{
    [Theory]
    [InlineData(TreeNodeType.Queue)]
    [InlineData(TreeNodeType.QueueDeadLetter)]
    [InlineData(TreeNodeType.TopicSubscription)]
    [InlineData(TreeNodeType.TopicSubscriptionDeadLetter)]
    public void ShouldRefreshMessageList_ValidNode_ReturnsTrue(TreeNodeType nodeType)
    {
        var node = new TreeNodeModel("id", "name", nodeType, "conn", "path");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: false, isModalOpen: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(TreeNodeType.Namespace)]
    [InlineData(TreeNodeType.Topic)]
    [InlineData(TreeNodeType.QueuesFolder)]
    [InlineData(TreeNodeType.TopicsFolder)]
    [InlineData(TreeNodeType.ConnectionsRoot)]
    public void ShouldRefreshMessageList_NonPeekableNode_ReturnsFalse(TreeNodeType nodeType)
    {
        var node = new TreeNodeModel("id", "name", nodeType, "conn");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: false, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshMessageList_NullNode_ReturnsFalse()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(null, isRefreshing: false, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshMessageList_AlreadyRefreshing_ReturnsFalse()
    {
        var node = new TreeNodeModel("id", "name", TreeNodeType.Queue, "conn", "path");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: true, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshMessageList_ModalOpen_ReturnsFalse()
    {
        var node = new TreeNodeModel("id", "name", TreeNodeType.Queue, "conn", "path");

        var result = AutoRefreshStateHelper.ShouldRefreshMessageList(node, isRefreshing: false, isModalOpen: true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshTreeCounts_NotRefreshing_ReturnsTrue()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshTreeCounts(isRefreshing: false, isModalOpen: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRefreshTreeCounts_AlreadyRefreshing_ReturnsFalse()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshTreeCounts(isRefreshing: true, isModalOpen: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRefreshTreeCounts_ModalOpen_ReturnsFalse()
    {
        var result = AutoRefreshStateHelper.ShouldRefreshTreeCounts(isRefreshing: false, isModalOpen: true);

        Assert.False(result);
    }
}
