using DiskGeek.Core.Formatting;
using Xunit;

namespace DiskGeek.Core.Tests;

public class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(999L, "999 B")]
    // Bytes are the one unit shown without decimals, so 1023 must not round up to "1 KB".
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(1099511627776L, "1 TB")]
    [InlineData(1125899906842624L, "1 PB")]
    public void FormatsKnownSizes(long bytes, string expected) =>
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes));

    [Fact]
    public void RoundsToTwoDecimalPlacesAtMost()
    {
        // 1.44 MB - the classic floppy - exercises the "0.##" path rather than a whole number.
        Assert.Equal("1.41 MB", ByteSizeFormatter.Format(1474560L));
    }

    [Theory]
    [InlineData(-1024L, "-1 KB")]
    [InlineData(-1L, "-1 B")]
    public void NegativesKeepTheirSign(long bytes, string expected) =>
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes));

    [Fact]
    public void StopsAtPetabytesRatherThanRunningOffTheEndOfTheUnitTable()
    {
        // 1024 PB has nowhere further to go, so it must stay in PB rather than index past the array.
        var result = ByteSizeFormatter.Format(1152921504606846976L);
        Assert.EndsWith(" PB", result);
    }

    [Fact]
    public void UsesInvariantDecimalSeparatorRegardlessOfCurrentCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // German uses a comma as the decimal separator. The formatter passes InvariantCulture
            // explicitly, so this must still come back with a dot.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5 KB", ByteSizeFormatter.Format(1536L));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void HandlesLongMaxValueWithoutOverflowing()
    {
        var result = ByteSizeFormatter.Format(long.MaxValue);
        Assert.EndsWith(" PB", result);
    }
}
