using DiskGeek.Core.Updates;
using Xunit;

namespace DiskGeek.Core.Tests;

public class UpdateCheckerTests
{
    /// <summary>
    /// The substitute the interface was split out for: returns a canned manifest, or throws to
    /// simulate the network being unavailable, without any real HTTP.
    /// </summary>
    private sealed class FakeFetcher : IUpdateManifestFetcher
    {
        private readonly string? _xml;
        private readonly Exception? _throw;

        public FakeFetcher(string xml) => _xml = xml;
        public FakeFetcher(Exception toThrow) => _throw = toThrow;

        public string? LastUrl { get; private set; }

        public Task<string> FetchAsync(string url, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            cancellationToken.ThrowIfCancellationRequested();
            if (_throw is not null) return Task.FromException<string>(_throw);
            return Task.FromResult(_xml!);
        }
    }

    private static string Manifest(string version) =>
        $"<appinfo><version>{version}</version><url>https://example.com/get</url><about>notes</about></appinfo>";

    private static UpdateChecker CheckerReturning(string version) => new(new FakeFetcher(Manifest(version)));

    [Fact]
    public async Task ReportsAnUpdateWhenTheManifestIsNewer()
    {
        var result = await CheckerReturning("2.0.0.0").CheckForUpdateAsync("https://x/", new Version(1, 0, 0, 0));

        Assert.True(result.IsUpdateAvailable);
        Assert.False(result.Failed);
        Assert.Equal(new Version(2, 0, 0, 0), result.LatestVersion);
        Assert.Equal("https://example.com/get", result.DownloadUrl);
        Assert.Equal("notes", result.About);
    }

    [Fact]
    public async Task ReportsNoUpdateWhenTheManifestMatches()
    {
        var result = await CheckerReturning("1.0.0.0").CheckForUpdateAsync("https://x/", new Version(1, 0, 0, 0));

        Assert.False(result.IsUpdateAvailable);
        Assert.False(result.Failed);
        // Even with no update, the manifest details still come back so a caller can show them.
        Assert.Equal(new Version(1, 0, 0, 0), result.LatestVersion);
    }

    [Fact]
    public async Task ReportsNoUpdateWhenTheManifestIsOlderThanTheRunningBuild()
    {
        var result = await CheckerReturning("0.9.0.0").CheckForUpdateAsync("https://x/", new Version(1, 0, 0, 0));

        Assert.False(result.IsUpdateAvailable);
    }

    // The bug NormalizeForComparison exists to prevent: Version treats a missing component as -1,
    // so a naive comparison makes "1.4.0" look older than "1.4.0.0" and DiskGeek would nag about an
    // update on every single check. These are the cases that would regress if that padding was ever
    // removed as "unnecessary".
    [Theory]
    [InlineData("1.4.0", "1.4.0.0")]
    [InlineData("1.4.0.0", "1.4.0")]
    [InlineData("1.4", "1.4.0.0")]
    [InlineData("1", "1.0.0.0")]
    public async Task TreatsDifferentComponentCountsOfTheSameReleaseAsEqual(string manifestVersion, string runningVersion)
    {
        var result = await CheckerReturning(manifestVersion)
            .CheckForUpdateAsync("https://x/", Version.Parse(runningVersion));

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task StillSpotsAGenuineUpdateWhenComponentCountsDiffer()
    {
        var result = await CheckerReturning("1.5").CheckForUpdateAsync("https://x/", new Version(1, 4, 0, 0));

        Assert.True(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task ANetworkFailureIsReportedAsAFailedCheckRatherThanThrowing()
    {
        var checker = new UpdateChecker(new FakeFetcher(new HttpRequestException("no such host")));

        var result = await checker.CheckForUpdateAsync("https://x/", new Version(1, 0));

        Assert.True(result.Failed);
        Assert.False(result.IsUpdateAvailable);
        Assert.Contains("no such host", result.ErrorMessage!);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task AMalformedManifestIsReportedAsAFailedCheckRatherThanThrowing()
    {
        var checker = new UpdateChecker(new FakeFetcher("<appinfo><version>banana</version></appinfo>"));

        var result = await checker.CheckForUpdateAsync("https://x/", new Version(1, 0));

        Assert.True(result.Failed);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task ARealCancellationPropagatesRatherThanBecomingAFailedCheck()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var checker = new UpdateChecker(new FakeFetcher(Manifest("2.0")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => checker.CheckForUpdateAsync("https://x/", new Version(1, 0), cts.Token));
    }

    [Fact]
    public async Task PassesTheManifestUrlStraightThroughToTheFetcher()
    {
        var fetcher = new FakeFetcher(Manifest("1.0"));
        await new UpdateChecker(fetcher).CheckForUpdateAsync("https://example.com/manifest.xml", new Version(1, 0));

        Assert.Equal("https://example.com/manifest.xml", fetcher.LastUrl);
    }

    // A blank URL or a null version is a programming mistake, not a runtime condition, so these
    // should throw rather than come back as a polite "couldn't check".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsABlankManifestUrl(string url)
    {
        var checker = CheckerReturning("1.0");
        await Assert.ThrowsAsync<ArgumentException>(() => checker.CheckForUpdateAsync(url, new Version(1, 0)));
    }

    [Fact]
    public async Task RejectsANullManifestUrl()
    {
        var checker = CheckerReturning("1.0");
        await Assert.ThrowsAsync<ArgumentNullException>(() => checker.CheckForUpdateAsync(null!, new Version(1, 0)));
    }

    [Fact]
    public async Task RejectsANullCurrentVersion()
    {
        var checker = CheckerReturning("1.0");
        await Assert.ThrowsAsync<ArgumentNullException>(() => checker.CheckForUpdateAsync("https://x/", null!));
    }
}
