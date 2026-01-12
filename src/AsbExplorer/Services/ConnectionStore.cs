using System.Text.Json;

namespace AsbExplorer.Services;

public record ServiceBusConnection(string Name, string ConnectionString)
{
    // Extract namespace from connection string
    public string? NamespaceName
    {
        get
        {
            // Connection string format: Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=...
            var endpoint = ConnectionString
                .Split(';')
                .FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase));

            if (endpoint is null) return null;

            var uri = endpoint.Replace("Endpoint=", "", StringComparison.OrdinalIgnoreCase);
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                return parsed.Host.Split('.')[0];
            }
            return null;
        }
    }
}

public class ConnectionStore
{
    private readonly string _filePath;
    private List<ServiceBusConnection> _connections = [];

    public ConnectionStore()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "asb-explorer"
        );

        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "connections.json");
    }

    public IReadOnlyList<ServiceBusConnection> Connections => _connections.AsReadOnly();

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _connections = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            _connections = JsonSerializer.Deserialize<List<ServiceBusConnection>>(json) ?? [];
        }
        catch
        {
            _connections = [];
        }
    }

    public async Task AddAsync(ServiceBusConnection connection)
    {
        // Remove existing with same name
        _connections.RemoveAll(c => c.Name.Equals(connection.Name, StringComparison.OrdinalIgnoreCase));
        _connections.Add(connection);
        await SaveAsync();
    }

    public async Task RemoveAsync(string name)
    {
        _connections.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        await SaveAsync();
    }

    public ServiceBusConnection? GetByName(string name)
    {
        return _connections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_connections, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
