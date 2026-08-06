using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskGeek.Core.Formatting;
using DiskGeek.Core.Models;

namespace DiskGeek.App.ViewModels;

/// <summary>
/// Bindable wrapper around a scanned <see cref="FileSystemNode"/>. Child view models are built
/// lazily (on first access to <see cref="Children"/>) so opening a huge tree doesn't require
/// materializing a view model for every file up front.
/// </summary>
public sealed class FileSystemNodeViewModel : ObservableObject
{
    /// <summary>
    /// The percent-of-root denominator, shared by reference across an entire scanned tree's worth
    /// of view models (every node built from the same root shares the same holder instance — see
    /// <see cref="BuildChildren"/>). Mutating <see cref="Value"/> after an in-place delete/rename
    /// changes what every already-built node's <see cref="PercentOfRoot"/> reads next, without
    /// needing a reference to each of those view models individually.
    /// </summary>
    private sealed class PercentBaseHolder
    {
        public long Value;
    }

    private readonly PercentBaseHolder _percentBase;
    private ObservableCollection<FileSystemNodeViewModel>? _children;

    public FileSystemNodeViewModel(FileSystemNode model, long percentBaseBytes)
        : this(model, new PercentBaseHolder { Value = percentBaseBytes > 0 ? percentBaseBytes : model.SizeInBytes })
    {
    }

    private FileSystemNodeViewModel(FileSystemNode model, PercentBaseHolder percentBase)
    {
        Model = model;
        _percentBase = percentBase;
    }

    public FileSystemNode Model { get; }

    public string Name => Model.Name;
    public string FullPath => Model.FullPath;
    public bool IsDirectory => Model.IsDirectory;
    public bool AccessDenied => Model.AccessDenied;
    public string Icon => AccessDenied ? "🔒" : (IsDirectory ? "📁" : "📄");
    public long SizeInBytes => Model.SizeInBytes;
    public string SizeDisplay => ByteSizeFormatter.Format(Model.SizeInBytes);
    public int FileCount => Model.FileCount;

    public string ModifiedDisplay => Model.LastModifiedUtc == default
        ? "—"
        : Model.LastModifiedUtc.ToLocalTime().ToString("g");

    public double PercentOfRoot => _percentBase.Value <= 0 ? 0 : Model.SizeInBytes / (double)_percentBase.Value * 100.0;

    public bool HasChildren => Model.Children.Count > 0;

    public ObservableCollection<FileSystemNodeViewModel> Children => _children ??= BuildChildren();

    private ObservableCollection<FileSystemNodeViewModel> BuildChildren() => new(
        Model.Children
            .OrderByDescending(c => c.SizeInBytes)
            .Select(c => new FileSystemNodeViewModel(c, _percentBase)));

    /// <summary>
    /// Call after <see cref="Model"/>'s own aggregate SizeInBytes/FileCount changed underneath this
    /// view model (e.g. a descendant was deleted via <c>TreeMutator</c>) to refresh its bindings.
    /// Does nothing to <see cref="Children"/> itself — callers patch child collections separately.
    /// </summary>
    public void RefreshAggregates()
    {
        OnPropertyChanged(nameof(SizeInBytes));
        OnPropertyChanged(nameof(SizeDisplay));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(PercentOfRoot));
    }

    /// <summary>
    /// Updates the shared percent-of-root denominator (call this once, on the root view model,
    /// after the root's total SizeInBytes changed) and refreshes <see cref="PercentOfRoot"/> on
    /// every already-built descendant. Nodes whose <see cref="Children"/> was never accessed are
    /// left alone — they'll read the updated denominator automatically whenever they're first built.
    /// </summary>
    public void UpdatePercentBaseRecursive(long newTotal)
    {
        _percentBase.Value = newTotal > 0 ? newTotal : Model.SizeInBytes;
        RefreshPercentRecursive();
    }

    private void RefreshPercentRecursive()
    {
        OnPropertyChanged(nameof(PercentOfRoot));
        if (_children is null) return;
        foreach (var child in _children)
            child.RefreshPercentRecursive();
    }

    /// <summary>Removes the child matching <paramref name="fullPath"/> from an already-built <see cref="Children"/> collection. No-ops if Children was never built (nothing to patch — it'll reflect the mutated Model whenever it is built).</summary>
    public bool TryRemoveChild(string fullPath)
    {
        if (_children is null) return false;

        for (var i = 0; i < _children.Count; i++)
        {
            if (string.Equals(_children[i].FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                _children.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Replaces the child previously at <paramref name="oldFullPath"/> with a fresh view model wrapping <paramref name="newModel"/> (used after an in-place rename, which creates a new <see cref="FileSystemNode"/> instance). No-ops if Children was never built.</summary>
    public bool TryReplaceChild(string oldFullPath, FileSystemNode newModel)
    {
        if (_children is null) return false;

        for (var i = 0; i < _children.Count; i++)
        {
            if (string.Equals(_children[i].FullPath, oldFullPath, StringComparison.OrdinalIgnoreCase))
            {
                _children[i] = new FileSystemNodeViewModel(newModel, _percentBase);
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds an already-built child by full path without forcing <see cref="Children"/> to build. Returns null if Children isn't built yet or no match is found at this level.</summary>
    public FileSystemNodeViewModel? FindBuiltChild(string fullPath)
    {
        if (_children is null) return null;
        foreach (var child in _children)
            if (string.Equals(child.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return child;
        return null;
    }
}
