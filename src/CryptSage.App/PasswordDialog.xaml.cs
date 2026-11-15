using System.Windows;
using Wpf.Ui.Controls;

namespace CryptSage.App;

public partial class PasswordDialog : FluentWindow
{
    private PasswordDialog(string prompt)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public string? Result { get; private set; }

    public static string? Prompt(Window owner, string promptText)
    {
        var dlg = new PasswordDialog(promptText) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = PasswordInput.Password;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
