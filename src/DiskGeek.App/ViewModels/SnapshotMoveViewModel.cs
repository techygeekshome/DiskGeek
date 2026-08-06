using DiskGeek.Core.Formatting;
using DiskGeek.Core.Snapshots;

namespace DiskGeek.App.ViewModels;

/// <summary>One row in a snapshot-compare results list representing a moved and/or renamed item.</summary>
public sealed class SnapshotMoveViewModel
{
    public SnapshotMove Model { get; }

    public SnapshotMoveViewModel(SnapshotMove model) => Model = model;

    public string Icon => Model.IsDirectory ? "📁" : "📄";
    public string SizeDisplay => ByteSizeFormatter.Format(Model.SizeBytes);

    /// <summary>Old name -> new name if the name changed, otherwise just the (unchanged) name.</summary>
    public string NameDisplay => Model.NameChanged ? $"{Model.OldName} → {Model.NewName}" : Model.NewName;

    /// <summary>Old path -> new path, shown as a tooltip since full paths get long.</summary>
    public string PathTooltip => $"{Model.OldFullPath}\n→ {Model.NewFullPath}";

    /// <summary>
    /// Whether the containing folder changed, not just the leaf name (full-path equality alone
    /// can't tell us this, since the path always differs when the name does).
    /// </summary>
    private bool FolderChanged => !string.Equals(
        System.IO.Path.GetDirectoryName(Model.OldFullPath),
        System.IO.Path.GetDirectoryName(Model.NewFullPath),
        StringComparison.OrdinalIgnoreCase);

    public string KindDisplay => Model.NameChanged && FolderChanged
        ? "Moved & renamed"
        : Model.NameChanged
            ? "Renamed"
            : "Moved";
}
