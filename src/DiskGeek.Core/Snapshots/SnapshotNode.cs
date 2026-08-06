namespace DiskGeek.Core.Snapshots;

/// <summary>
/// Plain-data (de)serializable mirror of <see cref="Models.FileSystemNode"/>. A snapshot is saved
/// as JSON of this shape rather than the live model directly, so the file format doesn't silently
/// break if <see cref="Models.FileSystemNode"/> changes shape later, and so there's no risk of
/// System.Text.Json trying (and failing) to serialize the doubly-linked Parent/Children graph.
/// </summary>
public sealed class SnapshotNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeInBytes { get; set; }
    public int FileCount { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string? Extension { get; set; }
    public List<SnapshotNode> Children { get; set; } = new();
}

/// <summary>The top-level document written to a .diskscan snapshot file.</summary>
public sealed class ScanSnapshot
{
    /// <summary>Bumped only if the on-disk shape changes in a way older readers can't handle.</summary>
    public int FormatVersion { get; set; } = 1;

    public DateTime TakenUtc { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public SnapshotNode Root { get; set; } = new();
}
