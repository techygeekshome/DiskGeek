namespace DiskGeek.Core.Scanning;

/// <summary>A point-in-time snapshot of scan progress, suitable for driving a UI progress indicator.</summary>
public sealed record ScanProgress(long FilesScanned, long DirectoriesScanned, long BytesScanned, string CurrentPath);
