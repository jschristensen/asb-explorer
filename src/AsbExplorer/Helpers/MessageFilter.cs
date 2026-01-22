using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class MessageFilter
{
    public static bool Matches(PeekedMessage message, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        return message.MessageId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }
}
