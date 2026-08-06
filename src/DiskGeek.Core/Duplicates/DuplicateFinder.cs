using System.Security.Cryptography;
using DiskGeek.Core.Models;

namespace DiskGeek.Core.Duplicates;

/// <summary>
/// Finds true (byte-for-byte) duplicate files under a scanned tree in three cheap-to-expensive
/// stages, so the (slow) full-file hashing only ever runs on files that already look like they
/// might match:
///   1. Group by file size — free, no I/O, and instantly rules out the vast majority of files
///      (two files can't be identical if their sizes differ).
///   2. Within a size group, hash only the first few KB of each file — cheap, and rules out most
///      remaining false positives (e.g. two unrelated files that happen to share a size) without
///      reading either file in full.
///   3. Only for files that still match after stage 2, hash the entire file to confirm.
/// </summary>
public sealed class DuplicateFinder : IDuplicateFinder
{
    private const int QuickHashSampleBytes = 4096;

    public Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        FileSystemNode root,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        // Everything below runs on a background thread — including the tree walk and the size
        // grouping, which are synchronous CPU work that could otherwise block a UI thread calling
        // this for a moment on a very large tree. (Exactly the class of bug fixed in the scanner:
        // see DirectoryScanner's remarks.)
        return Task.Run(() => FindDuplicatesCoreAsync(root, progress, cancellationToken), cancellationToken);
    }

    private static async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesCoreAsync(
        FileSystemNode root,
        IProgress<DuplicateScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var allFiles = new List<FileSystemNode>();
        CollectFiles(root, allFiles);

        // Zero-byte files are all trivially "identical" to each other but deleting duplicates of
        // nothing frees no space, and flooding the results with every empty file in the tree would
        // just be noise — so they're excluded, same rationale as the treemap's zero-size filter.
        var sizeGroups = allFiles
            .Where(f => f.SizeInBytes > 0)
            .GroupBy(f => f.SizeInBytes)
            .Where(g => g.Count() > 1)
            .ToList();

        var totalCandidates = sizeGroups.Sum(g => g.Count());
        var hashedSoFar = 0;
        var results = new List<DuplicateGroup>();

        foreach (var sizeGroup in sizeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var byQuickHash = new Dictionary<string, List<FileSystemNode>>();
            foreach (var file in sizeGroup)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var quickHash = await ComputeHashAsync(file.FullPath, QuickHashSampleBytes, cancellationToken)
                    .ConfigureAwait(false);

                hashedSoFar++;
                progress?.Report(new DuplicateScanProgress(hashedSoFar, totalCandidates, file.FullPath));

                if (quickHash is null)
                    continue; // unreadable (permissions, vanished mid-scan, etc.) — just skip it

                if (!byQuickHash.TryGetValue(quickHash, out var bucket))
                    byQuickHash[quickHash] = bucket = new List<FileSystemNode>();
                bucket.Add(file);
            }

            foreach (var quickGroup in byQuickHash.Values.Where(g => g.Count > 1))
            {
                var byFullHash = new Dictionary<string, List<FileSystemNode>>();
                foreach (var file in quickGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fullHash = await ComputeHashAsync(file.FullPath, sampleBytes: null, cancellationToken)
                        .ConfigureAwait(false);
                    if (fullHash is null)
                        continue;

                    if (!byFullHash.TryGetValue(fullHash, out var bucket))
                        byFullHash[fullHash] = bucket = new List<FileSystemNode>();
                    bucket.Add(file);
                }

                foreach (var confirmed in byFullHash.Values.Where(g => g.Count > 1))
                {
                    var ordered = confirmed.OrderBy(f => f.LastModifiedUtc).ThenBy(f => f.FullPath).ToList();
                    results.Add(new DuplicateGroup(ordered[0].SizeInBytes, ordered));
                }
            }
        }

        return (IReadOnlyList<DuplicateGroup>)results.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private static void CollectFiles(FileSystemNode node, List<FileSystemNode> into)
    {
        if (!node.IsDirectory)
        {
            into.Add(node);
            return;
        }

        foreach (var child in node.Children)
            CollectFiles(child, into);
    }

    /// <summary>Hashes the first <paramref name="sampleBytes"/> of a file, or the whole file if null.</summary>
    private static async Task<string?> ComputeHashAsync(string path, int? sampleBytes, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);

            if (sampleBytes is null)
            {
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                return Convert.ToHexString(hash);
            }

            var buffer = new byte[Math.Min(sampleBytes.Value, stream.Length)];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken).ConfigureAwait(false);
                if (read == 0) break; // shouldn't happen given the length clamp above, but don't spin if it does
                totalRead += read;
            }

            return Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, totalRead)));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
