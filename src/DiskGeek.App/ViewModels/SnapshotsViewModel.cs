using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;
using DiskGeek.Core.Snapshots;

namespace DiskGeek.App.ViewModels;

/// <summary>
/// Drives the Snapshots tab: save the current scan to a .diskscan file, load a previously-saved
/// one back as a baseline, and compare it against whatever's currently scanned to see what grew,
/// shrank, appeared, or disappeared since ("what changed since last week").
/// </summary>
public partial class SnapshotsViewModel : ObservableObject
{
    private readonly ISnapshotService _service = new SnapshotService();
    private FileSystemNode? _scanRoot;
    private ScanSnapshot? _baseline;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Scan a folder, then save a snapshot or load one to compare against.";

    [ObservableProperty]
    private string? _baselineLabel;

    public ObservableCollection<SnapshotChangeViewModel> Added { get; } = new();
    public ObservableCollection<SnapshotChangeViewModel> Removed { get; } = new();
    public ObservableCollection<SnapshotChangeViewModel> Changed { get; } = new();
    public ObservableCollection<SnapshotMoveViewModel> Moved { get; } = new();

    public bool HasComparison => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0 || Moved.Count > 0;
    public bool CanSaveSnapshot => !IsBusy && _scanRoot is not null;
    public bool CanCompare => !IsBusy && _scanRoot is not null && _baseline is not null;

    public string? NetChangeDisplay { get; private set; }

    public void SetScanRoot(FileSystemNode? root)
    {
        _scanRoot = root;
        OnPropertyChanged(nameof(CanSaveSnapshot));
        OnPropertyChanged(nameof(CanCompare));
    }

    public async Task SaveSnapshotAsync(string filePath)
    {
        if (_scanRoot is null) return;

        IsBusy = true;
        StatusText = "Saving snapshot…";
        try
        {
            await _service.SaveAsync(_scanRoot, filePath);
            StatusText = $"Snapshot saved to {filePath}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save snapshot: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadBaselineAsync(string filePath)
    {
        IsBusy = true;
        StatusText = "Loading snapshot…";
        try
        {
            _baseline = await _service.LoadAsync(filePath);
            BaselineLabel = $"{Path.GetFileName(filePath)} — taken {_baseline.TakenUtc.ToLocalTime():g}, root: {_baseline.RootPath}";
            StatusText = "Baseline loaded. Scan the same folder again (or use the current scan) and click Compare.";
            OnPropertyChanged(nameof(CanCompare));
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load snapshot: {ex.Message}";
            _baseline = null;
            BaselineLabel = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Compare()
    {
        if (_scanRoot is null || _baseline is null) return;

        if (!string.Equals(_baseline.RootPath, _scanRoot.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = $"Warning: baseline was taken of '{_baseline.RootPath}' but the current scan is of " +
                         $"'{_scanRoot.FullPath}' — comparing anyway, but results may not make sense.";
        }

        var currentSnapshot = SnapshotService.ToSnapshotNode(_scanRoot);
        var comparison = _service.Compare(_baseline.Root, currentSnapshot);

        Added.Clear();
        foreach (var a in comparison.Added) Added.Add(new SnapshotChangeViewModel(a));

        Removed.Clear();
        foreach (var r in comparison.Removed) Removed.Add(new SnapshotChangeViewModel(r));

        Changed.Clear();
        foreach (var c in comparison.Changed) Changed.Add(new SnapshotChangeViewModel(c));

        Moved.Clear();
        foreach (var m in comparison.Moved) Moved.Add(new SnapshotMoveViewModel(m));

        var sign = comparison.NetDeltaBytes >= 0 ? "+" : "-";
        NetChangeDisplay = $"{sign}{ByteSizeFormatter.Format(Math.Abs(comparison.NetDeltaBytes))}";
        OnPropertyChanged(nameof(NetChangeDisplay));

        StatusText = $"{Added.Count:N0} added, {Removed.Count:N0} removed, {Changed.Count:N0} changed, " +
                     $"{Moved.Count:N0} moved/renamed. Net change: {NetChangeDisplay}.";
        OnPropertyChanged(nameof(HasComparison));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveSnapshot));
        OnPropertyChanged(nameof(CanCompare));
    }
}
