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
    /// <summary>Gestion d'une liste de références : auteurs, éditeurs ou genres (selon RefKind).</summary>
    public partial class GestionRefsView : UserControl
    {
        private readonly RefKind _kind;
        private int? _editingId;
        private List<RefRow> _rows = new();

        public GestionRefsView(RefKind kind)
        {
            InitializeComponent();
            _kind = kind;

            ParentPanel.Visibility = kind == RefKind.Genre ? Visibility.Visible : Visibility.Collapsed;
            NameLabel.Text = kind == RefKind.Genre ? "Libellé *" : "Nom *";
            ResetForm();

            Loaded += async (_, _) => await LoadAsync();
        }

        private static string Singular(RefKind k) => k switch { RefKind.Auteur => "auteur", RefKind.Editeur => "éditeur", _ => "genre" };
        private static string PluralName(RefKind k) => k switch { RefKind.Auteur => "auteurs", RefKind.Editeur => "éditeurs", _ => "genres" };

        private async Task LoadAsync()
        {
            try
            {
                _rows = await AdminDataService.GetRefsAsync(_kind);
                RefList.ItemsSource = _rows;
                RefreshParentSuggestions();

                StatusText.Visibility = Visibility.Collapsed;
                EmptyText.Text = $"Aucun {Singular(_kind)} pour le moment.";
                EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = $"Impossible de charger les {PluralName(_kind)}.\n" + ex.GetBaseException().Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        // Un genre ne peut pas être son propre parent : on ne le propose pas.
        private void RefreshParentSuggestions()
        {
            if (_kind == RefKind.Genre)
                ParentBox.SetItems(_rows.Where(r => r.Id != _editingId).Select(r => r.Nom));
        }

        private void ResetForm()
        {
            _editingId = null;
            NameBox.Text = "";
            ParentBox.Text = "";
            FormTitle.Text = $"Ajouter un {Singular(_kind)}";
            SaveButton.Content = "Ajouter";
            CancelButton.Visibility = Visibility.Collapsed;
            FormError.Visibility = Visibility.Collapsed;
            RefreshParentSuggestions();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not RefRow row) return;

            _editingId = row.Id;
            NameBox.Text = row.Nom;
            ParentBox.Text = row.ParentNom ?? "";
            FormTitle.Text = $"Modifier « {row.Nom} »";
            SaveButton.Content = "Enregistrer";
            CancelButton.Visibility = Visibility.Visible;
            FormError.Visibility = Visibility.Collapsed;
            RefreshParentSuggestions();

            FormTitle.BringIntoView();
            NameBox.Focus();
            NameBox.CaretIndex = NameBox.Text.Length;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => ResetForm();

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            FormError.Visibility = Visibility.Collapsed;
            var name = NameBox.Text.Trim();
            bool creating = _editingId is null;

            SaveButton.IsEnabled = false;
            try
            {
                var (ok, error) = await AdminDataService.SaveRefAsync(_kind, _editingId, name, ParentBox.Text);
                if (!ok)
                {
                    FormError.Text = error;
                    FormError.Visibility = Visibility.Visible;
                    return;
                }

                ShowMessage(creating ? $"« {name} » a été ajouté." : $"« {name} » a été modifié.", true);
                ResetForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                FormError.Text = "Enregistrement impossible : " + ex.GetBaseException().Message;
                FormError.Visibility = Visibility.Visible;
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not RefRow row) return;

            // Un éditeur utilisé ne peut pas être supprimé : inutile de demander confirmation.
            if (_kind == RefKind.Editeur && row.Usage > 0)
            {
                ShowMessage($"Suppression impossible : {row.UsageText} utilise(nt) cet éditeur. Modifiez d'abord ces médias.", false);
                return;
            }

            string consequence = (_kind, row.Usage) switch
            {
                (RefKind.Auteur, > 0) => $" {row.UsageText} resteront au catalogue, sans auteur.",
                (RefKind.Genre, > 0) => $" Il sera retiré de {row.UsageText}. Ses sous-genres éventuels deviendront des genres principaux.",
                (RefKind.Genre, _) => " Ses sous-genres éventuels deviendront des genres principaux.",
                _ => "",
            };

            if (!ConfirmWindow.Ask(Window.GetWindow(this), $"Supprimer ce {Singular(_kind)} ?",
                    $"« {row.Nom} » sera supprimé.{consequence}"))
                return;

            try
            {
                var (ok, error) = await AdminDataService.DeleteRefAsync(_kind, row.Id);
                ShowMessage(ok ? $"« {row.Nom} » a été supprimé." : error!, ok);
                if (!ok) return;

                if (_editingId == row.Id) ResetForm();
                await LoadAsync();
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
