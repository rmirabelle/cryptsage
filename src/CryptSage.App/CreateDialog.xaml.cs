using System.IO;
using System.Windows;
using CryptSage.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace CryptSage.App;

public partial class CreateDialog : FluentWindow
{
    private static readonly (string Label, long Bytes)[] Sizes =
    {
        ("1 MB",    1L * 1024 * 1024),
        ("10 MB",  10L * 1024 * 1024),
        ("100 MB", 100L * 1024 * 1024),
        ("1 GB",    1L * 1024 * 1024 * 1024),
        ("5 GB",    5L * 1024 * 1024 * 1024),
        ("10 GB",  10L * 1024 * 1024 * 1024),
    };

    private readonly VeraCryptLauncher _launcher;

    private CreateDialog(VeraCryptLauncher launcher)
    {
        InitializeComponent();
        _launcher = launcher;
        SizeCombo.ItemsSource = Sizes.Select(s => s.Label).ToArray();
        SizeCombo.SelectedIndex = 2;
        Loaded += (_, _) => PathBox.Focus();
    }

    public char? MountedLetter { get; private set; }

    public static bool Show(Window owner, VeraCryptLauncher launcher,
        out string? createdPath, out char? mountedLetter)
    {
        var dlg = new CreateDialog(launcher) { Owner = owner };
        var ok = dlg.ShowDialog() == true;
        createdPath = ok ? dlg.PathBox.Text : null;
        mountedLetter = ok ? dlg.MountedLetter : null;
        return ok;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "New volume",
            Filter = "All files|*.*",
            AddExtension = false,
            AddToRecent = false,
            OverwritePrompt = false,
        };
        if (dlg.ShowDialog(this) == true)
            PathBox.Text = dlg.FileName;
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "";

        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            StatusText.Text = "Choose a file path.";
            return;
        }
        if (File.Exists(PathBox.Text))
        {
            StatusText.Text = "File already exists — choose a different name.";
            return;
        }
        if (PasswordInput.Password.Length == 0)
        {
            StatusText.Text = "Enter a password.";
            return;
        }
        if (PasswordInput.Password != ConfirmInput.Password)
        {
            StatusText.Text = "Passwords do not match.";
            return;
        }

        var sizeBytes = Sizes[SizeCombo.SelectedIndex].Bytes;

        SetBusy(true);
        try
        {
            var createResult = await _launcher.CreateAsync(PathBox.Text, sizeBytes, PasswordInput.Password);
            if (!createResult.Success)
            {
                StatusText.Text = $"Create failed (exit {createResult.ExitCode}).";
                return;
            }

            var letter = DriveLetters.FirstFree();
            if (letter is not null)
            {
                var mountResult = await _launcher.MountAsync(PathBox.Text, letter.Value, PasswordInput.Password);
                if (mountResult.Success) MountedLetter = letter.Value;
            }

            DialogResult = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void SetBusy(bool busy)
    {
        Spinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CreateButton.IsEnabled = !busy;
    }
}
