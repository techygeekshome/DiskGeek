using DiskGeek.Core.Formatting;
using DiskGeek.Core.Snapshots;

namespace DiskGeek.App.ViewModels;

/// <summary>One row in a snapshot-compare results list (added, removed, or grew/shrank).</summary>
public sealed class SnapshotChangeViewModel
{
    public SnapshotEntryChange Model { get; }

    public SnapshotChangeViewModel(SnapshotEntryChange model) => Model = model;

    public string Name => Model.Name;
    public string FullPath => Model.FullPath;
    public string Icon => Model.IsDirectory ? "📁" : "📄";

    public string DeltaDisplay
    {
        get
        {
            var sign = Model.DeltaBytes >= 0 ? "+" : "-";
            return $"{sign}{ByteSizeFormatter.Format(Math.Abs(Model.DeltaBytes))}";
        }
    }

    public string SizeDisplay => Model.OldSizeBytes == 0
        ? ByteSizeFormatter.Format(Model.NewSizeBytes)
        : Model.NewSizeBytes == 0
            ? ByteSizeFormatter.Format(Model.OldSizeBytes)
            : $"{ByteSizeFormatter.Format(Model.OldSizeBytes)} → {ByteSizeFormatter.Format(Model.NewSizeBytes)}";
}
