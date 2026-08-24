using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;
using DiskGeek.Core.Scanning;
using DiskGeek.Core.Search;
using DiskGeek.Core.Updates;

// A dependency-free end-to-end smoke harness, matching PDFGeek's. Where the xUnit suite covers
// units in isolation, this builds a real folder tree on disk and runs the real scanner and searcher
// over it - the kind of wiring mistake that unit tests happily miss.
//
// Run it with `dotnet run` from this folder. Exit code 0 means everything passed.

var work = Path.Combine(Path.GetTempPath(), "diskgeek-smoke");
if (Directory.Exists(work)) Directory.Delete(work, true);
Directory.CreateDirectory(work);

var passed = 0;
var failed = 0;

async Task CheckAsync(string name, Func<Task<string>> act)
{
    try
    {
        var detail = await act();
        Console.WriteLine($"  PASS  {name}  {detail}");
        passed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL  {name}  {ex.GetType().Name}: {ex.Message}");
        foreach (var line in (ex.StackTrace ?? "").Split('\n').Take(4))
            Console.WriteLine($"        {line.Trim()}");
        failed++;
    }
}

static void Expect(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// A small, known tree: three 1 KB files at the root plus one more in a subfolder.
// Four files, 4,096 bytes in total.
var sub = Path.Combine(work, "nested");
Directory.CreateDirectory(sub);
File.WriteAllBytes(Path.Combine(work, "alpha.txt"), new byte[1024]);
File.WriteAllBytes(Path.Combine(work, "beta.log"), new byte[1024]);
File.WriteAllBytes(Path.Combine(work, "gamma.txt"), new byte[1024]);
File.WriteAllBytes(Path.Combine(sub, "delta.txt"), new byte[1024]);

Console.WriteLine("DiskGeek smoke harness");
Console.WriteLine($"  working folder: {work}");
Console.WriteLine();

FileSystemNode? scanned = null;

await CheckAsync("scan totals the bytes across the whole tree", async () =>
{
    scanned = await new DirectoryScanner().ScanAsync(work);

    Expect(scanned is not null, "scanner returned null");
    Expect(scanned!.SizeInBytes == 4096, $"expected 4096 bytes, got {scanned.SizeInBytes}");
    return $"{scanned.SizeInBytes} bytes";
});

await CheckAsync("scan recurses into subfolders", async () =>
{
    Expect(scanned is not null, "no scan result to inspect");
    var names = Flatten(scanned!).Select(n => n.Name).ToList();
    Expect(names.Contains("delta.txt"), "the nested file was not found");
    return await Task.FromResult($"{names.Count} nodes");
});

await CheckAsync("scan counts the files it found", async () =>
{
    Expect(scanned is not null, "no scan result to inspect");
    Expect(scanned!.FileCount == 4, $"expected 4 files, got {scanned.FileCount}");
    return await Task.FromResult($"{scanned.FileCount} files");
});

await CheckAsync("search filters by extension", async () =>
{
    Expect(scanned is not null, "no scan result to inspect");
    var hits = await new FileSearcher().SearchAsync(scanned!, new SearchCriteria
    {
        Extensions = new[] { ".txt" },
        IncludeDirectories = false
    });

    Expect(hits.Count == 3, $"expected 3 .txt files, got {hits.Count}");
    return $"{hits.Count} matches";
});

await CheckAsync("search filters by minimum size", async () =>
{
    Expect(scanned is not null, "no scan result to inspect");
    var hits = await new FileSearcher().SearchAsync(scanned!, new SearchCriteria
    {
        MinSizeBytes = 2048,
        IncludeDirectories = false
    });

    Expect(hits.Count == 0, $"nothing here is over 2048 bytes, but got {hits.Count}");
    return "0 matches, as expected";
});

await CheckAsync("byte formatter agrees with the scan total", async () =>
{
    var formatted = ByteSizeFormatter.Format(4096);
    Expect(formatted == "4 KB", $"expected '4 KB', got '{formatted}'");
    return await Task.FromResult(formatted);
});

await CheckAsync("update check reports a newer manifest version", async () =>
{
    var checker = new UpdateChecker(new CannedFetcher(
        "<appinfo><version>99.0.0.0</version><url>https://example.com/</url></appinfo>"));

    var result = await checker.CheckForUpdateAsync("https://example.com/manifest.xml", new Version(1, 0, 0, 0));

    Expect(!result.Failed, $"check failed: {result.ErrorMessage}");
    Expect(result.IsUpdateAvailable, "expected an update to be reported");
    return $"latest {result.LatestVersion}";
});

await CheckAsync("update check survives the server being unreachable", async () =>
{
    var checker = new UpdateChecker(new ThrowingFetcher());
    var result = await checker.CheckForUpdateAsync("https://example.invalid/", new Version(1, 0, 0, 0));

    Expect(result.Failed, "a dead server should produce a failed check");
    Expect(!result.IsUpdateAvailable, "a failed check must never claim an update is available");
    return "reported cleanly, did not throw";
});

Directory.Delete(work, true);

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static IEnumerable<FileSystemNode> Flatten(FileSystemNode node)
{
    yield return node;
    foreach (var child in node.Children)
        foreach (var descendant in Flatten(child))
            yield return descendant;
}

file sealed class CannedFetcher(string xml) : IUpdateManifestFetcher
{
    public Task<string> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        Task.FromResult(xml);
}

file sealed class ThrowingFetcher : IUpdateManifestFetcher
{
    public Task<string> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        Task.FromException<string>(new HttpRequestException("simulated: host unreachable"));
}
