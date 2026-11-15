using System.Windows;
using CryptSage.Core;
using Wpf.Ui.Controls;

namespace CryptSage.App;

public partial class AboutDialog : FluentWindow
{
    private readonly string _version;
    private UpdateInfo? _update;
    private bool _install;

    private AboutDialog(string version, UpdateInfo? update)
    {
        InitializeComponent();
        _version = version;
        VersionText.Text = $"Version {version}";
        if (update is not null) ShowAvailable(update);
    }

    /**
     * Shows the dialog. Returns the update to install when the user clicked
     * "Install Update", and null when the user only closed the dialog.
     * `update` carries the latest known update in and out, so the caller
     * keeps the result of a check that the user made here.
     */
    public static UpdateInfo? Show(Window owner, string version, ref UpdateInfo? update)
    {
        var dlg = new AboutDialog(version, update) { Owner = owner };
        dlg.ShowDialog();
        update = dlg._update;
        return dlg._install ? dlg._update : null;
    }

    private void ShowAvailable(UpdateInfo update)
    {
        _update = update;
        StatusText.Text =
            $"Version {update.LatestVersion} is available. Crypt Sage closes, installs the update with no setup pages, and opens again.";
        ActionButton.Content = "Install Update";
        ActionButton.Appearance = ControlAppearance.Primary;
    }

    private async void OnAction(object sender, RoutedEventArgs e)
    {
        if (_update is not null)
        {
            _install = true;
            Close();
            return;
        }

        ActionButton.IsEnabled = false;
        StatusText.Text = "Checking for updates…";
        try
        {
            var update = await Updater.CheckAsync(_version);
            if (update is not null)
                ShowAvailable(update);
            else
                StatusText.Text = "Crypt Sage is up to date.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not check for updates. {ex.Message}";
        }
        finally
        {
            ActionButton.IsEnabled = true;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
