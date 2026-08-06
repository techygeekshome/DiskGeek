using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskGeek.Core.Duplicates;
using DiskGeek.Core.FileOperations;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;

namespace DiskGeek.App.ViewModels;

public partial class DuplicatesViewModel : ObservableObject
{
    private readonly IDuplicateFinder _finder = new DuplicateFinder();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Scan a folder first, then click \"Find Duplicates\".";

    [ObservableProperty]
    private long _selectedBytes;

    public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = new();

    public bool HasResults => Groups.Count > 0;

    public string SelectedBytesDisplay => ByteSizeFormatter.Format(SelectedBytes);
    public bool HasSelection => SelectedBytes > 0;

    /// <summary>True if any group has every one of its copies selected — deleting that group would lose the file entirely.</summary>
    public bool AnyGroupFullySelected => Groups.Any(g => g.AllFilesSelected);

    public async Task RunAsync(FileSystemNode scanRoot)
    {
        IsScanning = true;
        Groups.Clear();
        SelectedBytes = 0;
        StatusText = "Hashing files to find duplicates…";

        _cts = new CancellationTokenSource();

        var progress = new Progress<DuplicateScanProgress>(p =>
        {
            if (!IsScanning) return; // see MainWindowViewModel for why this guard matters
            StatusText = p.FilesToHash == 0
                ? "Hashing files to find duplicates…"
                : $"Hashing… {p.FilesHashed:N0} / {p.FilesToHash:N0} candidate files";
        });

        try
        {
            var groups = await _finder.FindDuplicatesAsync(scanRoot, progress, _cts.Token);

            foreach (var group in groups)
            {
                var groupVm = new DuplicateGroupViewModel(group);
                foreach (var entry in groupVm.Files)
                    entry.PropertyChanged += OnEntrySelectionChanged;
                Groups.Add(groupVm);
            }

            RecomputeSelectedBytes();

            var totalWasted = groups.Sum(g => g.WastedBytes);
            StatusText = groups.Count == 0
                ? "No duplicate files found."
                : $"Found {groups.Count:N0} duplicate group(s), wasting {ByteSizeFormatter.Format(totalWasted)}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Duplicate scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Duplicate scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(HasResults));
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private void OnEntrySelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DuplicateFileEntryViewModel.IsSelected))
        {
            RecomputeSelectedBytes();
            OnPropertyChanged(nameof(AnyGroupFullySelected));
        }
    }

    private void RecomputeSelectedBytes()
    {
        SelectedBytes = Groups.SelectMany(g => g.Files).Where(f => f.IsSelected).Sum(f => f.Node.SizeInBytes);
        OnPropertyChanged(nameof(SelectedBytesDisplay));
        OnPropertyChanged(nameof(HasSelection));
    }

    public IReadOnlyList<DuplicateFileEntryViewModel> GetSelectedEntries() =>
        Groups.SelectMany(g => g.Files).Where(f => f.IsSelected).ToList();

    /// <summary>
    /// Raised after a successful delete with the underlying <see cref="FileSystemNode"/>s that were
    /// actually removed from disk, so a listener (MainWindowViewModel) can patch the live scan tree
    /// in place rather than requiring a re-scan to see correct sizes elsewhere in the app.
    /// </summary>
    public event EventHandler<IReadOnlyList<FileSystemNode>>? FilesDeleted;

    /// <summary>Deletes the given entries (Recycle Bin on Windows, permanent elsewhere - see SafeFileDeleter), then removes them from the results.</summary>
    public DeleteResult DeleteEntries(IReadOnlyList<DuplicateFileEntryViewModel> entries)
    {
        var result = SafeFileDeleter.DeleteFiles(entries.Select(e => e.FullPath));
        var deletedPaths = new HashSet<string>(result.Deleted, StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups.ToList())
        {
            foreach (var entry in group.Files.Where(f => deletedPaths.Contains(f.FullPath)).ToList())
            {
                entry.PropertyChanged -= OnEntrySelectionChanged;
                group.Files.Remove(entry);
            }

            // Fewer than two remaining copies means it's no longer a "duplicate" group.
            if (group.Files.Count < 2)
                Groups.Remove(group);
        }

        RecomputeSelectedBytes();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(AnyGroupFullySelected));

        var deletedNodes = entries.Where(e => deletedPaths.Contains(e.FullPath)).Select(e => e.Node).ToList();

        StatusText = result.AllSucceeded
            ? $"Deleted {result.Deleted.Count:N0} file(s), freed {ByteSizeFormatter.Format(deletedNodes.Sum(n => n.SizeInBytes))}."
            : $"Deleted {result.Deleted.Count:N0} file(s); {result.Failed.Count:N0} failed.";

        if (deletedNodes.Count > 0)
            FilesDeleted?.Invoke(this, deletedNodes);

        return result;
    }
}
