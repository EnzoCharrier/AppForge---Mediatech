using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class MesEmpruntsView : UserControl
    {
        public MesEmpruntsView()
        {
            InitializeComponent();
            // Pas de garde : on recharge à chaque affichage (retour depuis une fiche, etc.).
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (Session.CurrentUser is not { } user) return;

            try
            {
                var rows = await EmpruntService.GetUserLoansAsync(user.Id);
                var active = rows.Where(r => r.IsActive).OrderBy(r => r.DateRetourPrevue).ToList();
                var history = rows.Where(r => !r.IsActive).OrderByDescending(r => r.DateRetour).ToList();

                ActiveList.ItemsSource = active;
                HistoryList.ItemsSource = history;

                CountText.Text = $"{active.Count} en cours";
                ActiveEmpty.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                HistoryPanel.Visibility = history.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Impossible de charger vos emprunts.\n" + ex.GetBaseException().Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        private async void Return_Click(object sender, RoutedEventArgs e)
        {
            if (Session.CurrentUser is not { } user) return;
            if (((FrameworkElement)sender).DataContext is not EmpruntRow row) return;

            var button = (Button)sender;
            button.IsEnabled = false;
            try
            {
                var (ok, error) = await EmpruntService.ReturnAsync(user.Id, row.EmpruntId);
                ShowMessage(ok ? $"« {row.Titre} » a bien été rendu." : error!, ok);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowMessage("Erreur lors du retour. Réessayez.", false);
                button.IsEnabled = true;
            }
        }

        private void Row_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is EmpruntRow row)
                Navigator.Show(new MediaDetailView(row.Kind, row.MediaId, this));
        }

        private void ShowMessage(string text, bool ok)
        {
            ActionMessage.Text = text;
            ActionMessage.Foreground = (System.Windows.Media.Brush)FindResource(ok ? "Accent" : "Danger");
            ActionMessage.Visibility = Visibility.Visible;
        }
    }
}
