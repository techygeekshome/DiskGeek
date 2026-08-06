namespace DiskGeek.Core.Updates;

/// <summary>
/// Fetches the raw text of a hosted update manifest. Split out from <see cref="UpdateChecker"/> so
/// the actual network call — the one part of this feature that genuinely can't be exercised from an
/// automated test without a real server — is a single small seam a test can substitute a canned
/// response (or a simulated failure) for, while the parsing and version-comparison logic around it
/// stays fully covered by real tests.
/// </summary>
public interface IUpdateManifestFetcher
{
    Task<string> FetchAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>Fetches a manifest over HTTP(S). This is the only piece of the update-check feature that touches the network.</summary>
public sealed class HttpUpdateManifestFetcher : IUpdateManifestFetcher
{
    // A short timeout matters here specifically: this check can run silently on every app startup,
    // and a slow/unreachable host should never make the app feel like it's hanging on launch.
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    public Task<string> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        Client.GetStringAsync(url, cancellationToken);
}
