using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static class MessageFilter
{
    public static bool Matches(PeekedMessage message, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return true;

        return ContainsIgnoreCase(message.MessageId, searchTerm)
            || ContainsIgnoreCase(message.Subject, searchTerm)
            || ContainsIgnoreCase(message.CorrelationId, searchTerm)
            || ContainsIgnoreCase(message.SessionId, searchTerm)
            || ContainsIgnoreCase(message.ContentType, searchTerm);
    }

    private static bool ContainsIgnoreCase(string? value, string searchTerm)
    {
        return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
    }
}
