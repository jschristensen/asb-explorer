namespace AsbExplorer.Helpers;

public class FoldRegion
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public bool IsCollapsed { get; set; }
    public char BracketType { get; init; } // '{' or '['
}

public class FoldableJsonDocument
{
    private readonly List<string> _lines;
    private readonly List<FoldRegion> _foldRegions;

    public FoldableJsonDocument(string json)
    {
        _lines = json.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        _foldRegions = FindFoldRegions();
    }

    public List<string> GetVisibleLines()
    {
        var result = new List<string>();
        var skipUntilLine = -1;

        for (var i = 0; i < _lines.Count; i++)
        {
            if (i <= skipUntilLine)
            {
                continue;
            }

            var region = _foldRegions.FirstOrDefault(r => r.StartLine == i && r.IsCollapsed);
            if (region != null)
            {
                var closeBracket = region.BracketType == '{' ? '}' : ']';
                var collapsedLine = _lines[i].TrimEnd();

                // Remove trailing bracket if present on same line as content
                if (collapsedLine.EndsWith(region.BracketType))
                {
                    collapsedLine = collapsedLine[..^1].TrimEnd();
                }

                // Preserve leading whitespace for indentation
                var lineContent = _lines[i].TrimEnd();
                var contentWithoutBracket = lineContent.TrimEnd(region.BracketType).TrimEnd();
                result.Add($"{contentWithoutBracket}{region.BracketType} ... {closeBracket}");

                skipUntilLine = region.EndLine;
            }
            else
            {
                result.Add(_lines[i]);
            }
        }

        return result;
    }

    public void ToggleFoldAt(int visibleLineNumber)
    {
        // Convert visible line number to actual line number
        var actualLine = GetActualLineNumber(visibleLineNumber);

        var region = _foldRegions.FirstOrDefault(r => r.StartLine == actualLine);
        if (region != null)
        {
            region.IsCollapsed = !region.IsCollapsed;
        }
    }

    public List<FoldRegion> GetFoldRegions() => _foldRegions.ToList();

    private int GetActualLineNumber(int visibleLineNumber)
    {
        var visibleIndex = 0;
        var skipUntilLine = -1;

        for (var i = 0; i < _lines.Count; i++)
        {
            if (i <= skipUntilLine) continue;

            if (visibleIndex == visibleLineNumber)
            {
                return i;
            }

            var region = _foldRegions.FirstOrDefault(r => r.StartLine == i && r.IsCollapsed);
            if (region != null)
            {
                skipUntilLine = region.EndLine;
            }

            visibleIndex++;
        }

        return visibleLineNumber;
    }

    private List<FoldRegion> FindFoldRegions()
    {
        var regions = new List<FoldRegion>();
        var bracketStack = new Stack<(int line, char bracket)>();

        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            foreach (var c in line)
            {
                if (c is '{' or '[')
                {
                    bracketStack.Push((i, c));
                }
                else if (c is '}' or ']')
                {
                    if (bracketStack.Count > 0)
                    {
                        var (startLine, bracket) = bracketStack.Pop();
                        // Only create fold region if it spans multiple lines
                        if (i > startLine)
                        {
                            regions.Add(new FoldRegion
                            {
                                StartLine = startLine,
                                EndLine = i,
                                BracketType = bracket
                            });
                        }
                    }
                }
            }
        }

        return regions.OrderBy(r => r.StartLine).ToList();
    }
}
