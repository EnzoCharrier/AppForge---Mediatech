using System.Windows;
using System.Windows.Controls;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    /// <summary>Coque de l'administration : onglets Médias / Auteurs / Éditeurs / Genres / Utilisateurs.</summary>
    public partial class GestionView : UserControl
    {
        public GestionView()
        {
            InitializeComponent();

            // Le bouton est déjà masqué aux non-admins ; ceci est une seconde barrière.
            SectionHost.Content = Session.IsAdmin
                ? new GestionMediasView()
                : new PlaceholderView("Accès réservé", "Cette section est réservée aux administrateurs.");
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (!Session.IsAdmin) return;

            SectionHost.Content = ((RadioButton)sender).Name switch
            {
                nameof(TabMedias) => new GestionMediasView(),
                nameof(TabAuteurs) => new GestionRefsView(RefKind.Auteur),
                nameof(TabEditeurs) => new GestionRefsView(RefKind.Editeur),
                nameof(TabGenres) => new GestionRefsView(RefKind.Genre),
                _ => new GestionUsersView(),
            };
        }
    }
}
