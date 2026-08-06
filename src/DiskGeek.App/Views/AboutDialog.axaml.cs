using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiskGeek.App.ViewModels;

namespace DiskGeek.App.Views;

/// <summary>Simple "About" dialog showing the product name, version, and TechyGeeksHome branding -
/// none of which were otherwise visible anywhere in the app beyond the window title bar.</summary>
public partial class AboutDialog : Window
{
    private const string WebsiteUrl = "https://techygeekshome.info";

    public AboutDialog()
    {
        InitializeComponent();

        var v = MainWindowViewModel.CurrentVersion;
        VersionText.Text = $"Version {v.Major}.{v.Minor}.{v.Build}";
        CopyrightText.Text = $"© {DateTime.Now.Year} TechyGeeksHome. All rights reserved.";
    }

    public static Task ShowAsync(Window owner) => new AboutDialog().ShowDialog(owner);

    private void OnWebsiteClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort - if there's no default browser association (unusual, but not worth
            // crashing the dialog over), the button simply does nothing.
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
