using CommunityToolkit.Mvvm.ComponentModel;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;

namespace DiskGeek.App.ViewModels;

/// <summary>One file within a duplicate group, with a checkbox state for the delete-selection UI.</summary>
public partial class DuplicateFileEntryViewModel : ObservableObject
{
    public FileSystemNode Node { get; }

    [ObservableProperty]
    private bool _isSelected;

    public DuplicateFileEntryViewModel(FileSystemNode node, bool isSelected)
    {
        Node = node;
        _isSelected = isSelected;
    }

    public string FullPath => Node.FullPath;
    public string SizeDisplay => ByteSizeFormatter.Format(Node.SizeInBytes);

    public string ModifiedDisplay => Node.LastModifiedUtc == default
        ? "—"
        : Node.LastModifiedUtc.ToLocalTime().ToString("g");
}
