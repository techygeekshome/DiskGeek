using System.Text.RegularExpressions;

namespace DiskGeek.Core.Renaming;

public sealed record RenamePreviewEntry(string OriginalPath, string NewPath, string? Error)
{
    public bool HasError => Error is not null;
    public string OriginalName => Path.GetFileName(OriginalPath);
    public string NewName => Path.GetFileName(NewPath);
}

public sealed record RenameResult(IReadOnlyList<(string From, string To)> Renamed, IReadOnlyList<(string Path, string Error)> Failed)
{
    public bool AllSucceeded => Failed.Count == 0;
}

/// <summary>Builds and applies a batch rename plan for a set of files. Preview before Apply so a user can review every resulting name first.</summary>
public static class BatchRenamer
{
    /// <summary>
    /// Computes the new name for every path without touching disk, so the caller can show a
    /// preview and let the user back out before anything actually changes. Flags per-entry errors
    /// (empty resulting name, collision with another entry in the same batch, collision with an
    /// existing file not part of the batch) rather than throwing, so one bad entry doesn't prevent
    /// showing a preview of the rest.
    /// </summary>
    public static IReadOnlyList<RenamePreviewEntry> Preview(IReadOnlyList<string> filePaths, RenameOptions options)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(options);

        Regex? findRegex = null;
        if (options.FindIsRegex && !string.IsNullOrEmpty(options.FindText))
            findRegex = new Regex(options.FindText);

        var results = new List<RenamePreviewEntry>();
        var newPathsSeenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var originalPathsInBatch = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
        var counter = options.CounterStart;

        foreach (var originalPath in filePaths)
        {
            var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var extension = Path.GetExtension(originalPath);
            var baseName = Path.GetFileNameWithoutExtension(originalPath);

            var newBaseName = ApplyFindReplace(baseName, options, findRegex);
            newBaseName = options.Prefix + newBaseName + options.Suffix;

            if (options.UseCounter)
            {
                newBaseName += counter.ToString().PadLeft(options.CounterDigits, '0');
                counter += options.CounterStep;
            }

            var newName = newBaseName + extension;
            var newPath = Path.Combine(directory, newName);

            string? error = null;
            if (string.IsNullOrWhiteSpace(newBaseName))
            {
                error = "Resulting name would be empty.";
            }
            else if (!newPathsSeenInBatch.Add(newPath))
            {
                error = "Collides with another renamed file in this batch.";
            }
            else if (!string.Equals(newPath, originalPath, StringComparison.OrdinalIgnoreCase) &&
                     File.Exists(newPath) && !originalPathsInBatch.Contains(newPath))
            {
                error = "A file with that name already exists.";
            }

            results.Add(new RenamePreviewEntry(originalPath, newPath, error));
        }

        return results;
    }

    /// <summary>Applies a previously-computed preview. Entries with an <see cref="RenamePreviewEntry.Error"/> are skipped, not attempted.</summary>
    public static RenameResult Apply(IReadOnlyList<RenamePreviewEntry> preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var renamed = new List<(string, string)>();
        var failed = new List<(string, string)>();

        foreach (var entry in preview)
        {
            if (entry.HasError)
            {
                failed.Add((entry.OriginalPath, entry.Error!));
                continue;
            }

            if (string.Equals(entry.OriginalPath, entry.NewPath, StringComparison.Ordinal))
                continue; // no-op rename, nothing to do

            try
            {
                File.Move(entry.OriginalPath, entry.NewPath);
                renamed.Add((entry.OriginalPath, entry.NewPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add((entry.OriginalPath, ex.Message));
            }
        }

        return new RenameResult(renamed, failed);
    }

    private static string ApplyFindReplace(string baseName, RenameOptions options, Regex? findRegex)
    {
        if (string.IsNullOrEmpty(options.FindText))
            return baseName;

        return findRegex is not null
            ? findRegex.Replace(baseName, options.ReplaceText)
            : baseName.Replace(options.FindText, options.ReplaceText, StringComparison.Ordinal);
    }
}
