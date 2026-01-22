using System.Text;
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
            || ContainsIgnoreCase(message.ContentType, searchTerm)
            || MatchesApplicationProperties(message.ApplicationProperties, searchTerm)
            || MatchesBody(message.Body, searchTerm);
    }

    public static IReadOnlyList<PeekedMessage> Apply(IReadOnlyList<PeekedMessage> messages, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return messages;

        return messages.Where(m => Matches(m, searchTerm)).ToList();
    }

    private static bool ContainsIgnoreCase(string? value, string searchTerm)
    {
        return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool MatchesApplicationProperties(IReadOnlyDictionary<string, object> props, string searchTerm)
    {
        foreach (var (key, value) in props)
        {
            if (ContainsIgnoreCase(key, searchTerm))
                return true;
            if (ContainsIgnoreCase(value?.ToString(), searchTerm))
                return true;
        }
        return false;
    }

    private static bool MatchesBody(BinaryData body, string searchTerm)
    {
        try
        {
            var text = Encoding.UTF8.GetString(body.ToArray());
            return ContainsIgnoreCase(text, searchTerm);
        }
        catch
        {
            // Body is not valid UTF-8, skip body matching
            return false;
        }
    }
}
