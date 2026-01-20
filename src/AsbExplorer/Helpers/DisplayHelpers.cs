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

    public static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
        {
            var days = (int)ts.TotalDays;
            var hours = ts.Hours;
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }
        if (ts.TotalHours >= 1)
        {
            var hours = (int)ts.TotalHours;
            var minutes = ts.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }
        if (ts.TotalMinutes >= 1)
        {
            var minutes = (int)ts.TotalMinutes;
            var seconds = ts.Seconds;
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }
        return $"{(int)ts.TotalSeconds}s";
    }

    public static string FormatScheduledTime(DateTimeOffset? time)
    {
        if (!time.HasValue)
            return "-";

        var diff = time.Value - DateTimeOffset.UtcNow;
        if (diff.TotalSeconds > 0)
        {
            // Future
            return diff.TotalMinutes switch
            {
                < 1 => "in <1m",
                < 60 => $"in {(int)diff.TotalMinutes}m",
                < 1440 => $"in {(int)diff.TotalHours}h",
                _ => $"in {(int)diff.TotalDays}d"
            };
        }
        else
        {
            // Past - use existing FormatRelativeTime
            return FormatRelativeTime(time.Value);
        }
    }
}
