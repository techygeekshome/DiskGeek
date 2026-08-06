using System.Collections.ObjectModel;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.ImageSimilarity;

namespace DiskGeek.App.ViewModels;

/// <summary>A group of visually-similar images, with the largest file left unchecked ("the one to keep") and the rest pre-selected for deletion.</summary>
public sealed class SimilarImageGroupViewModel
{
    public SimilarImageGroup Model { get; }
    public ObservableCollection<DuplicateFileEntryViewModel> Files { get; }

    public SimilarImageGroupViewModel(SimilarImageGroup model)
    {
        Model = model;

        // ImageSimilarityFinder already orders each group's Files largest-first, so index 0 is the
        // one we default to keeping (usually the highest-resolution/least-compressed copy) - the
        // rest are pre-checked as the likely-safe-to-delete near-duplicates.
        Files = new ObservableCollection<DuplicateFileEntryViewModel>(
            model.Files.Select((f, i) => new DuplicateFileEntryViewModel(f, isSelected: i > 0)));
    }

    public string WastedDisplay => ByteSizeFormatter.Format(Model.WastedBytes);
    public int CopyCount => Model.Files.Count;
    public bool AllFilesSelected => Files.All(f => f.IsSelected);
}
