using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class CatalogueView : UserControl
    {
        public CatalogueView()
        {
            InitializeComponent();
            Loaded += CatalogueView_Loaded;
        }

        private async void CatalogueView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await using var db = new MediaTechContext();

                LivresCountText.Text = FormatCount(await db.Livres.CountAsync());
                MagazinesCountText.Text = FormatCount(await db.Magazines.CountAsync());
                DvdCountText.Text = FormatCount(await db.DVDs.CountAsync());
                CdCountText.Text = FormatCount(await db.CDs.CountAsync());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                LivresCountText.Text = MagazinesCountText.Text =
                    DvdCountText.Text = CdCountText.Text = "Indisponible";
            }
        }

        private static string FormatCount(int n) => CatalogueService.Plural(n, "titre", "titres");

        // Le Tag de chaque carte contient le nom de l'enum MediaKind.
        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            var kind = Enum.Parse<MediaKind>((string)((FrameworkElement)sender).Tag);
            Navigator.Show(new MediaListView(kind));
        }

        // Entrée dans la barre de recherche : résultats sur tous les types.
        private void RootSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            Navigator.Show(new MediaListView(null, RootSearch.Text));
        }
    }
}
