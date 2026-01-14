using AsbExplorer.Services;

namespace AsbExplorer.Tests.Services;

public class MessageFormatterTests
{
    private readonly MessageFormatter _formatter = new();

    [Fact]
    public void Format_ValidJson_ReturnsPrettyPrintedJson()
    {
        var body = BinaryData.FromString("""{"name":"test","value":42}""");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\"name\": \"test\"", content);
        Assert.Contains("\"value\": 42", content);
    }

    [Fact]
    public void Format_ValidXml_ReturnsFormattedXml()
    {
        var body = BinaryData.FromString("<root><item>test</item></root>");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("xml", format);
        Assert.Contains("<root>", content);
        Assert.Contains("<item>test</item>", content);
    }

    [Fact]
    public void Format_PlainText_ReturnsTextFormat()
    {
        var body = BinaryData.FromString("Hello, World!");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("text", format);
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public void Format_BinaryData_ReturnsHexDump()
    {
        var bytes = new byte[] { 0x00, 0x01, 0xFF, 0xFE };
        var body = BinaryData.FromBytes(bytes);

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("hex", format);
        Assert.Contains("00 01 FF FE", content);
    }

    [Fact]
    public void Format_JsonContentType_TreatsAsJson()
    {
        var body = BinaryData.FromString("not valid json");

        var (content, format) = _formatter.Format(body, "application/json");

        Assert.Equal("json", format);
        Assert.Equal("not valid json", content);
    }

    [Fact]
    public void Format_XmlContentType_TreatsAsXml()
    {
        var body = BinaryData.FromString("not valid xml");

        var (content, format) = _formatter.Format(body, "application/xml");

        Assert.Equal("xml", format);
        Assert.Equal("not valid xml", content);
    }

    [Fact]
    public void Format_EmptyBody_ReturnsEmptyText()
    {
        var body = BinaryData.FromString("");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("text", format);
        Assert.Equal("", content);
    }

    [Fact]
    public void Format_JsonArray_ReturnsPrettyPrintedArray()
    {
        var body = BinaryData.FromString("[1,2,3]");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("1", content);
    }

    [Fact]
    public void Format_JsonWithBom_ReturnsJson()
    {
        // UTF-8 BOM followed by JSON
        var jsonWithBom = "\uFEFF{\"name\":\"test\"}";
        var body = BinaryData.FromString(jsonWithBom);

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\"name\"", content);
    }

    [Fact]
    public void Format_JsonWithUtf8BomBytes_ReturnsJson()
    {
        // UTF-8 BOM as raw bytes: EF BB BF
        var bomBytes = new byte[] { 0xEF, 0xBB, 0xBF };
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"test\"}");
        var combined = bomBytes.Concat(jsonBytes).ToArray();
        var body = BinaryData.FromBytes(combined);

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\"name\"", content);
    }

    [Fact]
    public void Format_ValidJson_PrettyPrintsWithNewlines()
    {
        var body = BinaryData.FromString("{\"name\":\"test\",\"value\":42}");

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\n", content); // Should have newlines from pretty printing
        Assert.Contains("  ", content); // Should have indentation
    }

    [Fact]
    public void Format_JsonWithBom_PrettyPrintsWithNewlines()
    {
        var jsonWithBom = "\uFEFF{\"name\":\"test\",\"value\":42}";
        var body = BinaryData.FromString(jsonWithBom);

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\n", content); // Should have newlines from pretty printing
    }

    [Fact]
    public void Format_RealWorldJson_PrettyPrintsWithNewlines()
    {
        // Real message content from user
        var json = "{\"recipients\":[\"da9m@kk.dk\"],\"subject\":\"\",\"body\":\"Davs\\n\\nb7f4f7b8-ccc3-4af3-9a5a-1224891d405d\",\"omitStandardTemplate\":false,\"billeder\":[],\"dokumenter\":[]}";
        var body = BinaryData.FromString(json);

        var (content, format) = _formatter.Format(body, null);

        Assert.Equal("json", format);
        Assert.Contains("\n", content); // Should have newlines from pretty printing
        Assert.Contains("  ", content); // Should have indentation
        Assert.Contains("\"recipients\"", content);
    }
}
