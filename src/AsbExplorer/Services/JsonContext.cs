using System.Text.Json.Serialization;
using AsbExplorer.Models;

namespace AsbExplorer.Services;

[JsonSerializable(typeof(List<ServiceBusConnection>))]
[JsonSerializable(typeof(List<Favorite>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(EntityColumnSettings))]
[JsonSerializable(typeof(ColumnConfig))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
