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

        public static string Label(MediaKind kind) => kind switch
        {
            MediaKind.Livre => "Livres",
            MediaKind.Magazine => "Magazines",
            MediaKind.DVD => "DVD",
            _ => "CD",
        };

        public static string SingularLabel(MediaKind kind) => kind switch
        {
            MediaKind.Livre => "Livre",
            MediaKind.Magazine => "Magazine",
            MediaKind.DVD => "DVD",
            _ => "CD",
        };

        // En français, 0 et 1 sont au singulier.
        public static string Plural(int n, string singular, string plural) // Verifier et adapte si le mot est au pluriel ou au singulier 
            => $"{n.ToString("N0", Fr)} {(n <= 1 ? singular : plural)}";

        public static string Normalize(string? txt) // Permet de rechercher les variantes de mot (ex: livre = LIVRE = LivRE)
        {
            if (string.IsNullOrWhiteSpace(txt)) return ""; // Verifie si chaine vide ou pas 
            string txtNormalized = txt.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD); // Supprime espace inutile + met tout en minuscules + separe accent en caractères spéciaux(é = e + accent)
            StringBuilder builder = new StringBuilder();
            foreach (char c in txtNormalized)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c); // Vérifie si le caractère est un accent
            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        // kind == null -> tous les types confondus
        public static async Task<(List<MediaRow> Rows, List<GenreInfo> Genres)> LoadAsync(MediaKind? kind) // Charge les différents médias depuis la bdd
        {
            await using MediaTechContext db = new MediaTechContext(); // connexion bdd
            bool all = kind is null; // indique le type de média recherché 
            List<MediaRow> rows = new List<MediaRow>(); 

            // Recupere le type de Media selon le besoin si null on recupere tout 
            if (all || kind == MediaKind.Livre)    rows.AddRange(await ProjectAsync(db.Livres, MediaKind.Livre, all));
            if (all || kind == MediaKind.Magazine) rows.AddRange(await ProjectAsync(db.Magazines, MediaKind.Magazine, all));
            if (all || kind == MediaKind.DVD)      rows.AddRange(await ProjectAsync(db.DVDs, MediaKind.DVD, all));
            if (all || kind == MediaKind.CD)       rows.AddRange(await ProjectAsync(db.CDs, MediaKind.CD, all));

            rows = rows.OrderBy(r => r.Titre, StringComparer.CurrentCultureIgnoreCase).ToList(); // Trie les média par titre 

            // Recupère les genres de la bdd
            var genres = await db.Genres.AsNoTracking()
                .Select(genre => new GenreInfo(genre.Id, genre.Libelle, genre.IdGenreParent))
                .ToListAsync();

            return (rows, genres); // retourne une liste de media(rows) et une liste de genre(genres)
        }

        private static async Task<List<MediaRow>> ProjectAsync<Task>(IQueryable<Task> query, MediaKind kind, bool includeKind) // Tranforme les données de la base en objets 
            where Task : Media
        {
            // Demande a la bdd des champ de media
            var raw = await query.AsNoTracking().Select(media => new
            {
                media.Id,
                media.Titre,
                Auteur = media.Auteur != null ? media.Auteur.Nom : null,
                Editeur = media.Editeur.Nom,
                media.DateSortie,
                media.Stock,
                Genres = media.Genres.Select(genre => new { genre.Id, genre.Libelle }).ToList(),
            }).ToListAsync();

            // Crée un objet MediaRow pour chaque media recupéré 
            return raw.Select(media =>
            {
                int? annee = media.DateSortie?.Year;
                List<string> parts = new List<string>();
                if (includeKind) parts.Add(SingularLabel(kind));
                if (!string.IsNullOrEmpty(media.Auteur)) parts.Add(media.Auteur);
                parts.Add(media.Editeur);
                if (annee != null) parts.Add(annee.Value.ToString(CultureInfo.InvariantCulture));

                List<string> genresLabels = media.Genres.Select(genre => genre.Libelle).OrderBy(s => s).ToList(); // Recupère nom des genres et les tri

                return new MediaRow
                {
                    Id = media.Id,
                    Kind = kind,
                    Titre = media.Titre,
                    Auteur = media.Auteur ?? "",
                    Editeur = media.Editeur,
                    Annee = annee,
                    Stock = media.Stock,
                    GenreIds = media.Genres.Select(genre => genre.Id).ToList(),
                    GenresText = string.Join(" · ", genresLabels),
                    Subtitle = string.Join(" · ", parts),
                    SearchText = Normalize($"{media.Titre} {media.Auteur}"), // Permet d'affiner la recherche en utilisant normalize sur le titre et le nom de l'auteur
                };
            }).ToList();
        }

        public static async Task<MediaDetail?> GetDetailAsync(MediaKind kind, int id) // Recuperes certaines info precise sur un média 
        {
            await using MediaTechContext db = new MediaTechContext();

            var media = await db.Medias.AsNoTracking().Where(media => media.Id == id).Select(media => new // Cherche le media cherché via l'ID
            {
                media.Titre,
                Auteur = media.Auteur != null ? media.Auteur.Nom : null,
                Editeur = media.Editeur.Nom,
                media.DateSortie,
                media.Stock,
                Genres = media.Genres.Select(g => g.Libelle).ToList(),
            }).FirstOrDefaultAsync();

            if (media is null) return null;

            // Crée un objet MediaDetail qui contient certaines info sur le media
            MediaDetail detail = new MediaDetail
            {
                Kind = kind,
                Titre = media.Titre,
                Auteur = media.Auteur,
                Stock = media.Stock,
                Genres = media.Genres.OrderBy(s => s).ToList(),
            };
            detail.Fields.Add(new FieldRow("Éditeur", media.Editeur)); // ajoute un editeur
            if (media.DateSortie is DateOnly sortie) detail.Fields.Add(new FieldRow("Parution", sortie.ToString("d MMMM yyyy", Fr))); // ajoute la date de parution

            // regarde quelle est le type du media qu'il est en train de traiter et adapte les données qu'il recupere en consequences
            switch (kind)
            {
                case MediaKind.Livre:
                    var livre = await db.Livres.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.NombreDePages, media.Resume }).FirstOrDefaultAsync();
                    if (livre?.NombreDePages is int lPages) detail.Fields.Add(new FieldRow("Pages", lPages.ToString(Fr)));
                    detail.Resume = livre?.Resume;
                    break;

                case MediaKind.Magazine:
                    var magazine = await db.Magazines.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.Numero, media.NombreDePages }).FirstOrDefaultAsync();
                    if (magazine?.Numero is int num) detail.Fields.Add(new FieldRow("Numéro", num.ToString(Fr)));
                    if (magazine?.NombreDePages is int mPages) detail.Fields.Add(new FieldRow("Pages", mPages.ToString(Fr)));
                    break;

                case MediaKind.DVD:
                    var dvd = await db.DVDs.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.Duree }).FirstOrDefaultAsync();
                    if (dvd?.Duree is TimeSpan t)
                        detail.Fields.Add(new FieldRow("Durée", $"{(int)t.TotalHours} h {t.Minutes:00} min"));
                    break;

                case MediaKind.CD:
                    var cd = await db.CDs.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.NombreDePiste }).FirstOrDefaultAsync();
                    if (cd?.NombreDePiste is int nPistes) detail.Fields.Add(new FieldRow("Nombre de pistes", nPistes.ToString(Fr)));

                    var pistes = await db.Pistes.AsNoTracking().Where(piste => piste.IdMedia == id) // Recuperes les pistes d'un CD 
                        .OrderBy(piste => piste.Numero) // Trie les pistes 
                        .Select(piste => new { piste.Numero, piste.Titre, piste.Duree }).ToListAsync();
                    detail.Pistes = pistes.Select(piste => new PisteRow(
                        piste.Numero?.ToString(Fr) ?? "",
                        piste.Titre,
                        piste.Duree is TimeSpan duree ? $"{(int)duree.TotalMinutes}:{duree.Seconds:00}" : "")).ToList();
                    break;
            }

            return detail;
        }
    }
}
