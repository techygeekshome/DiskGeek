using DiskGeek.Core.Duplicates;
using DiskGeek.Core.Models;

namespace DiskGeek.Core.ImageSimilarity;

public interface IImageSimilarityFinder
{
    Task<ImageSimilarityScanResult> FindSimilarImagesAsync(
        FileSystemNode root,
        int maxHammingDistance = 10,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a similar-image scan, including whether the candidate set had to be capped (see <see cref="ImageSimilarityFinder.MaxCandidates"/>).</summary>
public sealed record ImageSimilarityScanResult(
    IReadOnlyList<SimilarImageGroup> Groups,
    int TotalImageFilesFound,
    int CandidatesConsidered,
    bool CandidatesTruncated);

/// <summary>
/// Finds visually-similar images under a scanned tree using perceptual hashing (see
/// <see cref="PerceptualHash"/>) rather than exact byte content — catches the same photo saved
/// twice at different qualities/sizes, near-identical burst-mode shots, and similar cases that
/// <see cref="DuplicateFinder"/>'s exact SHA-256 matching can never find because the file bytes
/// genuinely differ.
///
/// Unlike exact duplicates (which bucket by an exact hash key in O(n)), "does this look similar"
/// has no exact key to bucket by, so images are compared pairwise and unioned into clusters — an
/// O(n^2) cost in the candidate count. <see cref="MaxCandidates"/> bounds that cost for very large
/// photo collections.
/// </summary>
public sealed class ImageSimilarityFinder : IImageSimilarityFinder
{
    /// <summary>
    /// Hard cap on how many candidate images a single scan will compare against each other.
    /// Clustering is pairwise (every candidate compared against every other), so cost grows with
    /// the square of the candidate count — comparing, say, 8,000 images is ~32 million cheap
    /// (XOR + popcount) comparisons, which finishes in a couple of seconds; a folder with tens of
    /// thousands of photos would multiply that well past what's reasonable for an interactive scan.
    /// When the cap is hit, the most-recently-modified <see cref="MaxCandidates"/> images are used
    /// and the rest are skipped — surfaced to the caller via <see cref="ImageSimilarityScanResult.CandidatesTruncated"/>
    /// rather than silently dropped.
    /// </summary>
    public const int MaxCandidates = 8000;

    public Task<ImageSimilarityScanResult> FindSimilarImagesAsync(
        FileSystemNode root,
        int maxHammingDistance = 10,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (maxHammingDistance is < 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(maxHammingDistance), "Hamming distance threshold must be between 0 and 64.");

        // Everything below is real CPU work (image decoding + pairwise comparison) - keep it off
        // whatever thread the caller is on, same discipline as DuplicateFinder/DirectoryScanner.
        return Task.Run(() => FindSimilarImagesCoreAsync(root, maxHammingDistance, progress, cancellationToken), cancellationToken);
    }

    private static async Task<ImageSimilarityScanResult> FindSimilarImagesCoreAsync(
        FileSystemNode root,
        int maxHammingDistance,
        IProgress<DuplicateScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var allImages = new List<FileSystemNode>();
        CollectImageFiles(root, allImages);
        var totalFound = allImages.Count;

        var truncated = false;
        if (allImages.Count > MaxCandidates)
        {
            allImages = allImages.OrderByDescending(f => f.LastModifiedUtc).Take(MaxCandidates).ToList();
            truncated = true;
        }

        var hashed = new List<(FileSystemNode Node, ulong Hash)>(allImages.Count);
        var processed = 0;

        // Image decoding is CPU-bound - spread it across cores, but cap concurrency so a folder
        // with thousands of large photos doesn't try to decode all of them into memory at once.
        var degreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
        using var throttle = new SemaphoreSlim(degreeOfParallelism);
        var resultLock = new object();

        var hashTasks = allImages.Select(file => Task.Run(async () =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hash = PerceptualHash.Compute(file.FullPath);

                lock (resultLock)
                {
                    processed++;
                    progress?.Report(new DuplicateScanProgress(processed, allImages.Count, file.FullPath));
                    if (hash is not null)
                        hashed.Add((file, hash.Value));
                }
            }
            finally
            {
                throttle.Release();
            }
        }, cancellationToken)).ToList();

        await Task.WhenAll(hashTasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Union-find clustering: images end up in the same group if there's a *chain* of
        // within-threshold matches connecting them, even if the two most-different members of the
        // group aren't close enough to match directly. That matches how a person judges "these are
        // all basically the same picture" for a burst of near-identical shots, rather than requiring
        // every pair in the group to be individually close.
        var parent = new int[hashed.Count];
        for (var i = 0; i < parent.Length; i++)
            parent[i] = i;

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
                parent[rootA] = rootB;
        }

        for (var i = 0; i < hashed.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var j = i + 1; j < hashed.Count; j++)
            {
                if (PerceptualHash.HammingDistance(hashed[i].Hash, hashed[j].Hash) <= maxHammingDistance)
                    Union(i, j);
            }
        }

        var byCluster = new Dictionary<int, List<int>>();
        for (var i = 0; i < hashed.Count; i++)
        {
            var clusterRoot = Find(i);
            if (!byCluster.TryGetValue(clusterRoot, out var members))
                byCluster[clusterRoot] = members = new List<int>();
            members.Add(i);
        }

        var groups = byCluster.Values
            .Where(members => members.Count > 1)
            .Select(members => new SimilarImageGroup(
                members.Select(i => hashed[i].Node)
                    .OrderByDescending(f => f.SizeInBytes)
                    .ThenBy(f => f.FullPath, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderByDescending(g => g.WastedBytes)
            .ToList();

        return new ImageSimilarityScanResult(groups, totalFound, allImages.Count, truncated);
    }

    private static void CollectImageFiles(FileSystemNode node, List<FileSystemNode> into)
    {
        if (!node.IsDirectory)
        {
            if (node.SizeInBytes > 0 && PerceptualHash.SupportedExtensions.Contains(node.Extension ?? string.Empty))
                into.Add(node);
            return;
        }

        foreach (var child in node.Children)
            CollectImageFiles(child, into);
    }
}
