namespace AsbExplorer.Models;

public class AppSettings
{
    public string Theme { get; set; } = "dark";
    public bool AutoRefreshTreeCounts { get; set; } = false;
    public bool AutoRefreshMessageList { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 10;
    public Dictionary<string, EntityColumnSettings> EntityColumns { get; set; } = [];
}
