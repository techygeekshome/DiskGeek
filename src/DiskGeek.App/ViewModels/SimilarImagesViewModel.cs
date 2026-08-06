using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskGeek.Core.Duplicates;
using DiskGeek.Core.FileOperations;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.ImageSimilarity;
using DiskGeek.Core.Models;

namespace DiskGeek.App.ViewModels;

/// <summary>
/// Drives the "Similar Images" mode of the Duplicates tab: finds photos that look alike (near-
/// identical, not necessarily byte-for-byte identical) using perceptual hashing — catches the same
/// photo saved at two qualities, resized copies, and near-identical burst shots that exact
/// duplicate detection can't, since the underlying bytes genuinely differ.
/// </summary>
public partial class SimilarImagesViewModel : ObservableObject
{
    private readonly IImageSimilarityFinder _finder = new ImageSimilarityFinder();
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Scan a folder first, then click \"Find Similar Images\".";

    [ObservableProperty]
    private long _selectedBytes;

    /// <summary>
    /// How different two images are allowed to look and still count as "similar" (0-64, a Hamming
    /// distance over a 64-bit perceptual hash). Low values (a handful) only catch near-exact matches
    /// like re-compressed copies; higher values start catching genuinely different-looking photos,
    /// so the default sits at a level tuned to catch real near-duplicates without much noise.
    /// </summary>
    [ObservableProperty]
    private int _sensitivity = 10;

    public ObservableCollection<SimilarImageGroupViewModel> Groups { get; } = new();

    public bool HasResults => Groups.Count > 0;

    public string SelectedBytesDisplay => ByteSizeFormatter.Format(SelectedBytes);
    public bool HasSelection => SelectedBytes > 0;

    /// <summary>True if any group has every one of its copies selected — deleting that group would lose the image entirely.</summary>
    public bool AnyGroupFullySelected => Groups.Any(g => g.AllFilesSelected);

    public async Task RunAsync(FileSystemNode scanRoot)
    {
        IsScanning = true;
        Groups.Clear();
        SelectedBytes = 0;
        StatusText = "Scanning images and comparing how they look…";

        _cts = new CancellationTokenSource();

        var progress = new Progress<DuplicateScanProgress>(p =>
        {
            if (!IsScanning) return;
            StatusText = p.FilesToHash == 0
                ? "Scanning images and comparing how they look…"
                : $"Comparing images… {p.FilesHashed:N0} / {p.FilesToHash:N0}";
        });

        try
        {
            var result = await _finder.FindSimilarImagesAsync(scanRoot, Sensitivity, progress, _cts.Token);

            foreach (var group in result.Groups)
            {
                var groupVm = new SimilarImageGroupViewModel(group);
                foreach (var entry in groupVm.Files)
                    entry.PropertyChanged += OnEntrySelectionChanged;
                Groups.Add(groupVm);
            }

            RecomputeSelectedBytes();

            var totalWasted = result.Groups.Sum(g => g.WastedBytes);
            var baseStatus = result.Groups.Count == 0
                ? $"No similar images found (checked {result.CandidatesConsidered:N0} of {result.TotalImageFilesFound:N0} image file(s))."
                : $"Found {result.Groups.Count:N0} group(s) of similar images, wasting up to {ByteSizeFormatter.Format(totalWasted)}.";

            // Comparison cost grows with the square of the candidate count, so very large photo
            // collections are capped (see ImageSimilarityFinder.MaxCandidates) — say so plainly
            // rather than silently only checking part of the folder.
            StatusText = result.CandidatesTruncated
                ? $"{baseStatus} Only the {result.CandidatesConsidered:N0} most recently modified images (of {result.TotalImageFilesFound:N0} found) were compared — this folder has too many images to compare all at once."
                : baseStatus;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Similar-image scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Similar-image scan failed: {ex.Message}";
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
    /// in place — same event shape as <see cref="DuplicatesViewModel.FilesDeleted"/>, handled the
    /// same way by the same subscriber.
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

            // Fewer than two remaining copies means it's no longer a "similar" group.
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
