namespace DiskGeek.Core.Updates;

/// <summary>
/// The parsed contents of a hosted update-check XML file — a tiny, hand-editable manifest a
/// developer updates on their own web host whenever a new version ships (no build server or
/// package feed required). Shape is deliberately simple and matches the same convention already
/// used for other apps:
/// <code>
/// &lt;?xml version="1.0" encoding="utf-8" ?&gt;
/// &lt;appinfo&gt;
/// &lt;version&gt;1.4.0.0&lt;/version&gt;
/// &lt;url&gt;https://example.com/download&lt;/url&gt;
/// &lt;about&gt;DiskGeek v1.4 | Duplicate Finder, Search, Snapshots, and More&lt;/about&gt;
/// &lt;/appinfo&gt;
/// </code>
/// </summary>
public sealed record UpdateManifest(Version Version, string Url, string? About);
