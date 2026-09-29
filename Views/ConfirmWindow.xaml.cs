using System.Windows;

namespace MediaTech_AppForge.Views
{
    /// <summary>Confirmation au thème de l'application (le MessageBox système est blanc).</summary>
    public partial class ConfirmWindow : Window
    {
        private ConfirmWindow(string title, string message, string confirmText)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            ConfirmButton.Content = confirmText;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        public static bool Ask(Window? owner, string title, string message, string confirmText = "Supprimer")
            => new ConfirmWindow(title, message, confirmText) { Owner = owner }.ShowDialog() == true;
    }
}
