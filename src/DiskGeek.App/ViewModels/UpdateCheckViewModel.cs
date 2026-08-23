using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskGeek.Core.Updates;

namespace DiskGeek.App.ViewModels;

/// <summary>
/// Drives the "is there a newer version" check: a small hosted XML file is fetched, compared
/// against the running build, and — only if something newer is actually found — a dismissible
/// banner offers a link to go download it. This deliberately never downloads or installs anything
/// itself; publishing an update just means editing the hosted manifest's &lt;version&gt; (and
/// &lt;url&gt;/&lt;about&gt; if they changed) after uploading the new build somewhere.
///
/// <para>
/// The check only ever runs when the user clicks "Check for Updates…" — nothing is requested at
/// startup. That matches PDFGeek, and it means simply opening DiskGeek makes no network request
/// at all, which is a promise worth being able to make plainly on the product page.
/// </para>
/// </summary>
public partial class UpdateCheckViewModel : ObservableObject
{
    /// <summary>
    /// Where the update manifest is hosted.
    /// </summary>
    public const string ManifestUrl = "https://techygeekshome.info/downloads/updates/da/daappinfo.xml";

    private readonly IUpdateChecker _checker;
    private readonly Version _currentVersion;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string? _latestVersionDisplay;

    [ObservableProperty]
    private string? _downloadUrl;

    [ObservableProperty]
    private string? _aboutText;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// True right after an explicit <see cref="CheckNowCommand"/> run that did *not* find an update
    /// (either "you're up to date" or an error) — a separate flag from <see cref="UpdateAvailable"/>
    /// so the two banners (update-found vs. check-result) never both show at once and the result of
    /// a manual click doesn't linger forever without a way to dismiss it.
    /// </summary>
    [ObservableProperty]
    private bool _showStatusMessage;

    public UpdateCheckViewModel(Version currentVersion, IUpdateChecker? checker = null)
    {
        _currentVersion = currentVersion;
        _checker = checker ?? new UpdateChecker();
    }

    /// <summary>The one and only entry point: an explicit, user-initiated check behind the "Check for Updates…" button. Always reports something back, including "you're up to date" or a plain-language error.</summary>
    [RelayCommand]
    private async Task CheckNowAsync()
    {
        var result = await RunCheckAsync();

        if (result.Failed)
        {
            UpdateAvailable = false;
            StatusText = result.ErrorMessage!;
            ShowStatusMessage = true;
            return;
        }

        if (result.IsUpdateAvailable)
        {
            ApplyAvailableUpdate(result);
        }
        else
        {
            UpdateAvailable = false;
            StatusText = "You're running the latest version.";
            ShowStatusMessage = true;
        }
    }

    [RelayCommand]
    private void Dismiss() => UpdateAvailable = false;

    [RelayCommand]
    private void DismissStatusMessage() => ShowStatusMessage = false;

    private async Task<UpdateCheckResult> RunCheckAsync()
    {
        IsChecking = true;
        try
        {
            return await _checker.CheckForUpdateAsync(ManifestUrl, _currentVersion).ConfigureAwait(false);
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void ApplyAvailableUpdate(UpdateCheckResult result)
    {
        UpdateAvailable = true;
        ShowStatusMessage = false;
        LatestVersionDisplay = result.LatestVersion?.ToString();
        DownloadUrl = result.DownloadUrl;
        AboutText = result.About;
        StatusText = $"Version {LatestVersionDisplay} is available.";
    }
}
