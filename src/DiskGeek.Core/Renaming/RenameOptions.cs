namespace DiskGeek.Core.Renaming;

/// <summary>
/// Rules for a batch rename, modeled on Total Commander's Multi-Rename Tool — the one feature from
/// it that clearly earns its place in a disk-cleanup tool: after finding duplicates or search
/// results, being able to rename a batch of files in one pass (rather than one at a time) is a
/// real workflow, not scope creep. Deliberately narrower than TC's full MRT (no full regex capture
/// groups in the replacement, no metadata placeholders like EXIF date) — those are real file-
/// manager features this app isn't trying to become.
/// </summary>
public sealed record RenameOptions
{
    /// <summary>Text (or, if <see cref="FindIsRegex"/>, a regex) to search for in the name (without extension).</summary>
    public string? FindText { get; init; }

    public bool FindIsRegex { get; init; }

    /// <summary>Replacement text. Ignored if <see cref="FindText"/> is empty.</summary>
    public string ReplaceText { get; init; } = string.Empty;

    public string? Prefix { get; init; }
    public string? Suffix { get; init; }

    /// <summary>Appends an incrementing number (e.g. "_001") before the extension.</summary>
    public bool UseCounter { get; init; }
    public int CounterStart { get; init; } = 1;
    public int CounterStep { get; init; } = 1;
    public int CounterDigits { get; init; } = 2;

    public bool IsEmpty =>
        string.IsNullOrEmpty(FindText) &&
        string.IsNullOrEmpty(Prefix) &&
        string.IsNullOrEmpty(Suffix) &&
        !UseCounter;
}
