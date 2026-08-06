using DiskGeek.Core.Models;

namespace DiskGeek.Core.Duplicates;

/// <summary>A set of two or more files confirmed byte-for-byte identical.</summary>
public sealed record DuplicateGroup(long SizeInBytes, IReadOnlyList<FileSystemNode> Files)
{
    /// <summary>Disk space that could be freed by deleting all but one copy.</summary>
    public long WastedBytes => SizeInBytes * (Files.Count - 1);
}
