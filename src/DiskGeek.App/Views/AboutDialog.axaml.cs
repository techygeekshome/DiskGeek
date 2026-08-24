using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DiskGeek.App.ViewModels;

namespace DiskGeek.App.Views;

/// <summary>
/// The About dialog, built to the same layout as PDFGeek's so the range looks like one range:
/// icon and tagline, a description card, a card of links, third-party credits, and an inline
/// update check that reports its result in the dialog rather than in a message box.
///
/// <para>
/// The update check here is the same one behind the main window's "Check for Updates…" — it is
/// user-initiated, it fetches a small manifest, and it never downloads or installs anything. If
/// something newer exists the button turns into a link to the download page and nothing more
/// happens without another click.
/// </para>
/// </summary>
public partial class AboutDialog : Window
{
    private const string WebsiteUrl = "https://techygeekshome.info";
    private const string ProductUrl = "https://techygeekshome.info/diskgeek/";
    private const string RepositoryUrl = "https://github.com/techygeekshome/DiskGeek";
    private const string IssuesUrl = "https://github.com/techygeekshome/DiskGeek/issues";
    private const string DonateUrl = "https://ko-fi.com/techygeekshome";

    /// <summary>
    /// Everything third-party that ships inside the binary. Listing it is partly courtesy and
    /// partly practical: DiskGeek is proprietary freeware, so the one question a cautious
    /// administrator asks is what else is in there.
    /// </summary>
    private static readonly (string Name, string Licence, string Url)[] Credits =
    {
        ("Avalonia", "MIT", "https://avaloniaui.net"),
        ("CommunityToolkit.Mvvm", "MIT", "https://github.com/CommunityToolkit/dotnet"),
        ("Inter typeface", "SIL Open Font License 1.1", "https://rsms.me/inter/"),
        (".NET 8", "MIT", "https://dotnet.microsoft.com")
    };

    private readonly UpdateCheckViewModel _updates;
    private bool _checking;

    public AboutDialog() : this(new UpdateCheckViewModel(MainWindowViewModel.CurrentVersion))
    {
    }

    public AboutDialog(UpdateCheckViewModel updates)
    {
        _updates = updates;
        InitializeComponent();

        var v = MainWindowViewModel.CurrentVersion;
        VersionText.Text = $"Version {v.Major}.{v.Minor}.{v.Build}  ·  TechyGeeksHome";
        LicenceText.Text =
            "Proprietary freeware — free for everyone, including commercial use. " +
            "No ads, no bundled offers, no telemetry.";
        CopyrightText.Text = $"© {DateTime.Now.Year} TechyGeeksHome. All rights reserved.";

        WebsiteButton.Click += (_, _) => OpenUrl(WebsiteUrl);
        ProductButton.Click += (_, _) => OpenUrl(ProductUrl);
        RepoButton.Click += (_, _) => OpenUrl(RepositoryUrl);
        IssuesButton.Click += (_, _) => OpenUrl(IssuesUrl);
        DonateButton.Click += (_, _) => OpenUrl(DonateUrl);
        CloseButton.Click += (_, _) => Close();
        CheckUpdatesButton.Click += async (_, _) => await CheckAsync();

        foreach (var (name, licence, url) in Credits)
        {
            var button = new Button
            {
                Content = $"{name} — {licence}",
                Classes = { "credit" },
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var target = url;
            button.Click += (_, _) => OpenUrl(target);
            CreditsList.Children.Add(button);
        }
    }

    /// <summary>
    /// Pass the main window's own update view-model so a check started here and a check started
    /// from the toolbar share one piece of state, rather than the dialog quietly running a second
    /// request against the manifest.
    /// </summary>
    public static Task ShowAsync(Window owner, UpdateCheckViewModel? updates = null) =>
        (updates is null ? new AboutDialog() : new AboutDialog(updates)).ShowDialog(owner);

    private async Task CheckAsync()
    {
        // Guard rather than queue: double-clicking the button should do nothing the second time,
        // not fire a second request at the manifest.
        if (_checking) return;
        _checking = true;
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";
        UpdateStatusText.Foreground = new SolidColorBrush(Color.Parse("#9ca3af"));

        try
        {
            await _updates.CheckNowCommand.ExecuteAsync(null);

            if (_updates.UpdateAvailable && !string.IsNullOrWhiteSpace(_updates.DownloadUrl))
            {
                UpdateStatusText.Text = _updates.StatusText;
                UpdateStatusText.Foreground = new SolidColorBrush(Color.Parse("#38bdf8"));

                // Deliberately does not start a download. The most it will ever do is open a page.
                CheckUpdatesButton.Content = "Open the download page";
                var url = _updates.DownloadUrl!;
                CheckUpdatesButton.Click += (_, _) => OpenUrl(url);
            }
            else
            {
                UpdateStatusText.Text = _updates.StatusText;
            }
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
            _checking = false;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort. If there is no default browser association — unusual, but it happens on
            // freshly imaged machines — the button does nothing rather than taking the dialog down.
        }
    }
}
