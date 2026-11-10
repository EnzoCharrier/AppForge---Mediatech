using System.Collections.Generic;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
    /// <summary>Une ligne de la liste du catalogue.</summary>
    public class MediaRow
    {
        public int Id { get; set; }
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public string Auteur { get; set; } = "";
        public string Editeur { get; set; } = "";
        public int? Annee { get; set; }
        public int Stock { get; set; }
        public List<int> GenreIds { get; set; } = new();
        public string GenresText { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string SearchText { get; set; } = "";

        public bool HasStock => Stock > 0;
        public string StockText => Stock > 0 ? $"{Stock} en stock" : "Épuisé";
    }
}
