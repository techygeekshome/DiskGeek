using System.Xml.Linq;

namespace DiskGeek.Core.Updates;

/// <summary>Parses the small hand-edited XML manifest described in <see cref="UpdateManifest"/>.</summary>
public static class UpdateManifestParser
{
    /// <summary>
    /// Parses <paramref name="xml"/> into an <see cref="UpdateManifest"/>. Throws
    /// <see cref="FormatException"/> (never a raw XML parsing exception) if the document isn't a
    /// valid manifest — missing root element, missing/unparsable &lt;version&gt;, or missing
    /// &lt;url&gt; — so a caller can catch one exception type regardless of what specifically was
    /// wrong with the file.
    /// </summary>
    public static UpdateManifest Parse(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new FormatException($"Update manifest isn't valid XML: {ex.Message}", ex);
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "appinfo")
            throw new FormatException("Update manifest is missing its <appinfo> root element.");

        var versionText = root.Element("version")?.Value?.Trim();
        if (string.IsNullOrEmpty(versionText))
            throw new FormatException("Update manifest is missing a <version> element.");

        if (!Version.TryParse(versionText, out var version))
            throw new FormatException($"Update manifest's <version> value ('{versionText}') isn't a valid version number.");

        var url = root.Element("url")?.Value?.Trim();
        if (string.IsNullOrEmpty(url))
            throw new FormatException("Update manifest is missing a <url> element.");

        var about = root.Element("about")?.Value?.Trim();

        return new UpdateManifest(version, url, string.IsNullOrEmpty(about) ? null : about);
    }
}
