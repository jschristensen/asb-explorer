using System.Text;
using AsbExplorer.Helpers;
using AsbExplorer.Models;
using Microsoft.Data.Sqlite;

namespace AsbExplorer.Services;

public class MessageExportService
{
    public async Task ExportAsync(
        IEnumerable<PeekedMessage> messages,
        IEnumerable<string> selectedColumns,
        string filePath)
    {
        var messageList = messages.ToList();
        var columns = selectedColumns.ToList();

        // Separate core columns from application properties
        var coreColumnsToInclude = columns.Where(CoreColumnRegistry.IsCore).ToList();
        var appPropsToInclude = columns.Where(c => !CoreColumnRegistry.IsCore(c)).ToList();

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
            var def = CoreColumnRegistry.Get(col)!;
            var constraint = col == "SequenceNumber" ? " PRIMARY KEY" : "";
            columnDefs.Add($"    {def.SqlName} {def.SqlType}{constraint}");
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

    private static string BuildInsertSql(List<string> coreColumns, List<string> appProps)
    {
        var columnNames = new List<string>();

        foreach (var col in coreColumns)
        {
            columnNames.Add(CoreColumnRegistry.Get(col)!.SqlName);
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
            var sqlName = CoreColumnRegistry.Get(col)!.SqlName;
            var value = CoreColumnValueExtractor.GetExportValue(msg, col);
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
