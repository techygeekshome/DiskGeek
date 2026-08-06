using DiskGeek.Core.Models;

namespace DiskGeek.Core.ImageSimilarity;

/// <summary>
/// A set of two or more image files that look alike (near-identical, not necessarily byte-for-byte
/// identical) — e.g. the same photo exported at two different qualities, a shot and a near-duplicate
/// burst frame, or a resized copy. Ordered largest-file-first: a bigger file is usually the
/// higher-resolution/less-compressed version, so it's the one recommended to keep.
/// </summary>
public sealed record SimilarImageGroup(IReadOnlyList<FileSystemNode> Files)
{
    /// <summary>The largest file in the group — the default "keep this one" recommendation.</summary>
    public FileSystemNode Representative => Files[0];

    /// <summary>Disk space that could be freed by deleting every copy except the representative.</summary>
    public long WastedBytes => Files.Skip(1).Sum(f => f.SizeInBytes);
}
