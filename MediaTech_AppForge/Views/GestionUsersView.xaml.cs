using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaTech_AppForge.Models;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class GestionUsersView : UserControl
    {
        private int? _editingId;

        public GestionUsersView()
        {
            InitializeComponent();
            ResetForm();
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                UserList.ItemsSource = await AdminDataService.GetUsersAsync();
                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Impossible de charger les utilisateurs.\n" + ex.GetBaseException().Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        private EtatUtilisateur SelectedEtat =>
            EtatSuspendu.IsChecked == true ? EtatUtilisateur.Suspendu :
            EtatBanni.IsChecked == true ? EtatUtilisateur.Banni : EtatUtilisateur.Actif;

        private void SetEtat(EtatUtilisateur etat)
        {
            EtatActif.IsChecked = etat == EtatUtilisateur.Actif;
            EtatSuspendu.IsChecked = etat == EtatUtilisateur.Suspendu;
            EtatBanni.IsChecked = etat == EtatUtilisateur.Banni;
        }

        // Pour son propre compte, rôle et état sont verrouillés (on ne peut pas se retirer l'accès).
        private void LockRights(bool locked)
        {
            foreach (Control c in new Control[] { AdminCheck, EtatActif, EtatSuspendu, EtatBanni })
            {
                c.IsEnabled = !locked;
                c.Opacity = locked ? 0.5 : 1;
            }
        }

        private void ResetForm()
        {
            _editingId = null;
            UsernameBox.Text = "";
            PasswordBox.Clear();
            AdminCheck.IsChecked = false;
            SetEtat(EtatUtilisateur.Actif);
            LockRights(false);

            FormTitle.Text = "Ajouter un utilisateur";
            PasswordHint.Text = "Obligatoire pour un nouveau compte (6 caractères minimum).";
            SaveButton.Content = "Ajouter";
            CancelButton.Visibility = Visibility.Collapsed;
            FormError.Visibility = Visibility.Collapsed;
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not UserRow row) return;

            _editingId = row.Id;
            UsernameBox.Text = row.Username;
            PasswordBox.Clear();
            AdminCheck.IsChecked = row.IsAdmin;
            SetEtat(row.Etat);
            LockRights(row.Id == Session.CurrentUser?.Id);

            FormTitle.Text = $"Modifier « {row.Username} »";
            PasswordHint.Text = "Laisser vide pour conserver le mot de passe actuel.";
            SaveButton.Content = "Enregistrer";
            CancelButton.Visibility = Visibility.Visible;
            FormError.Visibility = Visibility.Collapsed;

            FormTitle.BringIntoView();
            UsernameBox.Focus();
            UsernameBox.CaretIndex = UsernameBox.Text.Length;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => ResetForm();

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            FormError.Visibility = Visibility.Collapsed;
            var username = UsernameBox.Text.Trim();
            bool creating = _editingId is null;

            SaveButton.IsEnabled = false;
            try
            {
                var (ok, error) = await AdminDataService.SaveUserAsync(
                    _editingId, username, PasswordBox.Password, AdminCheck.IsChecked == true, SelectedEtat);

                if (!ok)
                {
                    FormError.Text = error;
                    FormError.Visibility = Visibility.Visible;
                    return;
                }

                ShowMessage(creating ? $"Le compte « {username} » a été créé." : $"Le compte « {username} » a été modifié.", true);
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
            if (((FrameworkElement)sender).DataContext is not UserRow row) return;

            if (!ConfirmWindow.Ask(Window.GetWindow(this), "Supprimer ce compte ?",
                    $"« {row.Username} » sera supprimé définitivement, avec son historique d'emprunts et ses notifications."))
                return;

            try
            {
                var (ok, error) = await AdminDataService.DeleteUserAsync(row.Id);
                ShowMessage(ok ? $"Le compte « {row.Username} » a été supprimé." : error!, ok);
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
