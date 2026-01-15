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
        // Queue nodes show both main and DLQ counts
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            MessageCount: 42,
            DlqMessageCount: 5
        );

        Assert.Equal("my-queue (42, D: 5)", node.EffectiveDisplayName);
    }

    [Fact]
    public void EffectiveDisplayName_DlqNode_ShowsSingleCount()
    {
        // DLQ nodes only show single count
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "DLQ",
            NodeType: TreeNodeType.QueueDeadLetter,
            MessageCount: 5
        );

        Assert.Equal("DLQ (5)", node.EffectiveDisplayName);
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
        // Queue nodes show both main and DLQ counts
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            MessageCount: 0,
            DlqMessageCount: 0
        );

        Assert.Equal("my-queue (0, D: 0)", node.EffectiveDisplayName);
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
