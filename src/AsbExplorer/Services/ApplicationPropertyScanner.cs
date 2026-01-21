using AsbExplorer.Models;

namespace AsbExplorer.Services;

public class ApplicationPropertyScanner
{
    public IReadOnlySet<string> ScanMessages(IEnumerable<PeekedMessage> messages, int limit = 20)
    {
        return messages
            .Take(limit)
            .SelectMany(m => m.ApplicationProperties?.Keys ?? [])
            .ToHashSet();
    }
}
