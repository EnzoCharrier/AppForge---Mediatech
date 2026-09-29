using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    /// <summary>Liste d'un type de média (ou de tous si kind == null), avec recherche et filtre par genre.</summary>
    public partial class MediaListView : UserControl
    {
        private readonly MediaKind? _kind;
        private List<MediaRow> _all = new();
        private Dictionary<int, HashSet<int>> _subtree = new(); // genre -> lui-même + ses sous-genres
        private int? _genreFilter;
        private bool _dataReady;
        private bool _loaded;

        public MediaListView(MediaKind? kind, string? initialQuery = null)
        {
            InitializeComponent();
            _kind = kind;

            TitleText.Text = kind is MediaKind k ? CatalogueService.Label(k)
                           : string.IsNullOrWhiteSpace(initialQuery) ? "Tout le catalogue" : "Résultats";
            if (!string.IsNullOrWhiteSpace(initialQuery)) SearchInput.Text = initialQuery;

            // Loaded se redéclenche quand on revient depuis une fiche : on ne recharge qu'une fois.
            Loaded += async (_, _) =>
            {
                if (_loaded) return;
                _loaded = true;
                await LoadAsync();
            };
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var (rows, genres) = await CatalogueService.LoadAsync(_kind);
                _all = rows;
                BuildGenreChips(genres);
                _dataReady = true;
                StatusText.Visibility = Visibility.Collapsed;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText.Text = "Impossible de charger le catalogue.\n" + ex.GetBaseException().Message;
                _loaded = false; // permet de réessayer au prochain affichage
            }
        }

        private void BuildGenreChips(List<GenreInfo> genres)
        {
            var children = genres.Where(g => g.ParentId != null)
                                 .GroupBy(g => g.ParentId!.Value)
                                 .ToDictionary(x => x.Key, x => x.Select(g => g.Id).ToList());

            HashSet<int> Collect(int root)
            {
                var set = new HashSet<int>();
                var stack = new Stack<int>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    if (!set.Add(cur)) continue; // protège d'un éventuel cycle
                    if (children.TryGetValue(cur, out var kids))
                        foreach (var k in kids) stack.Push(k);
                }
                return set;
            }

            _subtree = genres.ToDictionary(g => g.Id, g => Collect(g.Id));
            var used = _all.SelectMany(r => r.GenreIds).ToHashSet();

            GenreChips.Children.Clear();
            AddChip("Tous", null, true);
            // Un genre est proposé s'il (ou un de ses sous-genres) est utilisé par au moins un titre affiché.
            foreach (var g in genres.Where(g => _subtree[g.Id].Overlaps(used)).OrderBy(g => g.Libelle))
                AddChip(g.Libelle, g.Id, false);
        }

        private void AddChip(string text, int? genreId, bool isChecked)
        {
            var chip = new RadioButton
            {
                Content = text,
                Tag = genreId,
                GroupName = "GenreFilter",
                Style = (Style)FindResource("ChipButton"),
                IsChecked = isChecked,
            };
            chip.Checked += (s, _) =>
            {
                _genreFilter = (int?)((RadioButton)s).Tag;
                ApplyFilter();
            };
            GenreChips.Children.Add(chip);
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (!_dataReady) return;

            IEnumerable<MediaRow> res = _all;

            if (_genreFilter is int gid && _subtree.TryGetValue(gid, out var set))
                res = res.Where(r => r.GenreIds.Any(set.Contains));

            var q = CatalogueService.Normalize(SearchInput.Text);
            if (q.Length > 0)
                res = res.Where(r => r.SearchText.Contains(q));

            var list = res.ToList();
            ResultsList.ItemsSource = list;
            CountText.Text = CatalogueService.Plural(list.Count, "titre", "titres");
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Row_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is MediaRow row)
                Navigator.Show(new MediaDetailView(row.Kind, row.Id, this));
        }

        private void Back_Click(object sender, RoutedEventArgs e)
            => Navigator.Show(new CatalogueView());
    }
}
