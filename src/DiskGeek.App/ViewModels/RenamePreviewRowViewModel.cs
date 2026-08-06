using DiskGeek.Core.Renaming;

namespace DiskGeek.App.ViewModels;

/// <summary>Bindable wrapper around a <see cref="RenamePreviewEntry"/> for the Batch Rename dialog.</summary>
public sealed class RenamePreviewRowViewModel
{
    public RenamePreviewRowViewModel(RenamePreviewEntry model) => Model = model;

    public RenamePreviewEntry Model { get; }

    public string OriginalName => Model.OriginalName;
    public string NewName => Model.NewName;
    public string? Error => Model.Error;
    public string NameColor => Model.HasError ? "#9ba1a6" : "#1a7f37";
}
