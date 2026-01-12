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
}
