using System.Windows;
using Wpf.Ui.Controls;

namespace CryptSage.App;

public partial class ExitConfirmDialog : FluentWindow
{
    private ExitConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    public bool DismountOnExit { get; private set; }

    public static bool? Show(Window owner, string message, out bool dismountOnExit)
    {
        var dlg = new ExitConfirmDialog(message) { Owner = owner };
        var ok = dlg.ShowDialog() == true;
        dismountOnExit = ok && dlg.DismountOnExit;
        return ok;
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        DismountOnExit = DismountCheck.IsChecked == true;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
