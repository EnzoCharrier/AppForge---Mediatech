using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class GestionMediasView : UserControl
    {
        private List<MediaRow> _all = new();
        private bool _ready;

        public GestionMediasView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var (rows, _) = await CatalogueService.LoadAsync(null); // tous les types
                _all = rows;
                _ready = true;
                StatusText.Visibility = Visibility.Collapsed;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Impossible de charger les médias.\n" + ex.GetBaseException().Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (!_ready) return;

            var q = CatalogueService.Normalize(SearchInput.Text);
            var list = q.Length == 0 ? _all : _all.Where(r => r.SearchText.Contains(q)).ToList();

            MediaList.ItemsSource = list;
            CountText.Text = CatalogueService.Plural(list.Count, "média", "médias");
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void New_Click(object sender, RoutedEventArgs e) => OpenEditor(null, null);

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is MediaRow row)
                OpenEditor(row.Id, row.Kind);
        }

        private async void OpenEditor(int? id, MediaKind? kind)
        {
            var editor = new MediaEditorWindow(id, kind) { Owner = Window.GetWindow(this) };
            if (editor.ShowDialog() != true) return;

            ShowMessage(editor.Created
                ? $"« {editor.SavedTitle} » a été ajouté au catalogue."
                : $"« {editor.SavedTitle} » a été modifié.", true);
            await LoadAsync();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not MediaRow row) return;

            if (!ConfirmWindow.Ask(Window.GetWindow(this), "Supprimer ce média ?",
                    $"« {row.Titre} » sera définitivement retiré du catalogue, avec ses pistes et ses genres."))
                return;

            try
            {
                var (ok, error) = await AdminService.DeleteMediaAsync(row.Id);
                ShowMessage(ok ? $"« {row.Titre} » a été supprimé." : error!, ok);
                if (ok) await LoadAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowMessage("Suppression impossible : " + ex.GetBaseException().Message, false);
            }
        }

        private void ShowMessage(string text, bool ok)
        {
            ActionMessage.Text = text;
            ActionMessage.Foreground = (Brush)FindResource(ok ? "Accent" : "Danger");
            ActionMessage.Visibility = Visibility.Visible;
        }
    }
}
