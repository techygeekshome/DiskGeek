namespace DiskGeek.Core.Models;

/// <summary>
/// A single node (file or directory) in a scanned file system tree.
/// For directories, <see cref="SizeInBytes"/> and <see cref="FileCount"/> are aggregates of all descendants.
/// </summary>
public sealed class FileSystemNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required FileSystemNodeType NodeType { get; init; }
    public DateTime LastModifiedUtc { get; init; }
    public string? Extension { get; init; }

    /// <summary>Own size for files; sum of all descendants for directories.</summary>
    public long SizeInBytes { get; set; }

    /// <summary>1 for files; count of all descendant files for directories.</summary>
    public int FileCount { get; set; }

    /// <summary>True if this directory could not be fully enumerated (permissions, etc.).</summary>
    public bool AccessDenied { get; set; }

    public FileSystemNode? Parent { get; set; }

    public List<FileSystemNode> Children { get; } = new();

    public bool IsDirectory => NodeType == FileSystemNodeType.Directory;

    /// <summary>Percentage (0-100) this node's size represents of a given total.</summary>
    public double PercentOf(long totalBytes) => totalBytes <= 0 ? 0 : (double)SizeInBytes / totalBytes * 100.0;

    public override string ToString() => $"{Name} ({SizeInBytes:N0} bytes)";
}
