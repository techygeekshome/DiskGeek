namespace DiskGeek.Core.Scanning;

/// <summary>Thread-safe running totals for an in-progress scan, with time-based progress throttling.</summary>
internal sealed class ScanCounters
{
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

    private long _files;
    private long _directories;
    private long _bytes;
    private long _lastReportTicks;

    public long FilesScanned => Interlocked.Read(ref _files);
    public long DirectoriesScanned => Interlocked.Read(ref _directories);
    public long BytesScanned => Interlocked.Read(ref _bytes);

    public void AddFile(long size)
    {
        Interlocked.Increment(ref _files);
        Interlocked.Add(ref _bytes, size);
    }

    public void AddDirectory() => Interlocked.Increment(ref _directories);

    /// <summary>Returns true at most ~10 times/second, safe to call from many threads at once.</summary>
    public bool ShouldReport()
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastReportTicks);
        if (now - last < ReportInterval.Ticks)
            return false;

        return Interlocked.CompareExchange(ref _lastReportTicks, now, last) == last;
    }
}
