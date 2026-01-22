namespace AsbExplorer.Models;

public record TreeNodeDisplay(
    string DisplayText,
    string? DlqText,
    bool HighlightDlq,
    string? PrefixText
);

public static class TreeNodeDisplayFormatter
{
    public static TreeNodeDisplay Format(TreeNodeModel node)
    {
        // Keep folder nodes unchanged
        if (node.NodeType is TreeNodeType.QueuesFolder or TreeNodeType.TopicsFolder)
        {
            return new TreeNodeDisplay(node.DisplayName, null, false, node.DisplayName);
        }

        // Error state
        if (node.MessageCount == -1 || node.DlqMessageCount == -1)
        {
            var text = node.HasDlqChildInternal()
                ? $"{node.DisplayName} (?)"
                : node.MessageCount == -1
                    ? $"{node.DisplayName} (?)"
                    : node.DisplayName;
            return new TreeNodeDisplay(text, null, false, text);
        }

        // Nodes with DLQ child: show both counts
        if (node.HasDlqChildInternal())
        {
            if (node is not { MessageCount: not null, DlqMessageCount: not null })
                return new TreeNodeDisplay(node.DisplayName, null, false, node.DisplayName);
            var dlqText = $"D: {node.DlqMessageCount}";
            var display = $"{node.DisplayName} ({node.MessageCount}, {dlqText})";
            var highlight = node.DlqMessageCount.GetValueOrDefault() > 0;
            var prefix = $"{node.DisplayName} ({node.MessageCount}, ";
            return new TreeNodeDisplay(display, dlqText, highlight, prefix);

        }

        // DLQ-only nodes show single count (including DLQ favorites)
        var isDlqNode = node.NodeType is TreeNodeType.QueueDeadLetter or TreeNodeType.TopicSubscriptionDeadLetter
            || (node.NodeType == TreeNodeType.Favorite &&
                node.SourceEntityType is TreeNodeType.QueueDeadLetter or TreeNodeType.TopicSubscriptionDeadLetter);

        if (isDlqNode)
        {
            if (node.MessageCount.HasValue)
            {
                var dlqText = node.MessageCount.Value.ToString();
                var display = $"{node.DisplayName} ({dlqText})";
                var highlight = node.MessageCount.Value > 0;
                var prefix = $"{node.DisplayName} (";
                return new TreeNodeDisplay(display, dlqText, highlight, prefix);
            }
            return new TreeNodeDisplay(node.DisplayName, null, false, node.DisplayName);
        }

        // Other nodes: single count if available
        if (node.MessageCount.HasValue)
        {
            var display = $"{node.DisplayName} ({node.MessageCount})";
            return new TreeNodeDisplay(display, null, false, node.DisplayName);
        }

        return new TreeNodeDisplay(node.DisplayName, null, false, node.DisplayName);
    }
}
