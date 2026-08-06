using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DiskGeek.Core.Permissions;

/// <summary>One allow/deny access control entry on a file or folder.</summary>
public sealed record PermissionEntry(string IdentityName, string Rights, bool IsInherited, bool IsDeny);

public sealed record PermissionInfo(string Owner, IReadOnlyList<PermissionEntry> Entries);

public interface IPermissionReader
{
    /// <summary>
    /// Reads the owner and access control list for a file or folder. Windows-only — NTFS
    /// permissions (and .NET's <see cref="FileSystemSecurity"/> API surface for reading them) are
    /// a Windows-specific concept with no equivalent this can honestly report on other platforms.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
    PermissionInfo GetPermissions(string path, bool isDirectory);
}

public sealed class PermissionReader : IPermissionReader
{
    public PermissionInfo GetPermissions(string path, bool isDirectory)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "NTFS permission viewing is only available on Windows - it reads Windows ACLs, " +
                "which don't exist on this platform.");

        return GetPermissionsWindows(path, isDirectory);
    }

    [SupportedOSPlatform("windows")]
    private static PermissionInfo GetPermissionsWindows(string path, bool isDirectory)
    {
        FileSystemSecurity security = isDirectory
            ? new DirectoryInfo(path).GetAccessControl()
            : new FileInfo(path).GetAccessControl();

        var ownerRef = security.GetOwner(typeof(NTAccount));
        var owner = ownerRef?.Value ?? "(unknown)";

        var entries = new List<PermissionEntry>();
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true, includeInherited: true, targetType: typeof(NTAccount)))
        {
            entries.Add(new PermissionEntry(
                IdentityName: rule.IdentityReference.Value,
                Rights: FormatRights(rule.FileSystemRights),
                IsInherited: rule.IsInherited,
                IsDeny: rule.AccessControlType == AccessControlType.Deny));
        }

        // Own (explicit) rules first, then inherited - own permissions are usually what someone
        // troubleshooting access wants to see first, same emphasis TreeSize's viewer uses.
        var ordered = entries
            .OrderBy(e => e.IsInherited)
            .ThenByDescending(e => e.IsDeny)
            .ThenBy(e => e.IdentityName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PermissionInfo(owner, ordered);
    }

    [SupportedOSPlatform("windows")]
    private static string FormatRights(FileSystemRights rights)
    {
        // FileSystemRights is a big bitmask of low-level rights (ReadData, ExecuteFile, etc.) that
        // rarely means anything to a non-admin reading it raw. Collapse the common combinations
        // down to the labels Windows Explorer's own permissions dialog uses, and fall back to the
        // raw enum text for anything unusual so nothing is silently hidden.
        if (rights.HasFlag(FileSystemRights.FullControl))
            return "Full control";

        var hasModify = rights.HasFlag(FileSystemRights.Modify);
        var hasWrite = rights.HasFlag(FileSystemRights.Write);
        var hasReadExecute = rights.HasFlag(FileSystemRights.ReadAndExecute);
        var hasRead = rights.HasFlag(FileSystemRights.Read);

        if (hasModify && hasReadExecute) return "Modify";
        if (hasReadExecute && hasWrite) return "Read & execute, Write";
        if (hasReadExecute) return "Read & execute";
        if (hasRead && hasWrite) return "Read, Write";
        if (hasRead) return "Read";
        if (hasWrite) return "Write";

        return rights.ToString();
    }
}
