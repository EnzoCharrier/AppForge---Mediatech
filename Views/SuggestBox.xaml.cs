using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.Views
{
    /// <summary>
    /// Champ texte qui propose, sous forme de pastilles cliquables, les valeurs existantes proches de la saisie.
    /// Une valeur inconnue reste acceptée (elle sera créée à l'enregistrement).
    /// </summary>
    public partial class SuggestBox : UserControl
    {
        private List<string> _items = new();

        public SuggestBox() => InitializeComponent();

        public string Text
        {
            get => Input.Text;
            set => Input.Text = value ?? "";
        }

        public int MaxLength
        {
            get => Input.MaxLength;
            set => Input.MaxLength = value;
        }

        public void SetItems(IEnumerable<string> items)
        {
            _items = items.Distinct(StringComparer.CurrentCultureIgnoreCase)
                          .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
            Refresh();
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

        private void Refresh()
        {
            Chips.Children.Clear();

            var q = CatalogueService.Normalize(Input.Text);
            if (q.Length > 0)
            {
                var matches = _items
                    .Where(i => { var n = CatalogueService.Normalize(i); return n.Contains(q) && n != q; })
                    .Take(6);

                foreach (var name in matches)
                {
                    var chip = new Button
                    {
                        Content = name,
                        Style = (Style)FindResource("GhostButton"),
                        Margin = new Thickness(0, 0, 6, 6),
                    };
                    var value = name;
                    chip.Click += (_, _) =>
                    {
                        Input.Text = value;
                        Input.CaretIndex = value.Length;
                        Input.Focus();
                    };
                    Chips.Children.Add(chip);
                }
            }

            Chips.Visibility = Chips.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
