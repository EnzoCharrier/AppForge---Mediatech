using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    public partial class MediaDetailView : UserControl
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        private readonly MediaKind _kind;
        private readonly int _id;
        private readonly UserControl _backTo;
        private MediaDetail? _detail;
        private ActiveLoan? _activeLoan;
        private bool _busy;
        private bool _loaded;

        public MediaDetailView(MediaKind kind, int id, UserControl backTo)
        {
            InitializeComponent();
            _kind = kind;
            _id = id;
            _backTo = backTo;

            KindText.Text = CatalogueService.SingularLabel(kind).ToUpperInvariant();
            UpdateBorrowState();

            Loaded += MediaDetailView_Loaded;
            Unloaded += (_, _) => Session.Changed -= OnSessionChanged;
        }

        private async void MediaDetailView_Loaded(object sender, RoutedEventArgs e)
        {
            Session.Changed -= OnSessionChanged;
            Session.Changed += OnSessionChanged;

            if (_loaded) return;
            _loaded = true;

            try
            {
                var d = await CatalogueService.GetDetailAsync(_kind, _id);
                if (d is null) { StatusText.Text = "Ce média n'existe plus."; return; }

                _detail = d;
                Bind(d);
                await RefreshLoanAsync();
                UpdateBorrowState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Impossible de charger la fiche.\n" + ex.GetBaseException().Message;
                _loaded = false;
            }
        }

        // Connexion / déconnexion pendant que la fiche est ouverte.
        private async void OnSessionChanged()
        {
            try
            {
                BorrowMessage.Visibility = Visibility.Collapsed;
                await RefreshLoanAsync();
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
            UpdateBorrowState();
        }

        private async Task RefreshLoanAsync()
            => _activeLoan = Session.CurrentUser is { } u
                ? await EmpruntService.GetActiveLoanAsync(u.Id, _id)
                : null;

        private void Bind(MediaDetail d)
        {
            TitleText.Text = d.Titre;
            if (!string.IsNullOrEmpty(d.Auteur))
            {
                AuthorText.Text = d.Auteur;
                AuthorText.Visibility = Visibility.Visible;
            }

            GenresList.ItemsSource = d.Genres;
            FieldsList.ItemsSource = d.Fields;
            StockText.Text = d.Stock switch
            {
                <= 0 => "Aucun exemplaire physique en stock",
                1 => "1 exemplaire en stock",
                _ => $"{d.Stock} exemplaires en stock",
            };

            if (!string.IsNullOrWhiteSpace(d.Resume))
            {
                ResumeText.Text = d.Resume;
                ResumePanel.Visibility = Visibility.Visible;
            }
            if (d.Pistes.Count > 0)
            {
                PistesList.ItemsSource = d.Pistes;
                PistesPanel.Visibility = Visibility.Visible;
            }

            StatusText.Visibility = Visibility.Collapsed;
            BodyPanel.Visibility = Visibility.Visible;
        }

        // Invité : bouton de connexion. Connecté : choix physique / numérique,
        // ou information si le média est déjà emprunté.
        private void UpdateBorrowState()
        {
            bool logged = Session.IsLoggedIn;
            bool canBorrow = logged && _detail != null && _activeLoan == null;

            LoginButton.Visibility = logged ? Visibility.Collapsed : Visibility.Visible;

            PhysicalButton.Visibility = canBorrow ? Visibility.Visible : Visibility.Collapsed;
            PhysicalButton.IsEnabled = !_busy && _detail?.Stock > 0;

            DigitalButton.Visibility = canBorrow && EmpruntService.NumeriqueDisponible(_kind)
                ? Visibility.Visible : Visibility.Collapsed;
            DigitalButton.IsEnabled = !_busy;

            if (!logged)
                BorrowHint.Text = "Vous consultez en tant qu'invité.";
            else if (_activeLoan is { } loan)
                BorrowHint.Text = $"Vous avez déjà emprunté ce média (à rendre avant le {loan.DueDate.ToString("d MMMM yyyy", Fr)}). " +
                                  "Retrouvez-le dans « Mes emprunts ».";
            else if (canBorrow)
            {
                var due = EmpruntService.Today().AddDays(EmpruntService.DureeJours).ToString("d MMMM yyyy", Fr);
                BorrowHint.Text = $"Prêt de {EmpruntService.DureeJours} jours · retour prévu le {due}" +
                                  (_detail!.Stock <= 0 ? " · exemplaire physique indisponible" : "");
            }
            else
                BorrowHint.Text = "";
        }

        private async void Borrow_Click(object sender, RoutedEventArgs e)
        {
            if (Session.CurrentUser is not { } user || _detail is null) return;

            bool physique = ReferenceEquals(sender, PhysicalButton);
            _busy = true;
            UpdateBorrowState();

            try
            {
                var r = await EmpruntService.BorrowAsync(user.Id, _id, physique);
                if (r.Success)
                    ShowMessage($"Emprunt enregistré. À rendre avant le {r.DueDate!.Value.ToString("d MMMM yyyy", Fr)}.", true);
                else
                    ShowMessage(r.Error!, false);

                // Stock et état de l'emprunt à jour, dans les deux cas (ex. dernier exemplaire pris par quelqu'un d'autre).
                var d = await CatalogueService.GetDetailAsync(_kind, _id);
                if (d != null) { _detail = d; Bind(d); }
                await RefreshLoanAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowMessage("Erreur lors de l'emprunt. Réessayez.", false);
            }
            finally
            {
                _busy = false;
                UpdateBorrowState();
            }
        }

        private void ShowMessage(string text, bool ok)
        {
            BorrowMessage.Text = text;
            BorrowMessage.Foreground = (Brush)FindResource(ok ? "Accent" : "Danger");
            BorrowMessage.Visibility = Visibility.Visible;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
            => new LoginWindow { Owner = Window.GetWindow(this) }.ShowDialog();

        private void Back_Click(object sender, RoutedEventArgs e) => Navigator.Show(_backTo);
    }
}
