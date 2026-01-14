using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class TreeNodeModelTests
{
    [Fact]
    public void EffectiveDisplayName_WhenNoCount_ReturnsDisplayName()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue
        );

        Assert.Equal("my-queue", node.EffectiveDisplayName);
    }

    [Fact]
    public void EffectiveDisplayName_WhenLoading_ReturnsNameOnly()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            IsLoadingCount: true
        );

        Assert.Equal("my-queue", node.EffectiveDisplayName);
    }

    [Fact]
    public void EffectiveDisplayName_WhenHasCount_ReturnsNameWithCount()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            MessageCount: 42
        );

        Assert.Equal("my-queue (42)", node.EffectiveDisplayName);
    }

    [Fact]
    public void EffectiveDisplayName_WhenError_ReturnsNameWithQuestionMark()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            MessageCount: -1
        );

        Assert.Equal("my-queue (?)", node.EffectiveDisplayName);
    }

    [Fact]
    public void EffectiveDisplayName_WhenZeroCount_ReturnsNameWithZero()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            MessageCount: 0
        );

        Assert.Equal("my-queue (0)", node.EffectiveDisplayName);
    }

    [Fact]
    public void CanHaveChildren_QueuesFolderReturnsTrue()
    {
        var node = new TreeNodeModel(
            Id: "queues",
            DisplayName: "Queues",
            NodeType: TreeNodeType.QueuesFolder
        );

        Assert.True(node.CanHaveChildren);
    }

    [Fact]
    public void CanHaveChildren_TopicsFolderReturnsTrue()
    {
        var node = new TreeNodeModel(
            Id: "topics",
            DisplayName: "Topics",
            NodeType: TreeNodeType.TopicsFolder
        );

        Assert.True(node.CanHaveChildren);
    }

    [Fact]
    public void CanPeekMessages_QueuesFolderReturnsFalse()
    {
        var node = new TreeNodeModel(
            Id: "queues",
            DisplayName: "Queues",
            NodeType: TreeNodeType.QueuesFolder
        );

        Assert.False(node.CanPeekMessages);
    }

    [Fact]
    public void CanPeekMessages_TopicsFolderReturnsFalse()
    {
        var node = new TreeNodeModel(
            Id: "topics",
            DisplayName: "Topics",
            NodeType: TreeNodeType.TopicsFolder
        );

        Assert.False(node.CanPeekMessages);
    }

    [Fact]
    public void EffectiveDisplayName_FolderNodeNeverShowsCount()
    {
        var node = new TreeNodeModel(
            Id: "queues",
            DisplayName: "Queues",
            NodeType: TreeNodeType.QueuesFolder,
            MessageCount: 42
        );

        Assert.Equal("Queues", node.EffectiveDisplayName);
    }
}
