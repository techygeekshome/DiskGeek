using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DiskGeek.App.Views;

/// <summary>A small modal Yes/No dialog. Use <see cref="ShowAsync"/> rather than constructing directly.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog and returns true if the user confirmed.</summary>
    public static Task<bool> ShowAsync(Window owner, string title, string message, string? warning = null, string confirmLabel = "Delete")
    {
        var dialog = new ConfirmDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmLabel;

        if (!string.IsNullOrEmpty(warning))
        {
            dialog.WarningText.Text = warning;
            dialog.WarningText.IsVisible = true;
        }

        return dialog.ShowDialog<bool>(owner);
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
