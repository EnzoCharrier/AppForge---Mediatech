using System.Windows;
using System.Windows.Controls;
using MediaTech_AppForge.Services;
using MediaTech_AppForge.Views;

namespace MediaTech_AppForge
{
    public partial class MainWindow : Window
    {
        private RadioButton? _current;

        public MainWindow()
        {
            InitializeComponent();

            Navigator.Handler = page => MainContent.Content = page;
            Session.Changed += RefreshSession;
            Closed += (_, _) =>
            {
                Navigator.Handler = null;
                Session.Changed -= RefreshSession;
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
                nameof(NavNotifications) => new PlaceholderView("Notifications", "Bientôt disponible."),
                nameof(NavGestion) => new PlaceholderView("Gestion des contenus", "Bientôt disponible."),
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

            if (_current == null) return; // premier appel, avant la navigation initiale

            if (_current == NavGestion && !Session.IsAdmin)
                SelectNav(NavCatalogue);
            else if (_current == NavEmprunts || _current == NavNotifications || _current == NavGestion)
                ShowPage(_current); // pages dépendantes de la session : on les réévalue
            // Catalogue / Accueil : on ne touche pas à la page affichée (fiche ouverte, recherche en cours...).
        }

        private void Login_Click(object sender, RoutedEventArgs e)
            => new LoginWindow { Owner = this }.ShowDialog();

        private void Logout_Click(object sender, RoutedEventArgs e)
            => Session.SignOut();
    }
}
