using System.Text.Json;
using DiskGeek.Core.Models;

namespace DiskGeek.Core.Snapshots;

public interface ISnapshotService
{
    Task SaveAsync(FileSystemNode root, string filePath, CancellationToken cancellationToken = default);
    Task<ScanSnapshot> LoadAsync(string filePath, CancellationToken cancellationToken = default);
    SnapshotComparison Compare(SnapshotNode baseline, SnapshotNode current);
}

/// <summary>
/// Saves a scanned tree to a JSON snapshot file ("what the disk looked like on this date") and
/// compares two of them ("what grew since last week"), matched up by full path. A snapshot vs. a
/// freshly-scanned live tree can also be compared by converting the live tree with
/// <see cref="ToSnapshotNode"/> first.
/// </summary>
public sealed class SnapshotService : ISnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public Task SaveAsync(FileSystemNode root, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        var snapshot = new ScanSnapshot
        {
            TakenUtc = DateTime.UtcNow,
            RootPath = root.FullPath,
            Root = ToSnapshotNode(root)
        };

        // Serializing a huge tree is real CPU + I/O work - keep the same background-thread
        // discipline used everywhere else in Core so a caller on a UI thread never blocks on it.
        return Task.Run(async () =>
        {
            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<ScanSnapshot> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await using var stream = File.OpenRead(filePath);
            var snapshot = await JsonSerializer.DeserializeAsync<ScanSnapshot>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return snapshot ?? throw new InvalidDataException($"'{filePath}' is not a valid snapshot file.");
        }, cancellationToken);
    }

    public static SnapshotNode ToSnapshotNode(FileSystemNode node) => new()
    {
        Name = node.Name,
        FullPath = node.FullPath,
        IsDirectory = node.IsDirectory,
        SizeInBytes = node.SizeInBytes,
        FileCount = node.FileCount,
        LastModifiedUtc = node.LastModifiedUtc,
        Extension = node.Extension,
        Children = node.Children.Select(ToSnapshotNode).ToList()
    };

    public SnapshotComparison Compare(SnapshotNode baseline, SnapshotNode current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        // Flatten starting from each root's *children*, not the root itself: the two roots are the
        // same scanned folder by construction (that's the whole premise of a comparison), so its
        // own total size is always going to differ whenever anything below it changed. Surfacing
        // that as an "Added"/"Removed"/"Changed" entry for the root would just be noise - its net
        // effect is already summarized by NetDeltaBytes.
        var baselineByPath = new Dictionary<string, SnapshotNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in baseline.Children)
            Flatten(child, baselineByPath);

        var currentByPath = new Dictionary<string, SnapshotNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in current.Children)
            Flatten(child, currentByPath);

        var added = new List<SnapshotEntryChange>();
        var removed = new List<SnapshotEntryChange>();
        var changed = new List<SnapshotEntryChange>();

        foreach (var (path, node) in currentByPath)
        {
            if (!baselineByPath.TryGetValue(path, out var before))
            {
                added.Add(new SnapshotEntryChange(path, node.Name, node.IsDirectory, 0, node.SizeInBytes, node.Extension));
            }
            else if (before.SizeInBytes != node.SizeInBytes)
            {
                changed.Add(new SnapshotEntryChange(path, node.Name, node.IsDirectory, before.SizeInBytes, node.SizeInBytes, node.Extension));
            }
        }

        foreach (var (path, node) in baselineByPath)
        {
            if (!currentByPath.ContainsKey(path))
                removed.Add(new SnapshotEntryChange(path, node.Name, node.IsDirectory, node.SizeInBytes, 0, node.Extension));
        }

        // A plain path-keyed diff sees a move/rename as an unrelated delete-here + create-there,
        // which is technically correct but not what a human means by "what changed" - moving a 4 GB
        // video folder shouldn't show up as "4 GB removed" and "4 GB added" with no connection drawn
        // between them. Pull genuine moves/renames out of added/removed before returning.
        var moved = ExtractMoves(added, removed);

        return new SnapshotComparison(
            added.OrderByDescending(c => c.NewSizeBytes).ToList(),
            removed.OrderByDescending(c => c.OldSizeBytes).ToList(),
            changed.OrderByDescending(c => Math.Abs(c.DeltaBytes)).ToList(),
            moved.OrderByDescending(m => m.SizeBytes).ToList());
    }

    /// <summary>
    /// Greedily pairs up "removed" and "added" entries that almost certainly represent the same
    /// item moved and/or renamed, and removes the matched entries from <paramref name="added"/> and
    /// <paramref name="removed"/> in place so they aren't also reported as unrelated add/remove noise.
    /// Two tiers, most-confident first:
    ///   1. Same name + same size + same type (file/dir), different path -> a pure move.
    ///   2. Files only, same extension + same size (and non-zero, to avoid pairing up unrelated
    ///      empty files just because they're both 0 bytes) -> a likely rename (possibly also moved).
    /// Matching is O(n) per tier via a dictionary of queues rather than an O(n^2) nested loop, since
    /// a snapshot comparison can easily involve tens of thousands of changed entries.
    /// </summary>
    private static List<SnapshotMove> ExtractMoves(List<SnapshotEntryChange> added, List<SnapshotEntryChange> removed)
    {
        var moves = new List<SnapshotMove>();
        var matchedAdded = new HashSet<SnapshotEntryChange>();
        var matchedRemoved = new HashSet<SnapshotEntryChange>();

        // Tier 1: pure moves (name, size, and type all match).
        MatchByKey(
            removed, added, moves, matchedRemoved, matchedAdded,
            r => (r.Name, r.OldSizeBytes, r.IsDirectory),
            a => (a.Name, a.NewSizeBytes, a.IsDirectory));

        // Tier 2: likely renames - files only, same extension + size, not already matched above.
        MatchByKey(
            removed.Where(r => !matchedRemoved.Contains(r) && !r.IsDirectory && r.OldSizeBytes > 0),
            added.Where(a => !matchedAdded.Contains(a) && !a.IsDirectory && a.NewSizeBytes > 0),
            moves, matchedRemoved, matchedAdded,
            r => (r.Extension ?? string.Empty, r.OldSizeBytes),
            a => (a.Extension ?? string.Empty, a.NewSizeBytes));

        added.RemoveAll(matchedAdded.Contains);
        removed.RemoveAll(matchedRemoved.Contains);

        return moves;
    }

    private static void MatchByKey<TKey>(
        IEnumerable<SnapshotEntryChange> removedCandidates,
        IEnumerable<SnapshotEntryChange> addedCandidates,
        List<SnapshotMove> moves,
        HashSet<SnapshotEntryChange> matchedRemoved,
        HashSet<SnapshotEntryChange> matchedAdded,
        Func<SnapshotEntryChange, TKey> removedKeySelector,
        Func<SnapshotEntryChange, TKey> addedKeySelector)
        where TKey : notnull
    {
        var byKey = new Dictionary<TKey, Queue<SnapshotEntryChange>>();
        foreach (var candidate in addedCandidates)
        {
            var key = addedKeySelector(candidate);
            if (!byKey.TryGetValue(key, out var queue))
                byKey[key] = queue = new Queue<SnapshotEntryChange>();
            queue.Enqueue(candidate);
        }

        foreach (var removedEntry in removedCandidates)
        {
            var key = removedKeySelector(removedEntry);
            if (!byKey.TryGetValue(key, out var queue) || queue.Count == 0)
                continue;

            var addedEntry = queue.Dequeue();

            // A path that didn't actually change isn't a move - it's the same file matched to
            // itself (can happen when a rename-candidate's key collides with an unrelated file
            // that happens to share name/size/type at a different path but was already excluded
            // by path equality upstream; this guard is just defense in depth).
            if (string.Equals(removedEntry.FullPath, addedEntry.FullPath, StringComparison.OrdinalIgnoreCase))
                continue;

            matchedRemoved.Add(removedEntry);
            matchedAdded.Add(addedEntry);
            moves.Add(new SnapshotMove(
                removedEntry.FullPath, addedEntry.FullPath,
                removedEntry.Name, addedEntry.Name,
                removedEntry.IsDirectory, removedEntry.OldSizeBytes));
        }
    }

    private static void Flatten(SnapshotNode node, Dictionary<string, SnapshotNode> into)
    {
        into[node.FullPath] = node;
        foreach (var child in node.Children)
            Flatten(child, into);
    }
}

