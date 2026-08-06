namespace DiskGeek.Core.Scanning;

/// <summary>Configuration for a directory scan.</summary>
public sealed class ScanOptions
{
    /// <summary>Maximum number of directories that may be enumerated concurrently.</summary>
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(2, Environment.ProcessorCount);

    /// <summary>
    /// If false (default), reparse points / junctions / symlinked directories are recorded but not
    /// recursed into, to avoid infinite loops and double-counting.
    /// </summary>
    public bool FollowReparsePoints { get; init; } = false;
}
