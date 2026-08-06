using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskGeek.Core.Duplicates;
using DiskGeek.Core.Formatting;

namespace DiskGeek.App.ViewModels;

/// <summary>A duplicate group, with the oldest copy left unchecked ("the original") and the rest pre-selected for deletion.</summary>
public sealed class DuplicateGroupViewModel
{
    public DuplicateGroup Model { get; }
    public ObservableCollection<DuplicateFileEntryViewModel> Files { get; }

    public DuplicateGroupViewModel(DuplicateGroup model)
    {
        Model = model;

        // DuplicateFinder already orders each group's Files oldest-first, so index 0 is the one
        // we default to keeping - the rest are pre-checked as the likely-safe-to-delete copies.
        Files = new ObservableCollection<DuplicateFileEntryViewModel>(
            model.Files.Select((f, i) => new DuplicateFileEntryViewModel(f, isSelected: i > 0)));
    }

    public string SizeDisplay => ByteSizeFormatter.Format(Model.SizeInBytes);
    public string WastedDisplay => ByteSizeFormatter.Format(Model.WastedBytes);
    public int CopyCount => Model.Files.Count;
    public bool AllFilesSelected => Files.All(f => f.IsSelected);
}
