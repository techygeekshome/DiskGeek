using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;

namespace DiskGeek.App.ViewModels;

/// <summary>One row in the Search tab's results list.</summary>
public sealed class SearchResultViewModel
{
    public FileSystemNode Node { get; }

    public SearchResultViewModel(FileSystemNode node) => Node = node;

    public string Name => Node.Name;
    public string FullPath => Node.FullPath;
    public bool IsDirectory => Node.IsDirectory;
    public string Icon => IsDirectory ? "📁" : "📄";
    public string SizeDisplay => ByteSizeFormatter.Format(Node.SizeInBytes);
    public int FileCount => Node.FileCount;

    public string ModifiedDisplay => Node.LastModifiedUtc == default
        ? "—"
        : Node.LastModifiedUtc.ToLocalTime().ToString("g");
}
