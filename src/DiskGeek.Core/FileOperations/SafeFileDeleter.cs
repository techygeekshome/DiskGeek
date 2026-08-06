using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DiskGeek.Core.FileOperations;

public sealed record DeleteResult(IReadOnlyList<string> Deleted, IReadOnlyList<(string Path, string Error)> Failed)
{
    public bool AllSucceeded => Failed.Count == 0;
}

/// <summary>
/// Deletes files, sending them to the Recycle Bin on Windows (via the classic shell32
/// <c>SHFileOperation</c> API) so a mistaken duplicate-cleanup selection is recoverable. On
/// non-Windows platforms there is no equivalent OS-level trash the .NET base class library can
/// target uniformly, so files are permanently deleted there — callers must make that distinction
/// obvious to the user before confirming.
/// </summary>
public static class SafeFileDeleter
{
    public static DeleteResult DeleteFiles(IEnumerable<string> paths)
    {
        var deleted = new List<string>();
        var failed = new List<(string, string)>();

        foreach (var path in paths)
        {
            if (TryDelete(path, out var error))
                deleted.Add(path);
            else
                failed.Add((path, error ?? "Unknown error."));
        }

        return new DeleteResult(deleted, failed);
    }

    private static bool TryDelete(string path, out string? error)
    {
        error = null;

        try
        {
            if (OperatingSystem.IsWindows())
                return TryDeleteToRecycleBin(path, out error);

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryDeleteToRecycleBin(string path, out string? error)
    {
        error = null;
        var fileOp = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // pFrom is a list of paths, each null-separated, with one extra trailing null to mark
            // the end of the list - a single path still needs that double terminator.
            pFrom = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
        };

        int result;
        try
        {
            result = SHFileOperation(ref fileOp);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            error = $"Recycle Bin API unavailable ({ex.Message}); file was not deleted.";
            return false;
        }

        if (result != 0)
        {
            error = $"Recycle Bin delete failed (Windows error code {result}).";
            return false;
        }

        if (fileOp.fAnyOperationsAborted)
        {
            error = "Delete was aborted.";
            return false;
        }

        return true;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;   // send to Recycle Bin instead of permanent delete
    private const ushort FOF_NOCONFIRMATION = 0x0010; // we show our own confirmation dialog first
    private const ushort FOF_SILENT = 0x0004;      // no progress UI
    private const ushort FOF_NOERRORUI = 0x0400;   // surface failures to us, not a Windows dialog

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);
}
