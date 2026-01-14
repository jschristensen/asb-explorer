using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class AutoRefreshStateHelper
{
    public static bool ShouldRefreshMessageList(
        TreeNodeModel? selectedNode,
        bool isRefreshing,
        bool isModalOpen)
    {
        if (selectedNode is null) return false;
        if (isRefreshing) return false;
        if (isModalOpen) return false;
        if (!selectedNode.CanPeekMessages) return false;

        return true;
    }

    public static bool ShouldRefreshTreeCounts(
        bool isRefreshing,
        bool isModalOpen)
    {
        if (isRefreshing) return false;
        if (isModalOpen) return false;

        return true;
    }
}
