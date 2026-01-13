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
    public void EffectiveDisplayName_WhenLoading_ReturnsNameWithEllipsis()
    {
        var node = new TreeNodeModel(
            Id: "test",
            DisplayName: "my-queue",
            NodeType: TreeNodeType.Queue,
            IsLoadingCount: true
        );

        Assert.Equal("my-queue (...)", node.EffectiveDisplayName);
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
}
