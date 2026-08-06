using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskGeek.Core.Models;
using DiskGeek.Core.Search;

namespace DiskGeek.App.ViewModels;

/// <summary>Drives the Search tab: builds a <see cref="SearchCriteria"/> from bindable UI fields and runs it against the current scan.</summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly IFileSearcher _searcher = new FileSearcher();
    private FileSystemNode? _scanRoot;

    [ObservableProperty]
    private string _namePattern = string.Empty;

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private string _minSizeMb = string.Empty;

    [ObservableProperty]
    private string _maxSizeMb = string.Empty;

    [ObservableProperty]
    private string _extensions = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? _modifiedAfter;

    [ObservableProperty]
    private DateTimeOffset? _modifiedBefore;

    [ObservableProperty]
    private bool _includeFiles = true;

    [ObservableProperty]
    private bool _includeDirectories = true;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusText = "Scan a folder, set your filters, then click Search.";

    public ObservableCollection<SearchResultViewModel> Results { get; } = new();

    public bool HasResults => Results.Count > 0;

    /// <summary>Called whenever a new scan completes, so Search always runs against the latest tree.</summary>
    public void SetScanRoot(FileSystemNode? root)
    {
        _scanRoot = root;
        RunCommand.NotifyCanExecuteChanged();
    }

    public bool CanSearch => !IsSearching && _scanRoot is not null;

    public event EventHandler<FileSystemNode>? ResultActivated;

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task RunAsync()
    {
        if (_scanRoot is null) return;

        var (criteria, error) = BuildCriteria();
        if (error is not null)
        {
            StatusText = error;
            return;
        }

        IsSearching = true;
        Results.Clear();
        StatusText = "Searching…";

        try
        {
            var matches = await _searcher.SearchAsync(_scanRoot, criteria);
            foreach (var match in matches)
                Results.Add(new SearchResultViewModel(match));

            StatusText = matches.Count == 0
                ? "No matches."
                : $"{matches.Count:N0} match(es).";
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            OnPropertyChanged(nameof(HasResults));
        }
    }

    [RelayCommand]
    private void Clear()
    {
        NamePattern = string.Empty;
        UseRegex = false;
        MinSizeMb = string.Empty;
        MaxSizeMb = string.Empty;
        Extensions = string.Empty;
        ModifiedAfter = null;
        ModifiedBefore = null;
        IncludeFiles = true;
        IncludeDirectories = true;
        Results.Clear();
        StatusText = "Scan a folder, set your filters, then click Search.";
        OnPropertyChanged(nameof(HasResults));
    }

    public void ActivateResult(SearchResultViewModel result) => ResultActivated?.Invoke(this, result.Node);

    /// <summary>
    /// Drops any result rows wrapping the given nodes — used after those files were deleted or
    /// renamed elsewhere (Duplicates delete, Batch Rename), since the underlying node either no
    /// longer exists in the tree or has been replaced by a new node instance the old row doesn't
    /// know about. Removing the row is safer than leaving it showing stale data.
    /// </summary>
    public void RemoveResults(IEnumerable<FileSystemNode> nodes)
    {
        var set = new HashSet<FileSystemNode>(nodes);
        if (set.Count == 0) return;

        var toRemove = Results.Where(r => set.Contains(r.Node)).ToList();
        if (toRemove.Count == 0) return;

        foreach (var row in toRemove)
            Results.Remove(row);

        OnPropertyChanged(nameof(HasResults));
    }

    partial void OnIsSearchingChanged(bool value) => RunCommand.NotifyCanExecuteChanged();

    private (SearchCriteria Criteria, string? Error) BuildCriteria()
    {
        long? minBytes = null, maxBytes = null;

        if (!string.IsNullOrWhiteSpace(MinSizeMb))
        {
            if (!double.TryParse(MinSizeMb, NumberStyles.Float, CultureInfo.InvariantCulture, out var minMb) || minMb < 0)
                return (null!, $"'{MinSizeMb}' isn't a valid minimum size (MB).");
            minBytes = (long)(minMb * 1024 * 1024);
        }

        if (!string.IsNullOrWhiteSpace(MaxSizeMb))
        {
            if (!double.TryParse(MaxSizeMb, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxMb) || maxMb < 0)
                return (null!, $"'{MaxSizeMb}' isn't a valid maximum size (MB).");
            maxBytes = (long)(maxMb * 1024 * 1024);
        }

        if (minBytes is not null && maxBytes is not null && minBytes > maxBytes)
            return (null!, "Minimum size can't be larger than maximum size.");

        if (!IncludeFiles && !IncludeDirectories)
            return (null!, "At least one of Files or Folders must be included.");

        var extensionList = Extensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (UseRegex && !string.IsNullOrWhiteSpace(NamePattern))
        {
            try { _ = new System.Text.RegularExpressions.Regex(NamePattern); }
            catch (ArgumentException ex) { return (null!, $"Invalid regular expression: {ex.Message}"); }
        }

        var criteria = new SearchCriteria
        {
            NamePattern = string.IsNullOrWhiteSpace(NamePattern) ? null : NamePattern,
            UseRegex = UseRegex,
            MinSizeBytes = minBytes,
            MaxSizeBytes = maxBytes,
            ModifiedAfterUtc = ModifiedAfter?.UtcDateTime,
            ModifiedBeforeUtc = ModifiedBefore?.UtcDateTime,
            Extensions = extensionList.Count > 0 ? extensionList : null,
            IncludeFiles = IncludeFiles,
            IncludeDirectories = IncludeDirectories
        };

        return (criteria, null);
    }
}
