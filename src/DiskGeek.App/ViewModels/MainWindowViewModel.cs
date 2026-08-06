using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskGeek.Core.Export;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;
using DiskGeek.Core.Mutation;
using DiskGeek.Core.Scanning;

namespace DiskGeek.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDirectoryScanner _scanner = new DirectoryScanner();
    private readonly Stopwatch _scanStopwatch = new();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Pick a folder and click Scan to get started.";

    [ObservableProperty]
    private string _elapsedDisplay = string.Empty;

    [ObservableProperty]
    private string _windowTitle = AppTitle;

    /// <summary>
    /// Read from the assembly's informational/file version (set via the App project's
    /// &lt;Version&gt; in the .csproj) rather than hard-coded, so a version bump there is the only
    /// place that needs editing - shown in the window title so a user glancing at the taskbar or
    /// title bar can tell whether they're running the build they think they are, and fed to
    /// <see cref="Updates"/> as the version to check for newer releases against.
    /// </summary>
    public static readonly Version CurrentVersion =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    private static readonly string AppTitle = BuildAppTitle();

    private static string BuildAppTitle() =>
        $"DiskGeek v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private FileSystemNodeViewModel? _selectedNode;

    public ObservableCollection<FileSystemNodeViewModel> RootNodes { get; } = new();

    public DuplicatesViewModel Duplicates { get; } = new();

    public SimilarImagesViewModel SimilarImages { get; } = new();

    public SearchViewModel Search { get; }

    public SnapshotsViewModel Snapshots { get; }

    public UpdateCheckViewModel Updates { get; } = new(CurrentVersion);

    public MainWindowViewModel()
    {
        Search = new SearchViewModel();
        Search.ResultActivated += (_, node) => RevealNode(node);

        Snapshots = new SnapshotsViewModel();

        Duplicates.FilesDeleted += (_, deletedNodes) => PatchTreeAfterDelete(deletedNodes);
        SimilarImages.FilesDeleted += (_, deletedNodes) => PatchTreeAfterDelete(deletedNodes);

        // Best-effort, silent, fire-and-forget: never blocks startup and never surfaces an error
        // just because the machine happens to be offline right now - see the remarks on
        // UpdateCheckViewModel.CheckSilentlyAsync for why that specifically matters here.
        _ = Updates.CheckSilentlyAsync();
    }

    private static readonly ObservableCollection<FileSystemNodeViewModel> Empty = new();

    /// <summary>Children of whatever is selected in the tree, or the scan root if nothing is selected yet.</summary>
    public ObservableCollection<FileSystemNodeViewModel> DetailItems =>
        SelectedNode?.Children ?? (RootNodes.Count > 0 ? RootNodes[0].Children : Empty);

    partial void OnSelectedNodeChanged(FileSystemNodeViewModel? value) => OnPropertyChanged(nameof(DetailItems));

    public bool CanScan => !IsScanning && !string.IsNullOrWhiteSpace(FolderPath);

    public bool CanFindDuplicates => !IsScanning && RootNodes.Count > 0;

    public bool CanExport => !IsScanning && RootNodes.Count > 0;

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanFindDuplicates));
        OnPropertyChanged(nameof(CanExport));
        ScanCommand.NotifyCanExecuteChanged();
        FindDuplicatesCommand.NotifyCanExecuteChanged();
        FindSimilarImagesCommand.NotifyCanExecuteChanged();
    }

    partial void OnFolderPathChanged(string value)
    {
        OnPropertyChanged(nameof(CanScan));
        ScanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            StatusText = $"Folder not found: {FolderPath}";
            return;
        }

        IsScanning = true;
        RootNodes.Clear();
        SelectedNode = null;
        OnPropertyChanged(nameof(DetailItems));
        StatusText = $"Scanning {FolderPath} — starting…";
        ElapsedDisplay = "0s";
        WindowTitle = $"{AppTitle} — Scanning…";
        _scanStopwatch.Restart();

        _cts = new CancellationTokenSource();

        var progressReporter = new Progress<ScanProgress>(p =>
        {
            // Progress<T> marshals callbacks via Post, which is asynchronous: the scanner's final,
            // un-throttled report can end up dispatched *after* this method has already set the
            // "Done" status text below, clobbering it back to an in-progress message. Once scanning
            // has actually finished there's nothing useful left for a progress update to say.
            if (!IsScanning) return;

            ElapsedDisplay = FormatElapsed(_scanStopwatch.Elapsed);
            StatusText = $"Scanning… {p.FilesScanned:N0} files, {p.DirectoriesScanned:N0} folders, " +
                         $"{ByteSizeFormatter.Format(p.BytesScanned)} so far";
            WindowTitle = $"{AppTitle} — Scanning… ({ElapsedDisplay})";
        });

        try
        {
            var root = await _scanner.ScanAsync(FolderPath, progressReporter, _cts.Token);
            var rootVm = new FileSystemNodeViewModel(root, root.SizeInBytes);
            RootNodes.Add(rootVm);
            SelectedNode = rootVm;
            OnPropertyChanged(nameof(CanFindDuplicates));
            FindDuplicatesCommand.NotifyCanExecuteChanged();
            FindSimilarImagesCommand.NotifyCanExecuteChanged();
            Search.SetScanRoot(root);
            Snapshots.SetScanRoot(root);
            OnPropertyChanged(nameof(CanExport));
            StatusText = root.AccessDenied
                ? $"Done with some folders skipped (access denied) — {root.FileCount:N0} files, {ByteSizeFormatter.Format(root.SizeInBytes)} total, took {FormatElapsed(_scanStopwatch.Elapsed)}."
                : $"Done — {root.FileCount:N0} files, {ByteSizeFormatter.Format(root.SizeInBytes)} total, took {FormatElapsed(_scanStopwatch.Elapsed)}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Scan cancelled after {FormatElapsed(_scanStopwatch.Elapsed)}.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusText = "Access denied to that folder.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _scanStopwatch.Stop();
            IsScanning = false;
            WindowTitle = AppTitle;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanFindDuplicates))]
    private Task FindDuplicatesAsync() =>
        RootNodes.Count > 0 ? Duplicates.RunAsync(RootNodes[0].Model) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanFindDuplicates))]
    private Task FindSimilarImagesAsync() =>
        RootNodes.Count > 0 ? SimilarImages.RunAsync(RootNodes[0].Model) : Task.CompletedTask;

    /// <summary>
    /// Selects the tree node for <paramref name="target"/> (or its parent folder, for a file
    /// match, so the match shows up in the List/Treemap detail panel) — used by both Search
    /// ("locate this result") and Snapshot compare ("show me this changed item"). Walks down from
    /// the root by full path, which lazily builds only the <see cref="FileSystemNodeViewModel"/>
    /// levels actually needed rather than materializing the whole tree.
    /// </summary>
    public void RevealNode(FileSystemNode target)
    {
        if (RootNodes.Count == 0) return;

        var toSelect = target.IsDirectory || target.Parent is null ? target : target.Parent;

        var ancestry = new List<FileSystemNode>();
        for (var cursor = toSelect; cursor is not null; cursor = cursor.Parent)
            ancestry.Add(cursor);
        ancestry.Reverse(); // root ... toSelect

        if (ancestry.Count == 0 ||
            !string.Equals(RootNodes[0].FullPath, ancestry[0].FullPath, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Can't locate that item — it's from a different scan. Re-scan and try again.";
            return;
        }

        var currentVm = RootNodes[0];
        for (var i = 1; i < ancestry.Count; i++)
        {
            var next = currentVm.Children.FirstOrDefault(
                c => string.Equals(c.FullPath, ancestry[i].FullPath, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                StatusText = "Can't locate that item — it's from a different scan. Re-scan and try again.";
                return;
            }
            currentVm = next;
        }

        SelectedNode = currentVm;
    }

    /// <summary>
    /// Patches the live scan tree (Core model + already-built view models) after files were deleted
    /// elsewhere on disk (Duplicates tab), so sizes and file counts in the List/Treemap/tree update
    /// immediately instead of requiring a re-scan. Also drops the deleted files from any stale
    /// Search results still referencing them.
    /// </summary>
    private void PatchTreeAfterDelete(IReadOnlyList<FileSystemNode> deletedNodes)
    {
        if (RootNodes.Count == 0 || deletedNodes.Count == 0) return;

        var root = RootNodes[0].Model;
        var anyRemoved = false;

        foreach (var node in deletedNodes)
        {
            var parent = node.Parent;
            if (!TreeMutator.RemoveNode(node)) continue;
            anyRemoved = true;

            var parentVm = parent is null ? null : FindBuiltViewModel(parent);
            parentVm?.TryRemoveChild(node.FullPath);
            RefreshAncestorAggregates(parent);
        }

        if (!anyRemoved) return;

        RootNodes[0].UpdatePercentBaseRecursive(root.SizeInBytes);
        Search.RemoveResults(deletedNodes);
        OnPropertyChanged(nameof(DetailItems));
    }

    /// <summary>
    /// Patches the live scan tree after a batch rename (Search tab), so the renamed items' new
    /// names show up in List/Treemap/tree immediately. Sizes never change on a rename, so this only
    /// ever swaps node identities, not aggregates. Stale rows for the renamed files are dropped from
    /// Search results, since the old (pre-rename) node objects they wrap no longer exist in the tree.
    /// </summary>
    public void PatchTreeAfterRename(IReadOnlyList<(FileSystemNode Node, string NewFullPath)> renamed)
    {
        if (RootNodes.Count == 0 || renamed.Count == 0) return;

        var renamedOriginals = new List<FileSystemNode>(renamed.Count);

        foreach (var (node, newFullPath) in renamed)
        {
            if (node.Parent is null) continue; // renaming the scan root isn't supported/exposed anywhere

            var newName = System.IO.Path.GetFileName(newFullPath);
            var newNode = TreeMutator.RenameNode(node, newFullPath, newName);

            var parentVm = FindBuiltViewModel(node.Parent);
            parentVm?.TryReplaceChild(node.FullPath, newNode);
            renamedOriginals.Add(node);
        }

        Search.RemoveResults(renamedOriginals);
        OnPropertyChanged(nameof(DetailItems));
    }

    /// <summary>
    /// Walks from the root down to <paramref name="target"/> through only already-built view model
    /// levels (never forces <see cref="FileSystemNodeViewModel.Children"/> to materialize) and
    /// raises the size/count property-changed notifications on EVERY level found along the way —
    /// deleting a file changes every ancestor's aggregate size, not just its immediate parent's, so
    /// this must refresh the whole built chain, stopping only once it reaches a level that was never
    /// materialized (nothing beyond that point has stale bindings to fix).
    /// </summary>
    private void RefreshAncestorAggregates(FileSystemNode? target)
    {
        if (RootNodes.Count == 0 || target is null) return;

        var ancestry = new List<FileSystemNode>();
        for (var cursor = target; cursor is not null; cursor = cursor.Parent)
            ancestry.Add(cursor);
        ancestry.Reverse();

        if (ancestry.Count == 0 ||
            !string.Equals(RootNodes[0].FullPath, ancestry[0].FullPath, StringComparison.OrdinalIgnoreCase))
            return;

        var current = RootNodes[0];
        current.RefreshAggregates();
        for (var i = 1; i < ancestry.Count; i++)
        {
            current = current.FindBuiltChild(ancestry[i].FullPath);
            if (current is null) return;
            current.RefreshAggregates();
        }
    }

    /// <summary>
    /// Finds the already-built <see cref="FileSystemNodeViewModel"/> for <paramref name="target"/>,
    /// walking down from the root by full path without forcing any unbuilt level to materialize.
    /// Returns null if <paramref name="target"/> isn't currently realized in the view model tree
    /// (nothing to patch there — it'll reflect the mutated Core model whenever it is built) or isn't
    /// part of the current scan at all.
    /// </summary>
    private FileSystemNodeViewModel? FindBuiltViewModel(FileSystemNode? target)
    {
        if (RootNodes.Count == 0 || target is null) return null;

        var ancestry = new List<FileSystemNode>();
        for (var cursor = target; cursor is not null; cursor = cursor.Parent)
            ancestry.Add(cursor);
        ancestry.Reverse();

        if (ancestry.Count == 0 ||
            !string.Equals(RootNodes[0].FullPath, ancestry[0].FullPath, StringComparison.OrdinalIgnoreCase))
            return null;

        var current = RootNodes[0];
        for (var i = 1; i < ancestry.Count; i++)
        {
            current = current.FindBuiltChild(ancestry[i].FullPath);
            if (current is null) return null;
        }

        return current;
    }

    /// <summary>Exports every item in the current scan (folders and files, flattened) to CSV.</summary>
    public void ExportCsv(string filePath)
    {
        if (RootNodes.Count == 0) return;
        var root = RootNodes[0].Model;
        ScanExporter.ExportCsv(FlattenTree(root), filePath, root.SizeInBytes);
        StatusText = $"Exported CSV report to {filePath}.";
    }

    /// <summary>Exports every item in the current scan (folders and files, flattened) to a self-contained HTML report.</summary>
    public void ExportHtml(string filePath)
    {
        if (RootNodes.Count == 0) return;
        var root = RootNodes[0].Model;
        ScanExporter.ExportHtml($"DiskGeek report — {root.FullPath}", FlattenTree(root), filePath, root.SizeInBytes);
        StatusText = $"Exported HTML report to {filePath}.";
    }

    private static IEnumerable<FileSystemNode> FlattenTree(FileSystemNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in FlattenTree(child))
                yield return descendant;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1) return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        if (elapsed.TotalSeconds < 1) return "<1s";
        return $"{elapsed.Seconds}s";
    }
}