/// <summary>One item that differs between two snapshots.</summary>
public sealed record SnapshotEntryChange(string FullPath, string Name, bool IsDirectory, long OldSizeBytes, long NewSizeBytes, string? Extension = null)
{
    public long DeltaBytes => NewSizeBytes - OldSizeBytes;
}

/// <summary>An item that was moved and/or renamed between two snapshots, detected by matching size/type/name/extension rather than path.</summary>
public sealed record SnapshotMove(string OldFullPath, string NewFullPath, string OldName, string NewName, bool IsDirectory, long SizeBytes)
{
    public bool NameChanged => !string.Equals(OldName, NewName, StringComparison.Ordinal);
    public bool PathChanged => !string.Equals(OldFullPath, NewFullPath, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Result of comparing two snapshots (or a snapshot and a live tree converted to one).</summary>
public sealed record SnapshotComparison(
    IReadOnlyList<SnapshotEntryChange> Added,
    IReadOnlyList<SnapshotEntryChange> Removed,
    IReadOnlyList<SnapshotEntryChange> Changed,
    IReadOnlyList<SnapshotMove> Moved)
{
    public long NetDeltaBytes =>
        Added.Sum(a => a.NewSizeBytes) - Removed.Sum(r => r.OldSizeBytes) + Changed.Sum(c => c.DeltaBytes);
}
