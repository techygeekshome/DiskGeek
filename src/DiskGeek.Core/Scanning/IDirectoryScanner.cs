using DiskGeek.Core.Models;

namespace DiskGeek.Core.Scanning;

public interface IDirectoryScanner
{
    /// <summary>
    /// Recursively scans <paramref name="rootPath"/> and returns a fully size-aggregated
    /// <see cref="FileSystemNode"/> tree rooted at that path.
    /// </summary>
    Task<FileSystemNode> ScanAsync(string rootPath, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);
}
