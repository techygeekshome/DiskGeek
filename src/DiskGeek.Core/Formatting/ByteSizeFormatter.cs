using System.Globalization;

namespace DiskGeek.Core.Formatting;

/// <summary>Formats byte counts as human-readable strings (e.g. "1.44 GB").</summary>
public static class ByteSizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes < 0) return "-" + Format(-bytes);
        if (bytes == 0) return "0 B";

        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.##";
        return value.ToString(format, CultureInfo.InvariantCulture) + " " + Units[unitIndex];
    }
}
