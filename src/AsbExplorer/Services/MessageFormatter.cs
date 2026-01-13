using System.Text;
using System.Text.Json;
using System.Xml;

namespace AsbExplorer.Services;

public class MessageFormatter
{
    public (string Content, string Format) Format(BinaryData body, string? contentType)
    {
        // Try UTF-8 string first
        string? text = TryGetUtf8String(body);

        if (text is null)
        {
            return (FormatAsHex(body), "hex");
        }

        // Try JSON
        if (TryFormatJson(text, out var json))
        {
            return (json!, "json");
        }

        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (text, "json");
        }

        // Try XML
        if (TryFormatXml(text, out var xml))
        {
            return (xml!, "xml");
        }

        if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (text, "xml");
        }

        return (text, "text");
    }

    private static string? TryGetUtf8String(BinaryData body)
    {
        try
        {
            var bytes = body.ToArray();
            var text = Encoding.UTF8.GetString(bytes);

            // Check for invalid UTF-8 sequences (replacement char)
            if (text.Contains('\uFFFD'))
            {
                return null;
            }

            return text;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryFormatJson(string text, out string? formatted)
    {
        formatted = null;
        var trimmed = text.TrimStart();

        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            formatted = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFormatXml(string text, out string? formatted)
    {
        formatted = null;
        var trimmed = text.TrimStart();

        if (!trimmed.StartsWith('<'))
        {
            return false;
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(text);

            using var sw = new StringWriter();
            using var xw = new XmlTextWriter(sw)
            {
                Formatting = System.Xml.Formatting.Indented,
                Indentation = 2
            };
            doc.WriteTo(xw);
            formatted = sw.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatAsHex(BinaryData body)
    {
        var bytes = body.ToArray();
        var sb = new StringBuilder();
        const int bytesPerLine = 16;

        for (int i = 0; i < bytes.Length; i += bytesPerLine)
        {
            // Offset
            sb.Append($"{i:X8}  ");

            // Hex bytes
            for (int j = 0; j < bytesPerLine; j++)
            {
                if (i + j < bytes.Length)
                {
                    sb.Append($"{bytes[i + j]:X2} ");
                }
                else
                {
                    sb.Append("   ");
                }

                if (j == 7) sb.Append(' ');
            }

            sb.Append(" |");

            // ASCII
            for (int j = 0; j < bytesPerLine && i + j < bytes.Length; j++)
            {
                var b = bytes[i + j];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }

            sb.AppendLine("|");
        }

        return sb.ToString();
    }
}
