namespace AsbExplorer.Helpers;

public static class DisplayHelpers
{
    public static string TruncateId(string id, int maxLength)
    {
        return id.Length > maxLength ? $"{id[..maxLength]}..." : id;
    }

    public static string FormatRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;

        return diff.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)diff.TotalMinutes}m ago",
            < 1440 => $"{(int)diff.TotalHours}h ago",
            _ => $"{(int)diff.TotalDays}d ago"
        };
    }

    public static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes}B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1}MB"
        };
    }

    private const int MinPropertyColumnWidth = 10;
    private const int PropertyColumnPadding = 2;

    public static int CalculatePropertyColumnWidth(IEnumerable<string> propertyNames)
    {
        var maxLength = propertyNames
            .Where(n => n is not null)
            .Select(n => n.Length)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(maxLength + PropertyColumnPadding, MinPropertyColumnWidth);
    }
}
