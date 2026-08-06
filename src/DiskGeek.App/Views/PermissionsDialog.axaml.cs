using Avalonia.Controls;
using Avalonia.Interactivity;
using DiskGeek.App.ViewModels;
using DiskGeek.Core.Permissions;

namespace DiskGeek.App.Views;

/// <summary>Shows the owner + access control list for a file or folder (Windows only — see PermissionReader).</summary>
public partial class PermissionsDialog : Window
{
    public PermissionsDialog()
    {
        InitializeComponent();
    }

    public static Task ShowAsync(Window owner, string path, bool isDirectory)
    {
        var dialog = new PermissionsDialog();
        dialog.PathText.Text = path;
        dialog.Populate(path, isDirectory);
        return dialog.ShowDialog(owner);
    }

    private void Populate(string path, bool isDirectory)
    {
        try
        {
            var info = new PermissionReader().GetPermissions(path, isDirectory);
            OwnerText.Text = $"Owner: {info.Owner}";
            EntriesList.ItemsSource = info.Entries.Select(e => new PermissionEntryViewModel(e)).ToList();

            if (info.Entries.Count == 0)
            {
                ErrorText.Text = "No access control entries were returned for this item.";
                ErrorText.IsVisible = true;
            }
        }
        catch (PlatformNotSupportedException)
        {
            OwnerText.Text = string.Empty;
            ErrorText.Text = "NTFS permission viewing is only available on Windows — this build is " +
                              "running on a non-Windows platform, so there are no Windows ACLs to read. " +
                              "This is expected here, not a bug; it works on a real Windows machine.";
            ErrorText.IsVisible = true;
        }
        catch (Exception ex)
        {
            OwnerText.Text = string.Empty;
            ErrorText.Text = $"Couldn't read permissions: {ex.Message}";
            ErrorText.IsVisible = true;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
