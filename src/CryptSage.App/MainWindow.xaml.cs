using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CryptSage.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace CryptSage.App;

public partial class MainWindow : FluentWindow
{
    private readonly VeraCryptLauncher _launcher = new();
    private readonly HashSet<char> _ourMounts = new();
    private readonly Dictionary<char, Border> _mountBars = new();
    private bool _forceClose;
    private UpdateInfo? _update;

    private static string CurrentVersion =>
        typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _launcher.DismountAllAsync();
        await CheckForUpdateAsync();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose) return;

        var stillMounted = _ourMounts.Where(l => Directory.Exists($"{l}:\\")).ToList();
        if (stillMounted.Count == 0) return;

        e.Cancel = true;

        var message = stillMounted.Count == 1
            ? $"Drive {stillMounted[0]}: is still mounted."
            : $"{stillMounted.Count} drives are still mounted.";

        var confirmed = ExitConfirmDialog.Show(this, message, out var dismount) == true;
        if (!confirmed) return;

        if (dismount)
            await _launcher.DismountAllAsync();

        _forceClose = true;
        Close();
    }

    private async void OnMountClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select volume",
            Filter = "All files|*.*",
            CheckFileExists = true,
            AddToRecent = false,
            DereferenceLinks = true,
        };
        if (dlg.ShowDialog(this) != true) return;

        var pwd = PasswordDialog.Prompt(this, "Enter volume password");
        if (pwd is null) return;

        var letter = DriveLetters.FirstFree();
        if (letter is null)
        {
            ShowAlert(InfoBarSeverity.Error, "No free drive letters", "Unmount something first.");
            return;
        }

        SetBusy(true, $"Mounting to {letter}:…");
        try
        {
            var result = await _launcher.MountAsync(dlg.FileName, letter.Value, pwd);
            if (result.Success)
            {
                _ourMounts.Add(letter.Value);
                AddMountBar(letter.Value, $"Mounted to {letter}:");
            }
            else
            {
                ShowAlert(InfoBarSeverity.Error, "Mount failed", "Wrong password or invalid volume.");
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnDismountAllClick(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Dismounting all…");
        try
        {
            var result = await _launcher.DismountAllAsync();
            if (result.Success)
            {
                _ourMounts.Clear();
                ClearMountBars();
                ShowAlert(InfoBarSeverity.Success, "Dismounted", "All volumes dismounted.");
            }
            else
                ShowAlert(InfoBarSeverity.Error, "Dismount failed", $"Exit code {result.ExitCode}.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (!CreateDialog.Show(this, _launcher, out var path, out var letter)) return;

        if (letter is not null)
        {
            _ourMounts.Add(letter.Value);
            AddMountBar(letter.Value, $"Created and mounted to {letter}:");
        }
        else
        {
            ShowAlert(InfoBarSeverity.Success, "Created", $"Volume created at {path}");
        }
    }

    private void AddMountBar(char letter, string title)
    {
        /**
         * One row per mounted volume: the list of mounted drives. A row stays
         * until its drive is dismounted. A reused letter (volume dismounted
         * outside the app) replaces its old row. The purple color sets the
         * rows apart from the alerts of the status bar.
         */
        RemoveMountBar(letter);

        var icon = new SymbolIcon
        {
            Symbol = SymbolRegular.HardDrive20,
            FontSize = 22,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
        });
        text.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Click here to open in Explorer.",
            FontSize = 12,
            Opacity = 0.7,
        });

        var dismount = new Wpf.Ui.Controls.Button
        {
            Content = "Dismount",
            Cursor = Cursors.Arrow,
            VerticalAlignment = VerticalAlignment.Center,
        };
        dismount.Click += async (_, _) => await DismountOneAsync(letter);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 1);
        Grid.SetColumn(dismount, 2);
        grid.Children.Add(icon);
        grid.Children.Add(text);
        grid.Children.Add(dismount);

        var row = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x4D, 0x74, 0x4D, 0xA9)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x65, 0xBE)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 8, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            Child = grid,
        };
        /** A click on the Dismount button does not reach this handler: the button handles it. */
        row.MouseLeftButtonUp += (_, _) => OpenInExplorer(letter);

        _mountBars[letter] = row;
        MountBars.Children.Add(row);
        StatusBar.IsOpen = false;
    }

    private void RemoveMountBar(char letter)
    {
        if (_mountBars.Remove(letter, out var old))
            MountBars.Children.Remove(old);
    }

    private void ClearMountBars()
    {
        _mountBars.Clear();
        MountBars.Children.Clear();
    }

    private async Task DismountOneAsync(char letter)
    {
        SetBusy(true, $"Dismounting {letter}:…");
        try
        {
            await _launcher.DismountAsync(letter);

            /**
             * The drive itself is the test, not the exit code: a volume that
             * was already dismounted outside the app must also clear its row.
             */
            if (Directory.Exists($"{letter}:\\"))
            {
                ShowAlert(InfoBarSeverity.Error, "Dismount failed", $"Drive {letter}: is still mounted.");
                return;
            }

            _ourMounts.Remove(letter);
            RemoveMountBar(letter);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            _update = await Updater.CheckAsync(CurrentVersion);
        }
        catch
        {
            // No network or no release: the app works the same without the check.
            return;
        }
        if (_update is null) return;

        UpdateText.Text = $"Version {_update.LatestVersion} is available.";
        UpdateBar.Visibility = Visibility.Visible;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    /** Help > About, and the "Update" button of the update notice. */
    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var install = AboutDialog.Show(this, CurrentVersion, ref _update);
        if (_update is not null)
        {
            UpdateText.Text = $"Version {_update.LatestVersion} is available.";
            UpdateBar.Visibility = Visibility.Visible;
        }
        if (install is not null) await InstallUpdateAsync();
    }

    private async Task InstallUpdateAsync()
    {
        if (_update is null) return;

        /** The installer replaces the VeraCrypt files, so no drive can stay mounted. */
        if (_ourMounts.Any(l => Directory.Exists($"{l}:\\")))
        {
            ShowAlert(InfoBarSeverity.Warning, "Drives are mounted", "Dismount all drives before you update.");
            return;
        }

        SetBusy(true, "Downloading update…");
        try
        {
            var progress = new Progress<double>(f => BusyText.Text = $"Downloading update… {f:P0}");
            var installer = await Updater.DownloadAsync(_update, progress);
            Updater.RunInstaller(installer);
            _forceClose = true;
            Close();
        }
        catch (Exception ex)
        {
            SetBusy(false);
            ShowAlert(InfoBarSeverity.Error, "Update failed", ex.Message);
        }
    }

    private static void OpenInExplorer(char letter)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"{letter}:\\",
            UseShellExecute = true,
        });
    }

    private void SetBusy(bool busy, string? busyMessage = null)
    {
        BusyPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busyMessage is not null) BusyText.Text = busyMessage;
        MountButton.IsEnabled = !busy;
        DismountAllLink.IsEnabled = !busy;
        CreateLink.IsEnabled = !busy;
        MountBars.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy;
        MainMenu.IsEnabled = !busy;
        if (busy) StatusBar.IsOpen = false;
    }

    private void ShowAlert(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
