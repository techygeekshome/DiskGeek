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
    /// The organisation page, offered as one more button so the range list always squares off
    /// into an even grid however many apps there happen to be.
    /// </summary>
    private const string GitHubProfileUrl = "https://github.com/techygeekshome";

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

        BuildFamilyList();
        GitHubProfileButton.Click += (_, _) => OpenUrl(GitHubProfileUrl);
        FamilyHubButton.Click += (_, _) => OpenUrl(Family.HubUrl);
    }

    /// <summary>
    /// Renders the rest of the Geek range as a two-column grid of buttons, DiskGeek removed
    /// from its own list, each one opening that app's page on the website rather than its
    /// repository - a visitor wants the product page, not the source.
    ///
    /// The data lives in <see cref="Family"/> - one file, carried identically in every app repo -
    /// rather than being written into this markup, so adding a tool to the range does not mean
    /// hunting through four About screens. Nothing is fetched to build it; the list ships inside
    /// the executable.
    ///
    /// An odd number of apps would leave a hole in the second column, so in that case the GitHub
    /// profile button fills it and the separate full-width button below is hidden. With an even
    /// number the grid is already square and that button stays where it is.
    /// </summary>
    private void BuildFamilyList()
    {
        var others = Family.Others("DiskGeek");

        for (var i = 0; i < others.Count; i++)
        {
            FamilyGrid.Children.Add(FamilyButton(others[i].Name, others[i].ProductUrl, i));
        }

        if (others.Count % 2 == 1)
        {
            FamilyGrid.Children.Add(
                FamilyButton("All our code on GitHub", GitHubProfileUrl, others.Count));
            GitHubProfileButton.IsVisible = false;
        }
    }

    /// <summary>
    /// One tile in the range grid. The margin alternates so the gutter sits between the columns
    /// rather than outside them, which UniformGrid will not do on its own.
    /// </summary>
    private static Button FamilyButton(string text, string url, int index)
    {
        var button = new Button
        {
            Content = text,
            Classes = { "ghost" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = index % 2 == 0
                ? new Avalonia.Thickness(0, 0, 4, 8)
                : new Avalonia.Thickness(4, 0, 0, 8)
        };

        button.Click += (_, _) => OpenUrl(url);
        return button;
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
