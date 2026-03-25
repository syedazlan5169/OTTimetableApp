using OTTimetableApp.Services;
using System.Windows;

namespace OTTimetableApp;

public partial class ChangePasswordWindow : Window
{
    private readonly AdminAuthService _authSvc;

    public ChangePasswordWindow(AdminAuthService authSvc)
    {
        InitializeComponent();
        _authSvc = authSvc;
        Loaded += (_, _) => CurrentPasswordBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var current = CurrentPasswordBox.Password;
        var newPw = NewPasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(newPw))
        {
            ShowError("New password cannot be empty.");
            return;
        }

        if (newPw != confirm)
        {
            ShowError("New password and confirmation do not match.");
            return;
        }

        if (!_authSvc.ChangePassword(current, newPw))
        {
            ShowError("Current password is incorrect.");
            CurrentPasswordBox.Clear();
            CurrentPasswordBox.Focus();
            return;
        }

        MessageBox.Show("Password changed successfully.", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
