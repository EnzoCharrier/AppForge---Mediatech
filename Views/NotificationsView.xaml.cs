using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class NotificationsView : UserControl
    {
        private List<NotificationRow> _rows = new();
        private bool _ready;

        public NotificationsView()
        {
            InitializeComponent();
            // Rechargé à chaque affichage (retour depuis une fiche, nouveau rappel à générer...).
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (Session.CurrentUser is not { } user) return;

            try
            {
                await NotificationService.GenerateRemindersAsync(user.Id);
                _rows = await NotificationService.GetForUserAsync(user.Id);
                _ready = true;
                StatusText.Visibility = Visibility.Collapsed;
                ActionMessage.Visibility = Visibility.Collapsed;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Impossible de charger les notifications.\n" + ex.GetBaseException().Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (!_ready) return;

            bool onlyUnread = FilterUnread.IsChecked == true;
            var shown = onlyUnread ? _rows.Where(r => !r.IsRead).ToList() : _rows;
            NotifList.ItemsSource = shown;

            UpdateCount();
            EmptyText.Text = onlyUnread ? "Aucune notification non lue." : "Aucune notification.";
            EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCount()
        {
            int unread = _rows.Count(r => !r.IsRead);
            CountText.Text = CatalogueService.Plural(unread, "non lue", "non lues");
            MarkAllButton.IsEnabled = unread > 0;
        }

        // Clic : marque comme lue (création de la ligne EstLue = true si globale), puis ouvre le média concerné.
        private async void Row_Click(object sender, MouseButtonEventArgs e)
        {
            if (Session.CurrentUser is not { } user) return;
            if (((FrameworkElement)sender).DataContext is not NotificationRow row) return;

            try
            {
                if (!row.IsRead)
                {
                    await NotificationService.MarkReadAsync(user.Id, row.Id);
                    row.IsRead = true;
                    UpdateCount();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ActionMessage.Text = "Impossible de marquer la notification comme lue.";
                ActionMessage.Visibility = Visibility.Visible;
                return;
            }

            if (row.TargetMediaId is int mediaId && row.Kind is MediaKind kind)
                Navigator.Show(new MediaDetailView(kind, mediaId, this));
        }

        private async void MarkAll_Click(object sender, RoutedEventArgs e)
        {
            if (Session.CurrentUser is not { } user) return;

            MarkAllButton.IsEnabled = false;
            try
            {
                await NotificationService.MarkAllReadAsync(user.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ActionMessage.Text = "Impossible de tout marquer comme lu.";
                ActionMessage.Visibility = Visibility.Visible;
                MarkAllButton.IsEnabled = true;
            }
        }
    }
}
