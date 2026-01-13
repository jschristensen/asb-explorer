using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class FoldableJsonDocumentTests
{
    [Fact]
    public void GetVisibleLines_NoFolds_ReturnsAllLines()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        var lines = doc.GetVisibleLines();

        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void GetFoldRegions_SimpleObject_ReturnsOneRegion()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Single(regions);
        Assert.Equal(0, regions[0].StartLine);
        Assert.Equal(2, regions[0].EndLine);
    }

    [Fact]
    public void ToggleFoldAt_CollapseRoot_ShowsCollapsedIndicator()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        doc.ToggleFoldAt(0);
        var lines = doc.GetVisibleLines();

        Assert.Single(lines);
        Assert.Contains("{ ... }", lines[0]);
    }

    [Fact]
    public void ToggleFoldAt_ExpandAfterCollapse_RestoresLines()
    {
        var json = """
            {
              "name": "test"
            }
            """;
        var doc = new FoldableJsonDocument(json);

        doc.ToggleFoldAt(0); // Collapse
        doc.ToggleFoldAt(0); // Expand
        var lines = doc.GetVisibleLines();

        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void GetFoldRegions_NestedObjects_ReturnsMultipleRegions()
    {
        var json = """
            {
              "outer": {
                "inner": "value"
              }
            }
            """;
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Equal(2, regions.Count);
    }

    [Fact]
    public void ToggleFoldAt_CollapseNested_OnlyCollapsesThatRegion()
    {
        var json = """
            {
              "outer": {
                "inner": "value"
              }
            }
            """;
        var doc = new FoldableJsonDocument(json);

        // Collapse inner object (line 1 contains "outer": {)
        doc.ToggleFoldAt(1);
        var lines = doc.GetVisibleLines();

        // Should have: {, "outer": { ... }, }
        Assert.Equal(3, lines.Count);
        Assert.Contains("{ ... }", lines[1]);
    }

    [Fact]
    public void GetFoldRegions_Array_ReturnsFoldRegion()
    {
        var json = """
            [
              1,
              2
            ]
            """;
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Single(regions);
        Assert.Equal(0, regions[0].StartLine);
    }

    [Fact]
    public void ToggleFoldAt_CollapseArray_ShowsBrackets()
    {
        var json = """
            [
              1,
              2
            ]
            """;
        var doc = new FoldableJsonDocument(json);

        doc.ToggleFoldAt(0);
        var lines = doc.GetVisibleLines();

        Assert.Single(lines);
        Assert.Contains("[ ... ]", lines[0]);
    }

    [Fact]
    public void GetVisibleLines_SingleLineJson_NoFolding()
    {
        var json = """{"name": "test"}""";
        var doc = new FoldableJsonDocument(json);

        var regions = doc.GetFoldRegions();

        Assert.Empty(regions);
    }

    [Fact]
    public void ToggleFoldAt_CollapseNested_PreservesIndentation()
    {
        var json = """
            {
              "outer": {
                "inner": "value"
              }
            }
            """;
        var doc = new FoldableJsonDocument(json);

        // Collapse inner object (line 1 contains "outer": {)
        doc.ToggleFoldAt(1);
        var lines = doc.GetVisibleLines();

        // Collapsed line should preserve leading indentation
        Assert.StartsWith("  ", lines[1]);
    }
}
