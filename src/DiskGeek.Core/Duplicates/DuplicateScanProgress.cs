namespace DiskGeek.Core.Duplicates;

public sealed record DuplicateScanProgress(int FilesHashed, int FilesToHash, string CurrentPath);
