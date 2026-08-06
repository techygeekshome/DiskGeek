using System.Globalization;
using System.Text;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;

namespace DiskGeek.Core.Export;

/// <summary>
/// Exports a flat list of scanned items (e.g. a whole tree flattened, or a search/duplicate
/// result set) to CSV or a self-contained HTML report. Deliberately does not produce a native
/// .xlsx file — that needs either a third-party library or hand-rolled OOXML zip packaging, and
/// CSV already opens directly in Excel with full fidelity for tabular data like this, so adding
/// that complexity wouldn't earn its keep. HTML covers the "share a nice-looking report" case
/// CSV doesn't.
/// </summary>
public static class ScanExporter
{
    private static readonly string[] CsvHeader =
    {
        "Name", "Full Path", "Type", "Size (Bytes)", "Size", "% of Total", "File Count", "Last Modified (UTC)"
    };

    public static void ExportCsv(IEnumerable<FileSystemNode> items, string filePath, long? totalBytesForPercent = null)
    {
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        WriteCsv(items, writer, totalBytesForPercent);
    }

    public static void WriteCsv(IEnumerable<FileSystemNode> items, TextWriter writer, long? totalBytesForPercent = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(string.Join(",", CsvHeader.Select(CsvEscape)));

        foreach (var item in items)
        {
            var total = totalBytesForPercent ?? item.SizeInBytes;
            var fields = new[]
            {
                item.Name,
                item.FullPath,
                item.IsDirectory ? "Folder" : "File",
                item.SizeInBytes.ToString(CultureInfo.InvariantCulture),
                ByteSizeFormatter.Format(item.SizeInBytes),
                total > 0 ? item.PercentOf(total).ToString("0.00", CultureInfo.InvariantCulture) : "0.00",
                item.FileCount.ToString(CultureInfo.InvariantCulture),
                item.LastModifiedUtc == default ? "" : item.LastModifiedUtc.ToString("u", CultureInfo.InvariantCulture)
            };

            writer.WriteLine(string.Join(",", fields.Select(CsvEscape)));
        }
    }

    public static void ExportHtml(string title, IEnumerable<FileSystemNode> items, string filePath, long? totalBytesForPercent = null)
    {
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        WriteHtml(title, items, writer, totalBytesForPercent);
    }

    public static void WriteHtml(string title, IEnumerable<FileSystemNode> items, TextWriter writer, long? totalBytesForPercent = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(writer);

        var itemList = items.ToList();
        var grandTotal = totalBytesForPercent ?? itemList.Sum(i => i.SizeInBytes);

        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html><head><meta charset=\"utf-8\">");
        writer.WriteLine($"<title>{HtmlEscape(title)}</title>");
        writer.WriteLine("""
            <style>
              body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2328; }
              h1 { font-size: 20px; }
              .meta { color: #57606a; margin-bottom: 16px; }
              table { border-collapse: collapse; width: 100%; }
              th, td { text-align: left; padding: 6px 10px; border-bottom: 1px solid #e1e4e8; font-size: 13px; }
              th { background: #f6f8fa; position: sticky; top: 0; }
              tr:hover { background: #f6f8fa; }
              .num { text-align: right; font-variant-numeric: tabular-nums; }
              .bar-bg { background: #e1e4e8; border-radius: 3px; height: 8px; width: 120px; overflow: hidden; }
              .bar-fg { background: #1a73e8; height: 8px; }
              .folder { font-weight: 600; }
            </style>
            """);
        writer.WriteLine("</head><body>");
        writer.WriteLine($"<h1>{HtmlEscape(title)}</h1>");
        writer.WriteLine($"<div class=\"meta\">{itemList.Count:N0} item(s) &middot; generated {DateTime.Now:yyyy-MM-dd HH:mm}</div>");
        writer.WriteLine("<table>");
        writer.WriteLine("<tr><th>Name</th><th>Full Path</th><th>Type</th><th class=\"num\">Size</th><th>% of Total</th><th class=\"num\">Files</th><th>Last Modified</th></tr>");

        foreach (var item in itemList.OrderByDescending(i => i.SizeInBytes))
        {
            var percent = grandTotal > 0 ? item.PercentOf(grandTotal) : 0;
            var rowClass = item.IsDirectory ? " class=\"folder\"" : "";
            writer.WriteLine("<tr>");
            writer.WriteLine($"<td{rowClass}>{HtmlEscape(item.Name)}</td>");
            writer.WriteLine($"<td>{HtmlEscape(item.FullPath)}</td>");
            writer.WriteLine($"<td>{(item.IsDirectory ? "Folder" : "File")}</td>");
            writer.WriteLine($"<td class=\"num\">{HtmlEscape(ByteSizeFormatter.Format(item.SizeInBytes))}</td>");
            writer.WriteLine($"<td><div class=\"bar-bg\"><div class=\"bar-fg\" style=\"width:{percent.ToString("0.0", CultureInfo.InvariantCulture)}%\"></div></div></td>");
            writer.WriteLine($"<td class=\"num\">{item.FileCount:N0}</td>");
            writer.WriteLine($"<td>{(item.LastModifiedUtc == default ? "" : HtmlEscape(item.LastModifiedUtc.ToLocalTime().ToString("g")))}</td>");
            writer.WriteLine("</tr>");
        }

        writer.WriteLine("</table></body></html>");
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string HtmlEscape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
