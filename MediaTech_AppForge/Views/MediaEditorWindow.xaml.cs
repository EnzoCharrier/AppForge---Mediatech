using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    /// <summary>Formulaire de création / modification d'un média (id == null : création).</summary>
    public partial class MediaEditorWindow : Window
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        private readonly int? _id;
        private MediaKind _kind = MediaKind.Livre;
        private readonly List<CheckBox> _genreChecks = new();

        public string? SavedTitle { get; private set; }
        public bool Created { get; private set; }

        public MediaEditorWindow(int? id, MediaKind? kind)
        {
            InitializeComponent();
            _id = id;
            if (kind is MediaKind k) _kind = k;

            HeaderText.Text = id is null ? "Nouveau média" : "Modifier le média";
            Title = HeaderText.Text;

            Loaded += async (_, _) => await InitAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                var lookups = await AdminService.GetLookupsAsync();
                EditeurBox.SetItems(lookups.Editeurs);
                AuteurBox.SetItems(lookups.Auteurs);
                BuildGenreChips(lookups.Genres);

                MediaEditModel? model = null;
                if (_id is int id)
                {
                    model = await AdminService.GetMediaForEditAsync(id, _kind);
                    if (model is null) { StatusText.Text = "Ce média n'existe plus."; return; }
                }

                BuildTypeSelector();
                SetKind(_kind);
                if (model != null) Fill(model);

                NotifyCheck.Visibility = _id is null ? Visibility.Visible : Visibility.Collapsed;
                StatusText.Visibility = Visibility.Collapsed;
                FormPanel.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = true;
                SaveButton.IsDefault = true;
                TitreBox.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Chargement impossible : " + ex.GetBaseException().Message;
            }
        }

        // ---------------- Construction du formulaire ----------------
        private void BuildTypeSelector()
        {
            if (_id is not null)
            {
                // Le type d'un média existant ne change pas (il est porté par sa table fille).
                TypeChips.Visibility = Visibility.Collapsed;
                TypeFixedText.Text = CatalogueService.SingularLabel(_kind);
                TypeFixedText.Visibility = Visibility.Visible;
                return;
            }

            foreach (var k in Enum.GetValues<MediaKind>())
            {
                var rb = new RadioButton
                {
                    Content = CatalogueService.SingularLabel(k),
                    GroupName = "EditorType",
                    Style = (Style)FindResource("ChipButton"),
                    IsChecked = k == _kind,
                };
                var kind = k;
                rb.Checked += (_, _) => SetKind(kind);
                TypeChips.Children.Add(rb);
            }
        }

        private void BuildGenreChips(List<GenreInfo> genres)
        {
            var byId = genres.ToDictionary(g => g.Id);
            var labelled = genres
                .Select(g => (Genre: g,
                              Label: g.ParentId is int pid && byId.TryGetValue(pid, out var p)
                                     ? $"{p.Libelle} › {g.Libelle}" : g.Libelle))
                .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase);

            foreach (var (g, label) in labelled)
            {
                var cb = new CheckBox { Content = label, Tag = g.Id, Style = (Style)FindResource("ChipCheck") };
                _genreChecks.Add(cb);
                GenreChips.Children.Add(cb);
            }
            GenreEmpty.Visibility = genres.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetKind(MediaKind kind)
        {
            _kind = kind;
            LivrePanel.Visibility = kind == MediaKind.Livre ? Visibility.Visible : Visibility.Collapsed;
            MagazinePanel.Visibility = kind == MediaKind.Magazine ? Visibility.Visible : Visibility.Collapsed;
            DvdPanel.Visibility = kind == MediaKind.DVD ? Visibility.Visible : Visibility.Collapsed;
            CdPanel.Visibility = kind == MediaKind.CD ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Fill(MediaEditModel m)
        {
            TitreBox.Text = m.Titre;
            DateBox.Text = m.DateSortie?.ToString("dd/MM/yyyy", Fr) ?? "";
            StockBox.Text = m.Stock.ToString(CultureInfo.InvariantCulture);
            EditeurBox.Text = m.Editeur;
            AuteurBox.Text = m.Auteur ?? "";

            foreach (var cb in _genreChecks)
                cb.IsChecked = m.GenreIds.Contains((int)cb.Tag);

            switch (m.Kind)
            {
                case MediaKind.Livre:
                    LivrePagesBox.Text = m.NombreDePages?.ToString(CultureInfo.InvariantCulture) ?? "";
                    ResumeBox.Text = m.Resume ?? "";
                    break;
                case MediaKind.Magazine:
                    MagNumeroBox.Text = m.Numero?.ToString(CultureInfo.InvariantCulture) ?? "";
                    MagPagesBox.Text = m.NombreDePages?.ToString(CultureInfo.InvariantCulture) ?? "";
                    break;
                case MediaKind.DVD:
                    DvdDureeBox.Text = m.Duree is TimeSpan t ? $"{(int)t.TotalHours}:{t.Minutes:00}" : "";
                    break;
                case MediaKind.CD:
                    CdCountBox.Text = m.NombreDePiste?.ToString(CultureInfo.InvariantCulture) ?? "";
                    PistesBox.Text = string.Join(Environment.NewLine, m.Pistes.Select(p =>
                        p.Duree is TimeSpan d ? $"{p.Titre} ; {(int)d.TotalMinutes}:{d.Seconds:00}" : p.Titre));
                    break;
            }
        }

        // ---------------- Validation ----------------
        private static bool TryOptionalInt(string text, out int? value)
        {
            value = null;
            text = text.Trim();
            if (text.Length == 0) return true;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var v)) return false;
            value = v;
            return true;
        }

        private bool TryBuildModel(out MediaEditModel model, out string error)
        {
            model = new MediaEditModel { Kind = _kind };
            error = "";

            var titre = TitreBox.Text.Trim();
            if (titre.Length == 0) { error = "Le titre est obligatoire."; return false; }

            var editeur = EditeurBox.Text.Trim();
            if (editeur.Length == 0) { error = "L'éditeur est obligatoire."; return false; }

            DateOnly? date = null;
            var dateText = DateBox.Text.Trim();
            if (dateText.Length > 0)
            {
                if (!DateOnly.TryParseExact(dateText, new[] { "dd/MM/yyyy", "d/M/yyyy" }, Fr, DateTimeStyles.None, out var d))
                { error = "Date de sortie invalide (format jj/mm/aaaa)."; return false; }
                date = d;
            }

            int stock = 0;
            var stockText = StockBox.Text.Trim();
            if (stockText.Length > 0 &&
                !int.TryParse(stockText, NumberStyles.None, CultureInfo.InvariantCulture, out stock))
            { error = "Le stock doit être un entier positif ou nul."; return false; }

            model.Titre = titre;
            model.Editeur = editeur;
            model.Auteur = string.IsNullOrWhiteSpace(AuteurBox.Text) ? null : AuteurBox.Text.Trim();
            model.DateSortie = date;
            model.Stock = stock;
            model.GenreIds = _genreChecks.Where(c => c.IsChecked == true).Select(c => (int)c.Tag).ToList();

            switch (_kind)
            {
                case MediaKind.Livre:
                    if (!TryOptionalInt(LivrePagesBox.Text, out var lp)) { error = "Nombre de pages invalide."; return false; }
                    model.NombreDePages = lp;
                    model.Resume = ResumeBox.Text;
                    break;

                case MediaKind.Magazine:
                    if (!TryOptionalInt(MagNumeroBox.Text, out var num)) { error = "Numéro invalide."; return false; }
                    if (!TryOptionalInt(MagPagesBox.Text, out var mp)) { error = "Nombre de pages invalide."; return false; }
                    model.Numero = num;
                    model.NombreDePages = mp;
                    break;

                case MediaKind.DVD:
                    var dureeText = DvdDureeBox.Text.Trim();
                    if (dureeText.Length > 0)
                    {
                        if (!TimeSpan.TryParseExact(dureeText, new[] { @"h\:mm", @"h\:mm\:ss" }, CultureInfo.InvariantCulture, out var ts))
                        { error = "Durée invalide (format h:mm, ex. 2:49)."; return false; }
                        model.Duree = ts;
                    }
                    break;

                case MediaKind.CD:
                    if (!TryOptionalInt(CdCountBox.Text, out var np)) { error = "Nombre de pistes invalide."; return false; }
                    model.NombreDePiste = np;

                    var lines = PistesBox.Text.Split('\n').Select(l => l.Trim('\r', ' ', '\t')).Where(l => l.Length > 0).ToList();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        int sep = line.LastIndexOf(';');
                        var title = (sep < 0 ? line : line[..sep]).Trim();
                        var dur = sep < 0 ? "" : line[(sep + 1)..].Trim();

                        if (title.Length == 0) { error = $"Piste {i + 1} : titre manquant."; return false; }
                        if (title.Length > 150) { error = $"Piste {i + 1} : titre trop long (150 caractères max)."; return false; }

                        TimeSpan? duree = null;
                        if (dur.Length > 0)
                        {
                            if (!TimeSpan.TryParseExact(dur, new[] { @"m\:ss", @"h\:mm\:ss" }, CultureInfo.InvariantCulture, out var pd))
                            { error = $"Piste {i + 1} : durée invalide « {dur} » (format m:ss)."; return false; }
                            duree = pd;
                        }
                        model.Pistes.Add(new PisteInput(title, duree));
                    }
                    break;
            }
            return true;
        }

        // ---------------- Enregistrement ----------------
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (!TryBuildModel(out var model, out var error))
            {
                ShowError(error);
                return;
            }

            SaveButton.IsEnabled = false;
            try
            {
                await AdminService.SaveMediaAsync(_id, model, NotifyCheck.IsChecked == true);
                SavedTitle = model.Titre;
                Created = _id is null;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowError("Enregistrement impossible : " + ex.GetBaseException().Message);
                SaveButton.IsEnabled = true;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
