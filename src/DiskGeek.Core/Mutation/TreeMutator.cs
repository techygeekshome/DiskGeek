using DiskGeek.Core.Models;

namespace DiskGeek.Core.Mutation;

/// <summary>
/// Patches an already-scanned <see cref="FileSystemNode"/> tree in place after a file operation
/// (delete, rename) that happened outside the normal scan — so the app can reflect the change
/// immediately instead of requiring a full re-scan to see correct sizes/names again. Every mutation
/// here mirrors a real change already made on disk by the caller (this class doesn't touch the
/// filesystem itself); it just keeps the in-memory tree honest afterward.
/// </summary>
public static class TreeMutator
{
    /// <summary>
    /// Removes a file node from the tree and subtracts its size/count from every ancestor up to the
    /// root, so aggregate totals stay correct without a re-scan. No-ops (returns false) for a node
    /// that has no parent (the scan root itself) — that's never a valid thing to "delete" in place.
    /// </summary>
    public static bool RemoveNode(FileSystemNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var parent = node.Parent;
        if (parent is null)
            return false;

        if (!parent.Children.Remove(node))
            return false; // already removed, or wasn't actually a child of its own Parent reference

        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.SizeInBytes -= node.SizeInBytes;
            ancestor.FileCount -= node.FileCount;
        }

        return true;
    }

    /// <summary>
    /// Replaces a node with a renamed copy (same size, type, and modified time; new name/path) in
    /// its parent's children. <see cref="FileSystemNode"/>'s Name/FullPath are init-only, so a
    /// rename can't mutate the existing instance — this returns the new instance that replaces it.
    /// Sizes don't change on a rename, so ancestor aggregates are untouched. Only meaningful for
    /// leaf files that have no children of their own whose paths would also need rewriting (which
    /// matches how batch rename is exposed in the UI — files only, never folders).
    /// </summary>
    public static FileSystemNode RenameNode(FileSystemNode node, string newFullPath, string newName)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(newFullPath)) throw new ArgumentException("New path must not be empty.", nameof(newFullPath));
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("New name must not be empty.", nameof(newName));

        var parent = node.Parent ?? throw new InvalidOperationException("Cannot rename the scan root in place.");

        var newNode = new FileSystemNode
        {
            Name = newName,
            FullPath = newFullPath,
            NodeType = node.NodeType,
            LastModifiedUtc = node.LastModifiedUtc,
            Extension = System.IO.Path.GetExtension(newFullPath),
            SizeInBytes = node.SizeInBytes,
            FileCount = node.FileCount,
            AccessDenied = node.AccessDenied,
            Parent = parent
        };

        var index = parent.Children.IndexOf(node);
        if (index >= 0)
            parent.Children[index] = newNode;
        else
            parent.Children.Add(newNode); // shouldn't happen if node.Parent is accurate, but don't silently drop it

        return newNode;
    }
}
