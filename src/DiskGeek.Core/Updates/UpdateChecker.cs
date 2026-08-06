namespace DiskGeek.Core.Updates;

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckForUpdateAsync(string manifestUrl, Version currentVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Checks a hosted <see cref="UpdateManifest"/> against the running app's version. This is
/// deliberately not an auto-updater — it never downloads or installs anything. It only answers "is
/// there a newer version, and if so, where do I get it," leaving the actual download to the user's
/// browser (see <see cref="UpdateCheckResult.DownloadUrl"/>).
/// </summary>
public sealed class UpdateChecker : IUpdateChecker
{
    private readonly IUpdateManifestFetcher _fetcher;

    public UpdateChecker(IUpdateManifestFetcher? fetcher = null) => _fetcher = fetcher ?? new HttpUpdateManifestFetcher();

    public async Task<UpdateCheckResult> CheckForUpdateAsync(string manifestUrl, Version currentVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestUrl);
        ArgumentNullException.ThrowIfNull(currentVersion);

        string xml;
        try
        {
            xml = await _fetcher.FetchAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // a real cancellation - let it propagate, don't report it as a failed check
        }
        catch (Exception ex)
        {
            // No internet, the host is down, DNS failure, a timeout, a redirect loop, whatever -
            // none of it should ever be treated as "an update is available" or crash the caller.
            // Silent-on-startup / explained-on-manual-check is handled by the caller inspecting
            // ErrorMessage; this method itself never throws for network problems.
            return UpdateCheckResult.Failure($"Couldn't reach the update server: {ex.Message}");
        }

        UpdateManifest manifest;
        try
        {
            manifest = UpdateManifestParser.Parse(xml);
        }
        catch (FormatException ex)
        {
            return UpdateCheckResult.Failure(ex.Message);
        }

        var isNewer = NormalizeForComparison(manifest.Version) > NormalizeForComparison(currentVersion);
        return new UpdateCheckResult(isNewer, manifest.Version, manifest.Url, manifest.About, null);
    }

    /// <summary>
    /// <see cref="Version"/> treats a missing component as -1, not 0 - so "1.4.0" (Revision = -1)
    /// compares as *older* than "1.4.0.0" (Revision = 0) even though a human would call those the
    /// same version. Padding both sides to a full 4 components before comparing avoids a false
    /// "update available" whenever the manifest and the assembly version happen to have a different
    /// number of parts for what's actually the same release.
    /// </summary>
    private static Version NormalizeForComparison(Version version) =>
        new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}

/// <summary>Result of a single update check — never throws for "couldn't check," only for programmer errors (see <see cref="UpdateChecker.CheckForUpdateAsync"/>).</summary>
public sealed record UpdateCheckResult(bool IsUpdateAvailable, Version? LatestVersion, string? DownloadUrl, string? About, string? ErrorMessage)
{
    public bool Failed => ErrorMessage is not null;

    public static UpdateCheckResult Failure(string message) => new(false, null, null, null, message);
}
