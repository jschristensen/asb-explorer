namespace AsbExplorer.Models;

public record ExportOptions(
    bool ExportAll,
    List<string> SelectedColumns
);
