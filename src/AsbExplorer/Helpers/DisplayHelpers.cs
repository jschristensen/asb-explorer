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
}
