using OTTimetableApp.Services;
using System.Windows;
using System.Windows.Input;

namespace OTTimetableApp;

public partial class AdminLoginWindow : Window
{
    private readonly AdminAuthService _authSvc;

    public bool Authenticated { get; private set; }

    public AdminLoginWindow(AdminAuthService authSvc)
    {
        InitializeComponent();
        _authSvc = authSvc;
        UsernameBox.Text = "admin";
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void Login_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void TryLogin()
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (_authSvc.Verify(UsernameBox.Text.Trim(), PasswordBox.Password))
        {
            Authenticated = true;
            Close();
        }
        else
        {
            ErrorText.Visibility = Visibility.Visible;
            PasswordBox.Clear();
            PasswordBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
