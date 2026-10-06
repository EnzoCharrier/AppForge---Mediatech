using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    public enum MediaKind { Livre, Magazine, DVD, CD }

    public record GenreInfo(int Id, string Libelle, int? ParentId);
    public record FieldRow(string Label, string Value);
    public record PisteRow(string Numero, string Titre, string Duree);

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

    public class MediaDetail
    {
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public string? Auteur { get; set; }
        public int Stock { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<FieldRow> Fields { get; set; } = new();
        public string? Resume { get; set; }
        public List<PisteRow> Pistes { get; set; } = new();
    }

    public static class CatalogueService
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        public static string Label(MediaKind k) => k switch
        {
            MediaKind.Livre => "Livres",
            MediaKind.Magazine => "Magazines",
            MediaKind.DVD => "DVD",
            _ => "CD",
        };

        public static string SingularLabel(MediaKind k) => k switch
        {
            MediaKind.Livre => "Livre",
            MediaKind.Magazine => "Magazine",
            MediaKind.DVD => "DVD",
            _ => "CD",
        };

        // En français, 0 et 1 sont au singulier.
        /// 
        /// 
        /// 
        /// 
        /// 
        /// 
        /// 
        /// A REFAIRE
        ///.
        ///.
        ///.
        ///.
        ///.
        public static string Plural(int n, string singular, string plural) // Verifier et adapte si le mot est au pluriel ou au singulier 
            => $"{n.ToString("N0", Fr)} {(n <= 1 ? singular : plural)}";

        public static string Normalize(string? s) // Permet de rechercher les variantes de mot (ex: livre = LIVRE = LivRE)
        {
            if (string.IsNullOrWhiteSpace(s)) return ""; // Verifie si chaine vide ou pas 
            var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD); // Supprime espace inutile + met tout en minuscules + separe accent en caractères spéciaux(é = e + accent)
            var sb = new StringBuilder();
            foreach (var c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c); // Vérifie si le caractère est un accent
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // kind == null -> tous les types confondus
        public static async Task<(List<MediaRow> Rows, List<GenreInfo> Genres)> LoadAsync(MediaKind? kind) // Charge les différents médias depuis la bdd
        {
            await using var db = new MediaTechContext(); // connexion bdd
            bool all = kind is null; // indique le type de média recherché 
            var rows = new List<MediaRow>(); 

            // Recupere le type de Media selon le besoin si null on recupere tout 
            if (all || kind == MediaKind.Livre)    rows.AddRange(await ProjectAsync(db.Livres, MediaKind.Livre, all));
            if (all || kind == MediaKind.Magazine) rows.AddRange(await ProjectAsync(db.Magazines, MediaKind.Magazine, all));
            if (all || kind == MediaKind.DVD)      rows.AddRange(await ProjectAsync(db.DVDs, MediaKind.DVD, all));
            if (all || kind == MediaKind.CD)       rows.AddRange(await ProjectAsync(db.CDs, MediaKind.CD, all));

            rows = rows.OrderBy(r => r.Titre, StringComparer.CurrentCultureIgnoreCase).ToList(); // Trie les média par titre 

            // Recupère les genres de la bdd
            var genres = await db.Genres.AsNoTracking()
                .Select(g => new GenreInfo(g.Id, g.Libelle, g.IdGenreParent))
                .ToListAsync();

            return (rows, genres); // retourne une liste de media(rows) et une liste de genre(genres)
        }

        private static async Task<List<MediaRow>> ProjectAsync<T>(IQueryable<T> query, MediaKind kind, bool includeKind) // Tranforme les données de la base en objets 
            where T : Media
        {
            // Demande a la bdd des champ de media
            var raw = await query.AsNoTracking().Select(m => new
            {
                m.Id,
                m.Titre,
                Auteur = m.Auteur != null ? m.Auteur.Nom : null,
                Editeur = m.Editeur.Nom,
                m.DateSortie,
                m.Stock,
                Genres = m.Genres.Select(g => new { g.Id, g.Libelle }).ToList(),
            }).ToListAsync();

            // Crée un objet MediaRow pour chaque media recupéré 
            return raw.Select(x =>
            {
                int? annee = x.DateSortie?.Year;
                var parts = new List<string>();
                if (includeKind) parts.Add(SingularLabel(kind));
                if (!string.IsNullOrEmpty(x.Auteur)) parts.Add(x.Auteur);
                parts.Add(x.Editeur);
                if (annee != null) parts.Add(annee.Value.ToString(CultureInfo.InvariantCulture));

                var genresLabels = x.Genres.Select(g => g.Libelle).OrderBy(s => s).ToList(); // Recupère nom des genres et les tri

                return new MediaRow
                {
                    Id = x.Id,
                    Kind = kind,
                    Titre = x.Titre,
                    Auteur = x.Auteur ?? "",
                    Editeur = x.Editeur,
                    Annee = annee,
                    Stock = x.Stock,
                    GenreIds = x.Genres.Select(g => g.Id).ToList(),
                    GenresText = string.Join(" · ", genresLabels),
                    Subtitle = string.Join(" · ", parts),
                    SearchText = Normalize($"{x.Titre} {x.Auteur}"), // Permet d'affiner la recherche en utilisant normalize sur le titre et le nom de l'auteur
                };
            }).ToList();
        }

        public static async Task<MediaDetail?> GetDetailAsync(MediaKind kind, int id) // Recuperes certaines info precise sur un média 
        {
            await using var db = new MediaTechContext();

            var m = await db.Medias.AsNoTracking().Where(x => x.Id == id).Select(x => new // Cherche le media cherché via l'ID
            {
                x.Titre,
                Auteur = x.Auteur != null ? x.Auteur.Nom : null,
                Editeur = x.Editeur.Nom,
                x.DateSortie,
                x.Stock,
                Genres = x.Genres.Select(g => g.Libelle).ToList(),
            }).FirstOrDefaultAsync();

            if (m is null) return null;

            // Crée un objet MediaDetail qui contient certaines info sur le media
            var d = new MediaDetail
            {
                Kind = kind,
                Titre = m.Titre,
                Auteur = m.Auteur,
                Stock = m.Stock,
                Genres = m.Genres.OrderBy(s => s).ToList(),
            };
            d.Fields.Add(new FieldRow("Éditeur", m.Editeur)); // ajoute un editeur
            if (m.DateSortie is DateOnly ds) d.Fields.Add(new FieldRow("Parution", ds.ToString("d MMMM yyyy", Fr))); // ajoute la date de parution

            // regarde quelle est le type du media qu'il est en train de traiter et adapte les données qu'il recupere en consequences
            switch (kind)
            {
                case MediaKind.Livre:
                    var l = await db.Livres.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.NombreDePages, x.Resume }).FirstOrDefaultAsync();
                    if (l?.NombreDePages is int lp) d.Fields.Add(new FieldRow("Pages", lp.ToString(Fr)));
                    d.Resume = l?.Resume;
                    break;

                case MediaKind.Magazine:
                    var mg = await db.Magazines.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.Numero, x.NombreDePages }).FirstOrDefaultAsync();
                    if (mg?.Numero is int num) d.Fields.Add(new FieldRow("Numéro", num.ToString(Fr)));
                    if (mg?.NombreDePages is int mp) d.Fields.Add(new FieldRow("Pages", mp.ToString(Fr)));
                    break;

                case MediaKind.DVD:
                    var dv = await db.DVDs.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.Duree }).FirstOrDefaultAsync();
                    if (dv?.Duree is TimeSpan t)
                        d.Fields.Add(new FieldRow("Durée", $"{(int)t.TotalHours} h {t.Minutes:00} min"));
                    break;

                case MediaKind.CD:
                    var cd = await db.CDs.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.NombreDePiste }).FirstOrDefaultAsync();
                    if (cd?.NombreDePiste is int np) d.Fields.Add(new FieldRow("Nombre de pistes", np.ToString(Fr)));

                    var pistes = await db.Pistes.AsNoTracking().Where(p => p.IdMedia == id) // Recuperes les pistes d'un CD 
                        .OrderBy(p => p.Numero) // Trie les pistes 
                        .Select(p => new { p.Numero, p.Titre, p.Duree }).ToListAsync();
                    d.Pistes = pistes.Select(p => new PisteRow(
                        p.Numero?.ToString(Fr) ?? "",
                        p.Titre,
                        p.Duree is TimeSpan pd ? $"{(int)pd.TotalMinutes}:{pd.Seconds:00}" : "")).ToList();
                    break;
            }

            return d;
        }
    }
}
