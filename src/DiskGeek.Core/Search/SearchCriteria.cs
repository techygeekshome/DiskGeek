namespace DiskGeek.Core.Search;

/// <summary>
/// Filter criteria for searching a scanned tree. Every field is optional (null/empty = "don't
/// filter on this") so a caller can combine as many or as few as they like — e.g. "just show me
/// everything over 500 MB" needs only <see cref="MinSizeBytes"/> set.
/// </summary>
public sealed record SearchCriteria
{
    /// <summary>Name pattern to match against the file/folder name (not the full path).</summary>
    public string? NamePattern { get; init; }

    /// <summary>
    /// If true, <see cref="NamePattern"/> is a .NET regular expression. If false (default), it's a
    /// simple wildcard pattern using <c>*</c> (any run of characters) and <c>?</c> (any one
    /// character) — the syntax most people already know from Windows Explorer / DOS.
    /// </summary>
    public bool UseRegex { get; init; }

    public long? MinSizeBytes { get; init; }
    public long? MaxSizeBytes { get; init; }

    /// <summary>Only match items last modified on or after this UTC instant.</summary>
    public DateTime? ModifiedAfterUtc { get; init; }

    /// <summary>Only match items last modified on or before this UTC instant.</summary>
    public DateTime? ModifiedBeforeUtc { get; init; }

    /// <summary>
    /// File extensions to match, e.g. [".jpg", ".png"] (case-insensitive, leading dot optional in
    /// what the caller supplies — normalize when building this). Empty/null means "any extension".
    /// Ignored for directories.
    /// </summary>
    public IReadOnlyList<string>? Extensions { get; init; }

    public bool IncludeFiles { get; init; } = true;
    public bool IncludeDirectories { get; init; } = true;

    /// <summary>True if no filter is actually set — a search with this criteria would match everything.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(NamePattern) &&
        MinSizeBytes is null &&
        MaxSizeBytes is null &&
        ModifiedAfterUtc is null &&
        ModifiedBeforeUtc is null &&
        (Extensions is null || Extensions.Count == 0);
}
