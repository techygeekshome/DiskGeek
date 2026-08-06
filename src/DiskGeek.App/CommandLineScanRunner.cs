using DiskGeek.Core.Export;
using DiskGeek.Core.Models;
using DiskGeek.Core.Scanning;
using DiskGeek.Core.Snapshots;

namespace DiskGeek.App;

/// <summary>
/// Headless (no window) scan-and-export mode, driven entirely by command-line arguments.
/// <para>
/// TreeSize Pro's "scheduled scan" feature ultimately means "run a scan and produce a report
/// unattended, on a timer" — under the hood, even TreeSize itself is normally driven that way via
/// Windows Task Scheduler calling its command-line interface, not some custom in-app scheduler.
/// Building an in-app scheduler here would mean either a background Windows Service (a much bigger
/// surface area: install/uninstall, run-as-a-different-user, start-on-boot — none of which is
/// meaningfully testable from this Linux sandbox) or a "keep the GUI app running all the time"
/// hack that defeats the point. Exposing this same CLI switch instead and pointing users at Task
/// Scheduler is the honest, well-supported way to get the same result without inventing scheduling
/// infrastructure that can't be verified here.
/// </para>
/// </summary>
public static class CommandLineScanRunner
{
    public static bool TryGetScanPath(string[] args, out string? scanPath)
    {
        scanPath = GetArgValue(args, "--scan");
        return scanPath is not null;
    }

    public static int Run(string[] args)
    {
        var scanPath = GetArgValue(args, "--scan")!;
        var csvPath = GetArgValue(args, "--export-csv");
        var htmlPath = GetArgValue(args, "--export-html");
        var snapshotSavePath = GetArgValue(args, "--snapshot-save");
        var snapshotComparePath = GetArgValue(args, "--snapshot-compare");
        var quiet = HasFlag(args, "--quiet");

        if (!Directory.Exists(scanPath))
        {
            Console.Error.WriteLine($"Folder not found: {scanPath}");
            return 1;
        }

        if (csvPath is null && htmlPath is null && snapshotSavePath is null && snapshotComparePath is null)
        {
            Console.Error.WriteLine(
                "--scan requires at least one output: --export-csv, --export-html, --snapshot-save, and/or --snapshot-compare.");
            return 1;
        }

        try
        {
            if (!quiet) Console.WriteLine($"Scanning {scanPath}...");

            var root = new DirectoryScanner().ScanAsync(scanPath).GetAwaiter().GetResult();

            if (!quiet)
                Console.WriteLine($"Done — {root.FileCount:N0} files, {Core.Formatting.ByteSizeFormatter.Format(root.SizeInBytes)} total.");

            if (csvPath is not null)
            {
                ScanExporter.ExportCsv(Flatten(root), csvPath, root.SizeInBytes);
                if (!quiet) Console.WriteLine($"CSV report written to {csvPath}");
            }

            if (htmlPath is not null)
            {
                ScanExporter.ExportHtml($"DiskGeek report — {root.FullPath}", Flatten(root), htmlPath, root.SizeInBytes);
                if (!quiet) Console.WriteLine($"HTML report written to {htmlPath}");
            }

            if (snapshotSavePath is not null)
            {
                new SnapshotService().SaveAsync(root, snapshotSavePath).GetAwaiter().GetResult();
                if (!quiet) Console.WriteLine($"Snapshot written to {snapshotSavePath}");
            }

            if (snapshotComparePath is not null)
            {
                RunSnapshotCompare(root, snapshotComparePath, quiet);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Scan failed: {ex.Message}");
            return 2;
        }
    }

    private static void RunSnapshotCompare(FileSystemNode root, string baselinePath, bool quiet)
    {
        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Snapshot baseline not found: {baselinePath}");
            return;
        }

        var service = new SnapshotService();
        var baseline = service.LoadAsync(baselinePath).GetAwaiter().GetResult();
        var current = SnapshotService.ToSnapshotNode(root);
        var comparison = service.Compare(baseline.Root, current);

        var sign = comparison.NetDeltaBytes >= 0 ? "+" : "-";
        var netDisplay = $"{sign}{Core.Formatting.ByteSizeFormatter.Format(Math.Abs(comparison.NetDeltaBytes))}";

        Console.WriteLine(
            $"Compared against baseline taken {baseline.TakenUtc.ToLocalTime():g}: " +
            $"{comparison.Added.Count:N0} added, {comparison.Removed.Count:N0} removed, " +
            $"{comparison.Changed.Count:N0} changed. Net change: {netDisplay}.");

        if (quiet) return;

        foreach (var a in comparison.Added.Take(20))
            Console.WriteLine($"  + {a.FullPath} ({Core.Formatting.ByteSizeFormatter.Format(a.NewSizeBytes)})");
        foreach (var r in comparison.Removed.Take(20))
            Console.WriteLine($"  - {r.FullPath} ({Core.Formatting.ByteSizeFormatter.Format(r.OldSizeBytes)})");
        foreach (var c in comparison.Changed.Take(20))
            Console.WriteLine($"  ~ {c.FullPath} ({(c.DeltaBytes >= 0 ? "+" : "-")}{Core.Formatting.ByteSizeFormatter.Format(Math.Abs(c.DeltaBytes))})");
    }

    private static IEnumerable<FileSystemNode> Flatten(FileSystemNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Flatten(child))
                yield return descendant;
    }

    private static string? GetArgValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;
}
