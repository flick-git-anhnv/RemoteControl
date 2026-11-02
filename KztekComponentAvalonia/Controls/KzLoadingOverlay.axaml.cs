using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Loading overlay — port of WinForms UcLoading.
/// Usage: place as the last child in the root Panel of your Window so it covers everything.
/// Call Show/Hide from any thread (dispatches to UI thread automatically).
/// </summary>
public partial class KzLoadingOverlay : UserControl
{
    public KzLoadingOverlay()
    {
        InitializeComponent();
    }

    /// <summary>Shows the overlay with title and optional subtitle text.</summary>
    public void ShowLoading(string title, string subTitle = "")
    {
        Dispatcher.UIThread.Post(() =>
        {
            TitleLabel.Text    = title;
            SubTitleLabel.Text = subTitle;
            SubTitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subTitle);
            IsVisible = true;
        });
    }

    /// <summary>Hides the overlay.</summary>
    public void HideLoading()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsVisible = false;
        });
    }

    /// <summary>Updates text while overlay is already visible.</summary>
    public void UpdateLoading(string title, string subTitle = "")
    {
        Dispatcher.UIThread.Post(() =>
        {
            TitleLabel.Text    = title;
            SubTitleLabel.Text = subTitle;
            SubTitleLabel.IsVisible = !string.IsNullOrWhiteSpace(subTitle);
        });
    }
}
