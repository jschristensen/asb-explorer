using AsbExplorer.Models;

namespace AsbExplorer.Tests.Models;

public class TreeNodeDisplayFormatterTests
{
    [Fact]
    public void Format_QueueWithDlq_HighlightsDlqSegment()
    {
        var node = new TreeNodeModel(
            Id: "q1",
            DisplayName: "queue-1",
            NodeType: TreeNodeType.Queue,
            MessageCount: 10,
            DlqMessageCount: 3);

        var display = TreeNodeDisplayFormatter.Format(node);

        Assert.Equal("queue-1 (10, D: 3)", display.DisplayText);
        Assert.Equal("D: 3", display.DlqText);
        Assert.True(display.HighlightDlq);
    }

    [Fact]
    public void Format_QueueWithZeroDlq_DoesNotHighlight()
    {
        var node = new TreeNodeModel(
            Id: "q1",
            DisplayName: "queue-1",
            NodeType: TreeNodeType.Queue,
            MessageCount: 10,
            DlqMessageCount: 0);

        var display = TreeNodeDisplayFormatter.Format(node);

        Assert.Equal("queue-1 (10, D: 0)", display.DisplayText);
        Assert.Equal("D: 0", display.DlqText);
        Assert.False(display.HighlightDlq);
    }

    [Fact]
    public void Format_DlqNode_HighlightsCount()
    {
        var node = new TreeNodeModel(
            Id: "q1:dlq",
            DisplayName: "DLQ",
            NodeType: TreeNodeType.QueueDeadLetter,
            MessageCount: 4);

        var display = TreeNodeDisplayFormatter.Format(node);

        Assert.Equal("DLQ (4)", display.DisplayText);
        Assert.Equal("4", display.DlqText);
        Assert.True(display.HighlightDlq);
    }

    [Fact]
    public void Format_DlqNodeZero_DoesNotHighlight()
    {
        var node = new TreeNodeModel(
            Id: "q1:dlq",
            DisplayName: "DLQ",
            NodeType: TreeNodeType.QueueDeadLetter,
            MessageCount: 0);

        var display = TreeNodeDisplayFormatter.Format(node);

        Assert.Equal("DLQ (0)", display.DisplayText);
        Assert.Equal("0", display.DlqText);
        Assert.False(display.HighlightDlq);
    }

    [Fact]
    public void Format_ErrorState_DoesNotHighlight()
    {
        var node = new TreeNodeModel(
            Id: "q1",
            DisplayName: "queue-1",
            NodeType: TreeNodeType.Queue,
            MessageCount: -1,
            DlqMessageCount: -1);

        var display = TreeNodeDisplayFormatter.Format(node);

        Assert.Equal("queue-1 (?)", display.DisplayText);
        Assert.Null(display.DlqText);
        Assert.False(display.HighlightDlq);
    }
}
