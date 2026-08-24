using DiskGeek.Core.Updates;
using Xunit;

namespace DiskGeek.Core.Tests;

public class UpdateManifestParserTests
{
    private const string Valid = """
        <?xml version="1.0" encoding="utf-8" ?>
        <appinfo>
          <version>1.4.0.0</version>
          <url>https://example.com/download</url>
          <about>DiskGeek v1.4</about>
        </appinfo>
        """;

    [Fact]
    public void ParsesAValidManifest()
    {
        var manifest = UpdateManifestParser.Parse(Valid);

        Assert.Equal(new Version(1, 4, 0, 0), manifest.Version);
        Assert.Equal("https://example.com/download", manifest.Url);
        Assert.Equal("DiskGeek v1.4", manifest.About);
    }

    [Fact]
    public void AboutIsOptionalAndComesBackNullWhenAbsent()
    {
        var manifest = UpdateManifestParser.Parse(
            "<appinfo><version>2.0</version><url>https://example.com/</url></appinfo>");

        Assert.Null(manifest.About);
    }

    [Fact]
    public void AnEmptyAboutIsTreatedAsAbsentRatherThanAnEmptyString()
    {
        var manifest = UpdateManifestParser.Parse(
            "<appinfo><version>2.0</version><url>https://example.com/</url><about>   </about></appinfo>");

        Assert.Null(manifest.About);
    }

    [Fact]
    public void TrimsSurroundingWhitespaceOnValues()
    {
        var manifest = UpdateManifestParser.Parse(
            "<appinfo><version>  1.2.3  </version><url>  https://example.com/  </url></appinfo>");

        Assert.Equal(new Version(1, 2, 3), manifest.Version);
        Assert.Equal("https://example.com/", manifest.Url);
    }

    // Every one of these must surface as FormatException, never as a raw XmlException - that is the
    // documented contract, so a caller can catch a single type whatever was wrong with the file.
    [Theory]
    [InlineData("not xml at all <<<", "valid XML")]
    [InlineData("<wrongroot><version>1.0</version><url>https://x/</url></wrongroot>", "appinfo")]
    [InlineData("<appinfo><url>https://x/</url></appinfo>", "version")]
    [InlineData("<appinfo><version></version><url>https://x/</url></appinfo>", "version")]
    [InlineData("<appinfo><version>banana</version><url>https://x/</url></appinfo>", "valid version number")]
    [InlineData("<appinfo><version>1.0</version></appinfo>", "url")]
    [InlineData("<appinfo><version>1.0</version><url>  </url></appinfo>", "url")]
    public void RejectsBadManifestsWithAFormatException(string xml, string expectedInMessage)
    {
        var ex = Assert.Throws<FormatException>(() => UpdateManifestParser.Parse(xml));
        Assert.Contains(expectedInMessage, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WrapsTheUnderlyingXmlExceptionRatherThanLosingIt()
    {
        var ex = Assert.Throws<FormatException>(() => UpdateManifestParser.Parse("<unclosed>"));
        Assert.IsType<System.Xml.XmlException>(ex.InnerException);
    }
}
