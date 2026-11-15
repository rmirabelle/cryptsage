using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;

namespace CryptSage.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, a) => LogAndShow((Exception)a.ExceptionObject);
        DispatcherUnhandledException += (_, a) => { LogAndShow(a.Exception); a.Handled = true; };
        TaskScheduler.UnobservedTaskException += (_, a) => { LogAndShow(a.Exception); a.SetObserved(); };

        base.OnStartup(e);

        try
        {
            ApplicationAccentColorManager.Apply(
                Color.FromRgb(0x74, 0x4D, 0xA9),
                ApplicationTheme.Dark);

            SetBrush("AccentFillColorDefaultBrush",   0x74, 0x4D, 0xA9);
            SetBrush("AccentFillColorSecondaryBrush", 0x85, 0x5D, 0xB9);
            SetBrush("AccentFillColorTertiaryBrush",  0x5D, 0x3A, 0x8F);
            SetBrush("AccentTextFillColorPrimaryBrush",   0xC4, 0xA8, 0xE8);
            SetBrush("AccentTextFillColorSecondaryBrush", 0xB9, 0x9B, 0xE0);
            SetBrush("AccentTextFillColorTertiaryBrush",  0x9B, 0x7A, 0xC9);
        }
        catch (Exception ex)
        {
            LogAndShow(ex);
        }
    }

    private void SetBrush(string key, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        Resources[key] = brush;
    }

    private static void LogAndShow(Exception ex)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CryptSage", "crash.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath,
                $"[{DateTime.Now:O}]{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch { /* best-effort */ }

        MessageBox.Show(
            $"Unhandled exception:{Environment.NewLine}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{Environment.NewLine}Log: {logPath}",
            "Crypt Sage — Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
