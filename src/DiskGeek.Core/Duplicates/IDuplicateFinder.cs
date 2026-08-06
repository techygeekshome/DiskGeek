using DiskGeek.Core.Models;

namespace DiskGeek.Core.Duplicates;

public interface IDuplicateFinder
{
    /// <summary>
    /// Finds groups of byte-for-byte identical files anywhere under <paramref name="root"/>,
    /// ordered by how much space each group wastes (largest first).
    /// </summary>
    Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        FileSystemNode root,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
