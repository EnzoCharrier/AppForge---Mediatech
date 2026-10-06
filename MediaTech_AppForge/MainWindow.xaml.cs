using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaTech_AppForge.Services;
using MediaTech_AppForge.Views;

namespace MediaTech_AppForge
{
    public partial class MainWindow : Window
    {
        /// 
        /// 
        /// 
        /// 
        /// 
        /// 
        /// 
        /// A RELIRE
        ///.
        ///.
        ///.
        ///.
        ///.
        private RadioButton? _current;
        private readonly DispatcherTimer _notifTimer = new() { Interval = TimeSpan.FromSeconds(60) };

        public MainWindow()
        {
            InitializeComponent();

            Navigator.Handler = page => MainContent.Content = page;
            Session.Changed += RefreshSession;
            NotificationService.Changed += OnNotificationsChanged;

            // Les notifications globales publiées ailleurs (admin) arrivent via ce rafraîchissement périodique.
            _notifTimer.Tick += (_, _) => RefreshBadge(generate: false);
            _notifTimer.Start();

            Closed += (_, _) =>
            {
                _notifTimer.Stop();
                Navigator.Handler = null;
                Session.Changed -= RefreshSession;
                NotificationService.Changed -= OnNotificationsChanged;
            };

            RefreshSession();
            SelectNav(NavCatalogue);
        }

        // Click (et non Checked) : recliquer sur "Catalogue" depuis une liste ou une fiche revient à l'accueil du catalogue.
        private void Nav_Click(object sender, RoutedEventArgs e) => ShowPage((RadioButton)sender);

        private void SelectNav(RadioButton nav)
        {
            nav.IsChecked = true;
            ShowPage(nav);
        }

        private void ShowPage(RadioButton nav)
        {
            _current = nav;
            bool guest = !Session.IsLoggedIn;

            MainContent.Content = nav.Name switch
            {
                nameof(NavCatalogue) => new CatalogueView(),
                nameof(NavEmprunts) when guest => Locked("Mes emprunts"),
                nameof(NavNotifications) when guest => Locked("Notifications"),
                nameof(NavEmprunts) => new MesEmpruntsView(),
                nameof(NavNotifications) => new NotificationsView(),
                nameof(NavGestion) => new GestionView(),
                _ => new PlaceholderView("Accueil", "Bientôt disponible."),
            };
        }

        private static PlaceholderView Locked(string title)
            => new(title, "Connectez-vous pour accéder à cette section. En mode invité, vous pouvez consulter le catalogue.", showLogin: true);

        private void RefreshSession()
        {
            bool logged = Session.IsLoggedIn;

            GuestPanel.Visibility = logged ? Visibility.Collapsed : Visibility.Visible;
            UserPanel.Visibility = logged ? Visibility.Visible : Visibility.Collapsed;

            var adminVis = Session.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            AdminHeader.Visibility = adminVis;
            NavGestion.Visibility = adminVis;

            if (logged)
            {
                var name = Session.CurrentUser!.Username;
                UserNameText.Text = name;
                UserRoleText.Text = Session.IsAdmin ? "Administrateur" : "Membre";
                AvatarInitial.Text = name.Length > 0 ? name[..1].ToUpperInvariant() : "?";
            }

            // À la connexion : génère les rappels de retour puis met à jour la pastille.
            RefreshBadge(generate: logged);

            if (_current == null) return; // premier appel, avant la navigation initiale

            if (_current == NavGestion && !Session.IsAdmin)
                SelectNav(NavCatalogue);
            else if (_current == NavEmprunts || _current == NavNotifications || _current == NavGestion)
                ShowPage(_current); // pages dépendantes de la session : on les réévalue
            // Catalogue / Accueil : on ne touche pas à la page affichée (fiche ouverte, recherche en cours...).
        }

        private void OnNotificationsChanged() => RefreshBadge(generate: false);

        private async void RefreshBadge(bool generate)
        {
            if (Session.CurrentUser is not { } user)
            {
                SetNotificationBadge(0);
                return;
            }

            try
            {
                if (generate) await NotificationService.GenerateRemindersAsync(user.Id);
                int unread = await NotificationService.CountUnreadAsync(user.Id);

                // L'utilisateur a pu se déconnecter pendant la requête.
                if (Session.CurrentUser?.Id == user.Id) SetNotificationBadge(unread);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void SetNotificationBadge(int unread)
        {
            if (unread <= 0)
            {
                NavNotifications.Content = "Notifications";
                return;
            }

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = "Notifications", VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new Border
            {
                Background = (Brush)FindResource("Accent"),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = unread > 99 ? "99+" : unread.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                },
            });
            NavNotifications.Content = panel;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
            => new LoginWindow { Owner = this }.ShowDialog();

        private void Logout_Click(object sender, RoutedEventArgs e)
            => Session.SignOut();
    }
}
