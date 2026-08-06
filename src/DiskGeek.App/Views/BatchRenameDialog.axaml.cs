using Avalonia.Controls;
using Avalonia.Interactivity;
using DiskGeek.App.ViewModels;
using DiskGeek.Core.Models;
using DiskGeek.Core.Renaming;

namespace DiskGeek.App.Views;

/// <summary>Result of a batch-rename dialog session, letting the caller patch its own view of the scan tree afterward.</summary>
public sealed record BatchRenameOutcome(bool AnyApplied, IReadOnlyList<(FileSystemNode Node, string NewFullPath)> Applied)
{
    public static readonly BatchRenameOutcome None = new(false, Array.Empty<(FileSystemNode, string)>());
}

/// <summary>
/// Batch-renames a fixed set of files (e.g. from a Search result selection): find/replace,
/// prefix/suffix, and an optional counter, with a live preview and per-row error flags before
/// anything on disk actually changes.
/// </summary>
public partial class BatchRenameDialog : Window
{
    private IReadOnlyList<FileSystemNode> _files = Array.Empty<FileSystemNode>();
    private IReadOnlyList<string> _filePaths = Array.Empty<string>();

    public BatchRenameOutcome Outcome { get; private set; } = BatchRenameOutcome.None;

    public BatchRenameDialog()
    {
        InitializeComponent();
    }

    public static async Task<BatchRenameOutcome> ShowAsync(Window owner, IReadOnlyList<FileSystemNode> files)
    {
        var dialog = new BatchRenameDialog { _files = files, _filePaths = files.Select(f => f.FullPath).ToList() };
        dialog.UpdatePreview();
        var confirmed = await dialog.ShowDialog<bool>(owner);
        return confirmed ? dialog.Outcome : BatchRenameOutcome.None;
    }

    private void OnUpdatePreviewClicked(object? sender, RoutedEventArgs e) => UpdatePreview();

    private RenameOptions BuildOptions() => new()
    {
        FindText = string.IsNullOrEmpty(FindBox.Text) ? null : FindBox.Text,
        FindIsRegex = RegexCheck.IsChecked == true,
        ReplaceText = ReplaceBox.Text ?? string.Empty,
        Prefix = PrefixBox.Text,
        Suffix = SuffixBox.Text,
        UseCounter = CounterCheck.IsChecked == true,
        CounterStart = 1,
        CounterDigits = Math.Max(1, _filePaths.Count.ToString().Length)
    };

    private List<RenamePreviewEntry>? _currentPreview;

    private void UpdatePreview()
    {
        try
        {
            _currentPreview = BatchRenamer.Preview(_filePaths, BuildOptions()).ToList();
            PreviewList.ItemsSource = _currentPreview.Select(p => new RenamePreviewRowViewModel(p)).ToList();

            var errorCount = _currentPreview.Count(p => p.HasError);
            SummaryText.Text = errorCount == 0
                ? $"{_currentPreview.Count:N0} file(s) ready to rename."
                : $"{_currentPreview.Count:N0} file(s) — {errorCount:N0} have a problem and will be skipped (shown in grey/red below).";
            ApplyButton.IsEnabled = _currentPreview.Any(p => !p.HasError);
        }
        catch (ArgumentException ex)
        {
            // Most likely an invalid regex pattern - report it inline rather than crashing the dialog.
            SummaryText.Text = $"Invalid pattern: {ex.Message}";
            PreviewList.ItemsSource = null;
            ApplyButton.IsEnabled = false;
        }
    }

    private void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentPreview is null) return;

        var result = BatchRenamer.Apply(_currentPreview);

        // Map each successful (From, To) pair back to the original FileSystemNode it came from, so
        // the caller can patch its own in-memory tree without needing to re-search by path.
        var nodesByPath = _files.ToDictionary(f => f.FullPath, StringComparer.OrdinalIgnoreCase);
        var applied = result.Renamed
            .Where(r => nodesByPath.ContainsKey(r.From))
            .Select(r => (Node: nodesByPath[r.From], NewFullPath: r.To))
            .ToList();

        Outcome = new BatchRenameOutcome(applied.Count > 0, applied);

        SummaryText.Text = result.AllSucceeded
            ? $"Renamed {result.Renamed.Count:N0} file(s)."
            : $"Renamed {result.Renamed.Count:N0} file(s); {result.Failed.Count:N0} could not be renamed.";

        Close(true);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
