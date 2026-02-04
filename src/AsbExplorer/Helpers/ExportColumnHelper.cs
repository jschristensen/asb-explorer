using System.Text.RegularExpressions;
using AsbExplorer.Models;

namespace AsbExplorer.Helpers;

public static partial class ExportColumnHelper
{
    public static string GetSqlColumnName(string displayName)
    {
        var def = CoreColumnRegistry.Get(displayName);
        return def?.SqlName ?? NormalizePropertyName(displayName);
    }

    public static string NormalizePropertyName(string propertyName)
    {
        // Replace non-alphanumeric with underscore, lowercase
        var normalized = InvalidCharsRegex().Replace(propertyName, "_").ToLowerInvariant();

        // Collapse multiple underscores
        normalized = MultipleUnderscoreRegex().Replace(normalized, "_");

        // Trim leading/trailing underscores
        normalized = normalized.Trim('_');

        return $"prop_{normalized}";
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"_+")]
    private static partial Regex MultipleUnderscoreRegex();
}
