using System;
using System.Diagnostics;
using System.Windows;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => UsernameBox.Focus();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;
            if (username.Length == 0 || password.Length == 0)
            {
                ShowError("Renseignez l'identifiant et le mot de passe.");
                return;
            }

            LoginButton.IsEnabled = false;
            try
            {
                var (user, error) = await AuthService.LoginAsync(username, password);
                if (user is null) { ShowError(error!); return; }

                Session.SignIn(user);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowError("Impossible de joindre la base de données.");
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
