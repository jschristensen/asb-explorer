using System.Text;
using AsbExplorer.Helpers;
using AsbExplorer.Models;
using Microsoft.Data.Sqlite;

namespace AsbExplorer.Services;

public class MessageExportService
{
    private static readonly HashSet<string> CoreColumns = new()
    {
        "SequenceNumber", "MessageId", "Enqueued", "Subject", "Size",
        "DeliveryCount", "ContentType", "CorrelationId", "SessionId",
        "TimeToLive", "ScheduledEnqueue"
    };

    public async Task ExportAsync(
        IEnumerable<PeekedMessage> messages,
        IEnumerable<string> selectedColumns,
        string filePath)
    {
        var messageList = messages.ToList();
        var columns = selectedColumns.ToList();

        // Separate core columns from application properties
        var coreColumnsToInclude = columns.Where(c => CoreColumns.Contains(c)).ToList();
        var appPropsToInclude = columns.Where(c => !CoreColumns.Contains(c)).ToList();

        await using var connection = new SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        // Create table
        var createTableSql = BuildCreateTableSql(coreColumnsToInclude, appPropsToInclude);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = createTableSql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert messages
        var insertSql = BuildInsertSql(coreColumnsToInclude, appPropsToInclude);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = insertSql;

            foreach (var msg in messageList)
            {
                cmd.Parameters.Clear();
                AddParameters(cmd, msg, coreColumnsToInclude, appPropsToInclude);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await transaction.CommitAsync();
    }

    private static string BuildCreateTableSql(List<string> coreColumns, List<string> appProps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE messages (");

        var columnDefs = new List<string>();

        foreach (var col in coreColumns)
        {
            var sqlName = ExportColumnHelper.GetSqlColumnName(col);
            var sqlType = GetSqlType(col);
            var constraint = col == "SequenceNumber" ? " PRIMARY KEY" : "";
            columnDefs.Add($"    {sqlName} {sqlType}{constraint}");
        }

        // Body columns are always included
        columnDefs.Add("    body TEXT");
        columnDefs.Add("    body_encoding TEXT");

        foreach (var prop in appProps)
        {
            var sqlName = ExportColumnHelper.NormalizePropertyName(prop);
            columnDefs.Add($"    {sqlName} TEXT");
        }

        sb.AppendLine(string.Join(",\n", columnDefs));
        sb.Append(')');

        return sb.ToString();
    }

    private static string GetSqlType(string column) => column switch
    {
        "SequenceNumber" => "INTEGER",
        "DeliveryCount" => "INTEGER",
        "Size" => "INTEGER",
        "TimeToLive" => "REAL",
        _ => "TEXT"
    };

    private static string BuildInsertSql(List<string> coreColumns, List<string> appProps)
    {
        var columnNames = new List<string>();

        foreach (var col in coreColumns)
        {
            columnNames.Add(ExportColumnHelper.GetSqlColumnName(col));
        }

        columnNames.Add("body");
        columnNames.Add("body_encoding");

        foreach (var prop in appProps)
        {
            columnNames.Add(ExportColumnHelper.NormalizePropertyName(prop));
        }

        var paramNames = columnNames.Select(c => $"@{c}").ToList();

        return $"INSERT INTO messages ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
    }

    private static void AddParameters(
        SqliteCommand cmd,
        PeekedMessage msg,
        List<string> coreColumns,
        List<string> appProps)
    {
        foreach (var col in coreColumns)
        {
            var sqlName = ExportColumnHelper.GetSqlColumnName(col);
            var value = GetCoreColumnValue(msg, col);
            cmd.Parameters.AddWithValue($"@{sqlName}", value ?? DBNull.Value);
        }

        // Body - detect encoding
        var (bodyContent, bodyEncoding) = EncodeBody(msg.Body);
        cmd.Parameters.AddWithValue("@body", bodyContent);
        cmd.Parameters.AddWithValue("@body_encoding", bodyEncoding);

        foreach (var prop in appProps)
        {
            var sqlName = ExportColumnHelper.NormalizePropertyName(prop);
            var value = msg.ApplicationProperties.TryGetValue(prop, out var v) ? v?.ToString() : null;
            cmd.Parameters.AddWithValue($"@{sqlName}", (object?)value ?? DBNull.Value);
        }
    }

    private static object? GetCoreColumnValue(PeekedMessage msg, string column) => column switch
    {
        "SequenceNumber" => msg.SequenceNumber,
        "MessageId" => msg.MessageId,
        "Enqueued" => msg.EnqueuedTime.ToString("o"),
        "Subject" => msg.Subject,
        "Size" => msg.BodySizeBytes,
        "DeliveryCount" => msg.DeliveryCount,
        "ContentType" => msg.ContentType,
        "CorrelationId" => msg.CorrelationId,
        "SessionId" => msg.SessionId,
        "TimeToLive" => msg.TimeToLive.TotalSeconds,
        "ScheduledEnqueue" => msg.ScheduledEnqueueTime?.ToString("o"),
        _ => null
    };

    private static (string Content, string Encoding) EncodeBody(BinaryData body)
    {
        try
        {
            var bytes = body.ToArray();
            var text = Encoding.UTF8.GetString(bytes);

            // Check for invalid UTF-8 sequences (replacement char)
            if (text.Contains('\uFFFD'))
            {
                return (Convert.ToBase64String(bytes), "base64");
            }

            return (text, "text");
        }
        catch
        {
            return (Convert.ToBase64String(body.ToArray()), "base64");
        }
    }
}
