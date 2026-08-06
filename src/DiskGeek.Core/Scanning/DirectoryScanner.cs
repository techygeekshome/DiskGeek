using DiskGeek.Core.Models;

namespace DiskGeek.Core.Scanning;

/// <summary>
/// Recursively scans a directory tree in parallel, aggregating file sizes bottom-up.
/// </summary>
/// <remarks>
/// Concurrency is bounded by a <see cref="SemaphoreSlim"/> that is only held while a directory's
/// own entries are being enumerated — never while awaiting that directory's subdirectory tasks.
/// Holding the permit across the recursive await would let a deep enough tree deadlock itself
/// (an ancestor holding the only free permit while blocked on a descendant that needs a permit
/// to even start), so the permit is always released before recursing.
/// </remarks>
public sealed class DirectoryScanner : IDirectoryScanner
{
    private readonly ScanOptions _options;

    public DirectoryScanner(ScanOptions? options = null)
    {
        _options = options ?? new ScanOptions();
    }

    public Task<FileSystemNode> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path must not be empty.", nameof(rootPath));

        var rootInfo = new DirectoryInfo(rootPath);
        if (!rootInfo.Exists)
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");

        // Everything below — including the very first directory's enumeration — must happen on a
        // thread-pool thread, not the caller's. `async` alone doesn't guarantee that: SemaphoreSlim
        // .WaitAsync() completes synchronously whenever a permit is free, so without this Task.Run
        // the initial recursive descent (as many directories as MaxDegreeOfParallelism) runs
        // synchronously on whichever thread called ScanAsync. On a UI button-click handler that
        // means the UI thread blocks — no progress updates, no repaints, the window looks hung —
        // for exactly as long as scanning a large tree (e.g. a whole C: drive) takes.
        return Task.Run(() => ScanCoreAsync(rootInfo, progress, cancellationToken), cancellationToken);
    }

    private async Task<FileSystemNode> ScanCoreAsync(
        DirectoryInfo rootInfo,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(Math.Max(1, _options.MaxDegreeOfParallelism));
        var counters = new ScanCounters();

        var root = await ScanDirectoryAsync(rootInfo, throttle, counters, progress, cancellationToken)
            .ConfigureAwait(false);

        // The per-directory reports are time-throttled (see ScanCounters.ShouldReport), so the very
        // last one can be swallowed on a fast scan. Always emit one final, un-throttled report so
        // callers can rely on the last progress update matching the returned tree's totals.
        progress?.Report(new ScanProgress(
            counters.FilesScanned,
            counters.DirectoriesScanned,
            counters.BytesScanned,
            root.FullPath));

        return root;
    }

    private async Task<FileSystemNode> ScanDirectoryAsync(
        DirectoryInfo directoryInfo,
        SemaphoreSlim throttle,
        ScanCounters counters,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var node = new FileSystemNode
        {
            Name = string.IsNullOrEmpty(directoryInfo.Name) ? directoryInfo.FullName : directoryInfo.Name,
            FullPath = directoryInfo.FullName,
            NodeType = FileSystemNodeType.Directory,
            LastModifiedUtc = SafeGetLastWriteTimeUtc(directoryInfo)
        };

        var subDirs = new List<DirectoryInfo>();

        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = directoryInfo.EnumerateFileSystemInfos();
            }
            catch (UnauthorizedAccessException)
            {
                node.AccessDenied = true;
                entries = Array.Empty<FileSystemInfo>();
            }
            catch (IOException)
            {
                node.AccessDenied = true;
                entries = Array.Empty<FileSystemInfo>();
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (entry)
                {
                    case DirectoryInfo subDir:
                        if (!_options.FollowReparsePoints && subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            continue;
                        subDirs.Add(subDir);
                        break;

                    case FileInfo file:
                        var fileNode = BuildFileNode(file, node);
                        node.Children.Add(fileNode);
                        counters.AddFile(fileNode.SizeInBytes);
                        MaybeReportProgress(progress, counters, fileNode.FullPath);
                        break;
                }
            }
        }
        finally
        {
            throttle.Release();
        }

        // Recurse without holding a permit — see the deadlock note in the class remarks.
        if (subDirs.Count > 0)
        {
            var subDirTasks = subDirs
                .Select(sd => ScanDirectoryAsync(sd, throttle, counters, progress, cancellationToken))
                .ToList();

            var subDirNodes = await Task.WhenAll(subDirTasks).ConfigureAwait(false);
            foreach (var subNode in subDirNodes)
            {
                subNode.Parent = node;
                node.Children.Add(subNode);
            }
        }

        node.SizeInBytes = node.Children.Sum(c => c.SizeInBytes);
        node.FileCount = node.Children.Sum(c => c.FileCount);

        counters.AddDirectory();
        MaybeReportProgress(progress, counters, node.FullPath);

        return node;
    }

    private static FileSystemNode BuildFileNode(FileInfo file, FileSystemNode parent)
    {
        long size;
        try
        {
            size = file.Length;
        }
        catch (IOException)
        {
            size = 0;
        }
        catch (UnauthorizedAccessException)
        {
            size = 0;
        }

        return new FileSystemNode
        {
            Name = file.Name,
            FullPath = file.FullName,
            NodeType = FileSystemNodeType.File,
            SizeInBytes = size,
            FileCount = 1,
            Extension = file.Extension,
            LastModifiedUtc = SafeGetLastWriteTimeUtc(file),
            Parent = parent
        };
    }

    private static DateTime SafeGetLastWriteTimeUtc(FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTimeUtc;
        }
        catch
        {
            return default;
        }
    }

    private static void MaybeReportProgress(IProgress<ScanProgress>? progress, ScanCounters counters, string currentPath)
    {
        if (progress is null || !counters.ShouldReport())
            return;

        progress.Report(new ScanProgress(
            counters.FilesScanned,
            counters.DirectoriesScanned,
            counters.BytesScanned,
            currentPath));
    }
}
