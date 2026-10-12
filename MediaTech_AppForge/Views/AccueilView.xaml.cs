using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    /// <summary>Page d'accueil "vitrine" : logo, bandeau d'accueil et couvertures des derniers ajouts.</summary>
    public partial class AccueilView : UserControl
    {
        // Deux dégradés par type : les couvertures varient sans perdre le code couleur du type.
        private static readonly Dictionary<MediaKind, (string From, string To)[]> Palettes = new()
        {
            [MediaKind.Livre] = new[] { ("#FF8B5CF6", "#FF4C1D95"), ("#FF6366F1", "#FF312E81") },
            [MediaKind.Magazine] = new[] { ("#FFF472B6", "#FF9D174D"), ("#FFFB7185", "#FFBE123C") },
            [MediaKind.DVD] = new[] { ("#FF38BDF8", "#FF1D4ED8"), ("#FF0EA5E9", "#FF0C4A6E") },
            [MediaKind.CD] = new[] { ("#FF34D399", "#FF047857"), ("#FF22C55E", "#FF166534") },
        };

        private static string KindIcon(MediaKind k) => k switch
        {
            MediaKind.Livre => "\U0001F4D6",     // 📖
            MediaKind.Magazine => "\U0001F4F0",  // 📰
            MediaKind.DVD => "\U0001F3AC",       // 🎬
            _ => "\U0001F4BF",                   // 💿
        };

        private static Color ToColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        public AccueilView()
        {
            InitializeComponent();

            var user = Session.CurrentUser;
            GreetingText.Text = user is null ? "Bienvenue" : $"Bonjour, {user.Username}";
            LoginButton.Visibility = user is null ? Visibility.Visible : Visibility.Collapsed;

            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var showcase = await HomeService.LoadShowcaseAsync();

                if (showcase.Total > 0)
                {
                    TotalText.Text = CatalogueService.Plural(showcase.Total, "titre", "titres") + " à découvrir";
                    TotalBadge.Visibility = Visibility.Visible;
                }

                ShowcasePanel.Children.Clear();
                foreach (var row in showcase.Latest)
                    ShowcasePanel.Children.Add(BuildTile(row));
                ShowcaseSection.Visibility = showcase.Latest.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                // La page reste belle sans données : on signale juste discrètement le problème.
                Debug.WriteLine(ex);
                StatusText.Text = "Les derniers ajouts n'ont pas pu être chargés : " + ex.GetBaseException().Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Une "couverture" : dégradé de couleur + pictogramme du type + titre en bas.</summary>
        private Border BuildTile(MediaRow row)
        {
            var palette = Palettes[row.Kind][row.Id % 2];
            var background = new LinearGradientBrush(ToColor(palette.From), ToColor(palette.To), 55);

            var grid = new Grid();

            grid.Children.Add(new TextBlock
            {
                Text = KindIcon(row.Kind), FontSize = 64, Opacity = 0.3,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 40, 0, 0),
            });

            grid.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = CatalogueService.SingularLabel(row.Kind).ToUpperInvariant(),
                    FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                },
            });

            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = row.Titre, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 38,
            });
            info.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(row.Auteur) ? row.Editeur : row.Auteur,
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)),
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0),
            });

            // Voile sombre sous le texte pour qu'il reste lisible sur toutes les couleurs.
            grid.Children.Add(new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(12, 30, 12, 12),
                Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(215, 0, 0, 0), 90),
                Child = info,
            });

            var scale = new ScaleTransform(1, 1);
            var tile = new Border
            {
                Width = 160, Height = 220,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 14, 14),
                Background = background,
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = grid,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale,
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.4, Color = Colors.Black },
            };

            tile.MouseEnter += (_, _) => scale.ScaleX = scale.ScaleY = 1.04;
            tile.MouseLeave += (_, _) => scale.ScaleX = scale.ScaleY = 1;
            tile.MouseLeftButtonUp += (_, _) => Navigator.Show(new MediaDetailView(row.Kind, row.Id, this));

            return tile;
        }

        private void Catalogue_Click(object sender, RoutedEventArgs e) => Navigator.GoTo(NavTarget.Catalogue);

        private void Login_Click(object sender, RoutedEventArgs e)
            => new LoginWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
