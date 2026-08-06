using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DiskGeek.App.ViewModels;
using DiskGeek.App.Views;

namespace DiskGeek.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext ??= new MainWindowViewModel();
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder to scan",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(path) && DataContext is MainWindowViewModel vm)
        {
            vm.FolderPath = path;
        }
    }

    private void OnTreemapNodeClicked(object? sender, ViewModels.FileSystemNodeViewModel node)
    {
        if (DataContext is MainWindowViewModel vm && node.IsDirectory)
        {
            vm.SelectedNode = node;
        }
    }

    private async void OnDeleteSelectedDuplicatesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var selected = vm.Duplicates.GetSelectedEntries();
        if (selected.Count == 0)
            return;

        var recycleBinNote = OperatingSystem.IsWindows()
            ? "Deleted files will be sent to the Recycle Bin, so this is recoverable if you change your mind."
            : "This will PERMANENTLY delete the files — there's no Recycle Bin equivalent on this platform.";

        var warning = vm.Duplicates.AnyGroupFullySelected
            ? "Warning: at least one group has every copy selected. That would delete the file entirely, not just the extra copies."
            : null;

        var confirmed = await ConfirmDialog.ShowAsync(
            this,
            title: $"Delete {selected.Count:N0} file(s)?",
            message: $"{recycleBinNote}\n\nTotal space freed: {vm.Duplicates.SelectedBytesDisplay}.",
            warning: warning);

        if (!confirmed)
            return;

        vm.Duplicates.DeleteEntries(selected);
    }

    private async void OnDeleteSelectedSimilarImagesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var selected = vm.SimilarImages.GetSelectedEntries();
        if (selected.Count == 0)
            return;

        var recycleBinNote = OperatingSystem.IsWindows()
            ? "Deleted files will be sent to the Recycle Bin, so this is recoverable if you change your mind."
            : "This will PERMANENTLY delete the files — there's no Recycle Bin equivalent on this platform.";

        var warning = vm.SimilarImages.AnyGroupFullySelected
            ? "Warning: at least one group has every copy selected. That would delete the image entirely, not just the extra copies. Similar images aren't guaranteed to be byte-identical, so double-check before deleting the last copy."
            : "Reminder: these images look alike but may not be byte-identical - review each group before deleting.";

        var confirmed = await ConfirmDialog.ShowAsync(
            this,
            title: $"Delete {selected.Count:N0} image(s)?",
            message: $"{recycleBinNote}\n\nTotal space freed: {vm.SimilarImages.SelectedBytesDisplay}.",
            warning: warning);

        if (!confirmed)
            return;

        vm.SimilarImages.DeleteEntries(selected);
    }

    /// <summary>
    /// Opens the update's download URL in the user's default browser rather than downloading or
    /// installing anything itself - see the remarks on <see cref="ViewModels.UpdateCheckViewModel"/>
    /// for why this feature stops there deliberately.
    /// </summary>
    private void OnDownloadUpdateClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { Updates.DownloadUrl: { } url }) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Updates.StatusText = $"Couldn't open the download link automatically: {ex.Message}. It's {url}";
                vm.Updates.ShowStatusMessage = true;
            }
        }
    }

    private async void OnPermissionsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SelectedNode: { } node })
            return;

        await PermissionsDialog.ShowAsync(this, node.FullPath, node.IsDirectory);
    }

    private async void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        await AboutDialog.ShowAsync(this);
    }

    private async void OnExportCsvClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var path = await PickSaveFileAsync("Export to CSV", "report.csv", "CSV file", "csv");
        if (path is null) return;

        vm.ExportCsv(path);
    }

    private async void OnExportHtmlClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var path = await PickSaveFileAsync("Export to HTML report", "report.html", "HTML file", "html");
        if (path is null) return;

        vm.ExportHtml(path);
    }

    private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is not DataGrid { SelectedItem: SearchResultViewModel result }) return;

        vm.Search.ActivateResult(result);
    }

    private async void OnBatchRenameSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var selectedFiles = SearchResultsGrid.SelectedItems
            .OfType<SearchResultViewModel>()
            .Where(r => !r.IsDirectory) // renaming folders that other results still point inside of gets confusing fast - files only, matching the safest Total Commander MRT usage
            .Select(r => r.Node)
            .ToList();

        if (selectedFiles.Count == 0)
        {
            vm.Search.StatusText = "Select one or more files in the results (not folders) first.";
            return;
        }

        var outcome = await BatchRenameDialog.ShowAsync(this, selectedFiles);
        if (outcome.AnyApplied)
        {
            // Patches the live tree in place (List/Treemap/tree all update immediately) and drops
            // the now-stale rows from the Search results themselves.
            vm.PatchTreeAfterRename(outcome.Applied);
            vm.Search.StatusText = $"Renamed {outcome.Applied.Count:N0} file(s) — tree, list, and treemap updated.";
        }
    }

    private async void OnSaveSnapshotClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var suggestedName = vm.RootNodes.Count > 0
            ? $"{vm.RootNodes[0].Name}-{DateTime.Now:yyyyMMdd-HHmm}.diskscan.json"
            : "snapshot.diskscan.json";

        var path = await PickSaveFileAsync("Save snapshot", suggestedName, "DiskGeek snapshot", "json");
        if (path is null) return;

        await vm.Snapshots.SaveSnapshotAsync(path);
    }

    private async void OnLoadBaselineClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load snapshot baseline",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("DiskGeek snapshot") { Patterns = new[] { "*.json" } } }
        });

        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        await vm.Snapshots.LoadBaselineAsync(path);
    }

    private void OnCompareSnapshotClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.Snapshots.Compare();
    }

    private async Task<string?> PickSaveFileAsync(string title, string suggestedName, string typeName, string extension)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return null;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(typeName) { Patterns = new[] { $"*.{extension}" } } }
        });

        return file?.TryGetLocalPath();
    }
}
