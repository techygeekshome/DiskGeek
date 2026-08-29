using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;

namespace DiskGeek.App.ViewModels;

/// <summary>
/// The shell half of the main view model: which screen is showing, the sidebar's own strings,
/// and the figures the Overview screen puts on top.
///
/// This is a partial rather than an edit to MainWindowViewModel.cs on purpose - the scanning
/// half of that file is unchanged by the 1.1 shell, and keeping the two apart makes that
/// obvious in a diff.
/// </summary>
public partial class MainWindowViewModel
{
    // ---------------------------------------------------------------- navigation

    [ObservableProperty]
    private string _page = "Overview";

    partial void OnPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(IsExplorer));
        OnPropertyChanged(nameof(IsTreemap));
        OnPropertyChanged(nameof(IsDuplicates));
        OnPropertyChanged(nameof(IsSimilar));
        OnPropertyChanged(nameof(IsSearch));
        OnPropertyChanged(nameof(IsSnapshots));
        OnPropertyChanged(nameof(IsSettings));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageStatus));
    }

    public bool IsOverview => Page == "Overview";
    public bool IsExplorer => Page == "Explorer";
    public bool IsTreemap => Page == "Treemap";
    public bool IsDuplicates => Page == "Duplicates";
    public bool IsSimilar => Page == "Similar";
    public bool IsSearch => Page == "Search";
    public bool IsSnapshots => Page == "Snapshots";
    public bool IsSettings => Page == "Settings";

    [RelayCommand] private void ShowOverview() => Page = "Overview";
    [RelayCommand] private void ShowExplorer() => Page = "Explorer";
    [RelayCommand] private void ShowTreemap() => Page = "Treemap";
    [RelayCommand] private void ShowDuplicates() => Page = "Duplicates";
    [RelayCommand] private void ShowSimilar() => Page = "Similar";
    [RelayCommand] private void ShowSearch() => Page = "Search";
    [RelayCommand] private void ShowSnapshots() => Page = "Snapshots";
    [RelayCommand] private void ShowSettings() => Page = "Settings";

    // ---------------------------------------------------------------- sidebar

    public string BrandName => "DiskGeek";
    public string BrandBy => "by TechyGeeksHome";

    public string VersionText =>
        $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    /// <summary>What the header shows above the status line, per screen.</summary>
    public string PageTitle => Page switch
    {
        "Overview" => "Overview",
        "Explorer" => "Explorer",
        "Treemap" => "Treemap",
        "Duplicates" => "Duplicates",
        "Similar" => "Similar images",
        "Search" => "Search",
        "Snapshots" => "Snapshots",
        "Settings" => "Settings",
        _ => "DiskGeek"
    };

    /// <summary>
    /// The one line under the title. Every app in the range states what was found and what was
    /// changed; for DiskGeek that means the scan target and its totals, never a bare "Ready".
    /// </summary>
    public string PageStatus => Page switch
    {
        "Duplicates" => Duplicates.HasResults
            ? $"{DuplicateGroupCount} groups, byte-for-byte identical · {DuplicateReclaimDisplay} recoverable"
            : "Nothing found yet. Finding duplicates measures and changes nothing.",
        "Similar" => SimilarImages.HasResults
            ? $"{SimilarGroupCount} groups of visually similar images · {SimilarReclaimDisplay} recoverable"
            : "Nothing found yet. Finding similar images measures and changes nothing.",
        "Settings" => "What DiskGeek will and will not do, in plain words.",
        _ => ScannedSummary
    };

    // ---------------------------------------------------------------- overview figures

    public bool HasScan => RootNodes.Count > 0;

    private FileSystemNodeViewModel? Root => RootNodes.Count > 0 ? RootNodes[0] : null;

    public string ScannedSummary => Root is null
        ? "Nothing scanned yet. Pick a folder or a drive and press Scan - it measures, and changes nothing."
        : $"{Root.FullPath} · {TotalFiles:n0} files in {TotalFolders:n0} folders";

    public string TotalSizeDisplay => Root is null ? "-" : ByteSizeFormatter.Format(Root.SizeInBytes);

    public long TotalFiles => Root?.FileCount ?? 0;

    // Counted off the scanned model tree, not the view models. FileSystemNodeViewModel builds its
    // children lazily on first access, so walking Children here would allocate a view model for
    // every node in the scan - four hundred thousand of them on a full C:\ - purely to produce two
    // numbers on the Overview card. Counted once per scan and cached; RefreshShellFigures clears it.
    private (long Folders, long Denied)? _treeCounts;

    private (long Folders, long Denied) TreeCounts
    {
        get
        {
            if (_treeCounts is { } cached) return cached;
            var result = Root is null ? (0L, 0L) : Count(Root.Model);
            _treeCounts = result;
            return result;
        }
    }

    private static (long Folders, long Denied) Count(FileSystemNode node)
    {
        long folders = node.IsDirectory ? 1 : 0;
        long denied = node.AccessDenied ? 1 : 0;

        foreach (var child in node.Children)
        {
            var (f, d) = Count(child);
            folders += f;
            denied += d;
        }

        return (folders, denied);
    }

    public long TotalFolders => TreeCounts.Folders;

    /// <summary>Folders the scan could not read. Shown because it is the honest caveat on the total.</summary>
    public long UnreadableCount => TreeCounts.Denied;

    public int DuplicateGroupCount => Duplicates.Groups.Count;
    public int SimilarGroupCount => SimilarImages.Groups.Count;

    // Both group models already define what "wasted" means for their own kind of match -
    // exact duplicates count every copy after the first, similar images count everything
    // after the largest. Use theirs rather than a second definition that could disagree.
    public long DuplicateReclaimBytes => Duplicates.Groups.Sum(g => g.Model.WastedBytes);

    public long SimilarReclaimBytes => SimilarImages.Groups.Sum(g => g.Model.WastedBytes);

    public string DuplicateReclaimDisplay => ByteSizeFormatter.Format(DuplicateReclaimBytes);
    public string SimilarReclaimDisplay => ByteSizeFormatter.Format(SimilarReclaimBytes);
    public string TotalReclaimDisplay => ByteSizeFormatter.Format(DuplicateReclaimBytes + SimilarReclaimBytes);

    public bool HasDuplicateReclaim => DuplicateReclaimBytes > 0;
    public bool HasSimilarReclaim => SimilarReclaimBytes > 0;

    /// <summary>The biggest six children of the scan root, for the Overview table.</summary>
    public ObservableCollection<FileSystemNodeViewModel> BiggestChildren
    {
        get
        {
            var list = new ObservableCollection<FileSystemNodeViewModel>();
            if (Root is null) return list;
            foreach (var c in Root.Children.OrderByDescending(c => c.SizeInBytes).Take(6))
                list.Add(c);
            return list;
        }
    }

    /// <summary>
    /// Called after a scan, a duplicate find or a delete, so the sidebar figures and the
    /// Overview table follow the data rather than going stale.
    /// </summary>
    public void RefreshShellFigures()
    {
        _treeCounts = null;
        OnPropertyChanged(nameof(HasScan));
        OnPropertyChanged(nameof(ScannedSummary));
        OnPropertyChanged(nameof(PageStatus));
        OnPropertyChanged(nameof(TotalSizeDisplay));
        OnPropertyChanged(nameof(TotalFiles));
        OnPropertyChanged(nameof(TotalFolders));
        OnPropertyChanged(nameof(UnreadableCount));
        OnPropertyChanged(nameof(DuplicateGroupCount));
        OnPropertyChanged(nameof(SimilarGroupCount));
        OnPropertyChanged(nameof(DuplicateReclaimDisplay));
        OnPropertyChanged(nameof(SimilarReclaimDisplay));
        OnPropertyChanged(nameof(TotalReclaimDisplay));
        OnPropertyChanged(nameof(HasDuplicateReclaim));
        OnPropertyChanged(nameof(HasSimilarReclaim));
        OnPropertyChanged(nameof(BiggestChildren));
    }
}
