namespace AsbExplorer.Models;

public record FilterState(string SearchTerm, bool IsInputActive)
{
    public static FilterState Empty => new("", false);

    public bool HasFilter => !string.IsNullOrEmpty(SearchTerm);
}
