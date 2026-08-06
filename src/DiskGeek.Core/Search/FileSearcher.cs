using System.Text.RegularExpressions;
using DiskGeek.Core.Models;

namespace DiskGeek.Core.Search;

public interface IFileSearcher
{
    Task<IReadOnlyList<FileSystemNode>> SearchAsync(
        FileSystemNode root,
        SearchCriteria criteria,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filters an already-scanned tree in memory — no disk I/O, so this is fast even against a huge
/// tree. Matches TreeSize's "search within results" behavior rather than a live filesystem search.
/// </summary>
public sealed class FileSearcher : IFileSearcher
{
    public Task<IReadOnlyList<FileSystemNode>> SearchAsync(
        FileSystemNode root,
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(criteria);

        // Walking a multi-million-node tree is real CPU work; keep it off the caller's thread just
        // like the scanner and duplicate finder do (see DirectoryScanner's remarks for why this
        // matters — an "async" method that never actually yields still blocks its caller).
        return Task.Run(() => SearchCore(root, criteria, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<FileSystemNode> SearchCore(
        FileSystemNode root,
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var nameRegex = BuildNameRegex(criteria);
        var normalizedExtensions = NormalizeExtensions(criteria.Extensions);

        var results = new List<FileSystemNode>();
        Walk(root, criteria, nameRegex, normalizedExtensions, results, cancellationToken);

        return results
            .OrderByDescending(n => n.SizeInBytes)
            .ToList();
    }

    private static void Walk(
        FileSystemNode node,
        SearchCriteria criteria,
        Regex? nameRegex,
        HashSet<string>? extensions,
        List<FileSystemNode> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Matches(node, criteria, nameRegex, extensions))
            results.Add(node);

        foreach (var child in node.Children)
            Walk(child, criteria, nameRegex, extensions, results, cancellationToken);
    }

    private static bool Matches(FileSystemNode node, SearchCriteria criteria, Regex? nameRegex, HashSet<string>? extensions)
    {
        if (node.IsDirectory && !criteria.IncludeDirectories) return false;
        if (!node.IsDirectory && !criteria.IncludeFiles) return false;

        if (nameRegex is not null && !nameRegex.IsMatch(node.Name)) return false;

        if (criteria.MinSizeBytes is { } min && node.SizeInBytes < min) return false;
        if (criteria.MaxSizeBytes is { } max && node.SizeInBytes > max) return false;

        if (criteria.ModifiedAfterUtc is { } after && node.LastModifiedUtc < after) return false;
        if (criteria.ModifiedBeforeUtc is { } before && node.LastModifiedUtc > before) return false;

        if (extensions is { Count: > 0 } && !node.IsDirectory)
        {
            var ext = node.Extension ?? string.Empty;
            if (!extensions.Contains(ext)) return false;
        }

        return true;
    }

    private static Regex? BuildNameRegex(SearchCriteria criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria.NamePattern)) return null;

        var pattern = criteria.UseRegex
            ? criteria.NamePattern
            : WildcardToRegexPattern(criteria.NamePattern);

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Translates a DOS-style wildcard pattern (* and ?) into an anchored, case-insensitive regex.</summary>
    private static string WildcardToRegexPattern(string wildcard)
    {
        var escaped = Regex.Escape(wildcard)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");
        return "^" + escaped + "$";
    }

    private static HashSet<string>? NormalizeExtensions(IReadOnlyList<string>? extensions)
    {
        if (extensions is null || extensions.Count == 0) return null;

        return extensions
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
