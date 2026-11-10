using System.Windows;
using System.Windows.Controls;

namespace MediaTech_AppForge.Views
{
    /// <summary>Page provisoire : "à venir", ou "connexion requise" pour un invité.</summary>
    public partial class PlaceholderView : UserControl
    {
        public PlaceholderView(string title, string message, bool showLogin = false)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            LoginButton.Visibility = showLogin ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
            => new LoginWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
