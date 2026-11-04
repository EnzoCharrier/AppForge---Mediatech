using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;
using MediaTech_AppForge.ViewModels;

namespace MediaTech_AppForge.Services
{
    public enum MediaKind { Livre, Magazine, DVD, CD }

    public record GenreInfo(int Id, string Libelle, int? ParentId);
    public record FieldRow(string Label, string Value);
    public record PisteRow(string Numero, string Titre, string Duree);

    public static class CatalogueService
    {
        private static readonly CultureInfo CultureFrancaise = CultureInfo.GetCultureInfo("fr-FR");

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
        public static string Plural(int nombre, string singulier, string pluriel) // Vérifie et adapte si le mot est au pluriel ou au singulier
            => $"{nombre.ToString("N0", CultureFrancaise)} {(nombre <= 1 ? singulier : pluriel)}";

        public static string Normalize(string? texte) // Permet de rechercher les variantes de mot (ex: livre = LIVRE = LivRE)
        {
            if (string.IsNullOrWhiteSpace(texte)) // Vérifie si chaîne vide ou pas
            {
                return "";
            }
            string texteDecompose = texte.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD); // Supprime espaces inutiles + minuscules + sépare les accents (é = e + accent)
            StringBuilder texteSansAccents = new StringBuilder();
            foreach (char caractere in texteDecompose.Where(lettre => CharUnicodeInfo.GetUnicodeCategory(lettre) != UnicodeCategory.NonSpacingMark)) // Ignore les caractères d'accent
            {
                texteSansAccents.Append(caractere);
            }
            return texteSansAccents.ToString().Normalize(NormalizationForm.FormC);
        }

        // kind == null -> tous les types confondus
        public static async Task<(List<MediaRow> Rows, List<GenreInfo> Genres)> LoadAsync(MediaKind? kind) // Charge les différents médias depuis la bdd
        {
            await using MediaTechContext db = new MediaTechContext(); // connexion bdd
            bool tousLesTypes = kind is null; // indique le type de média recherché
            List<MediaRow> medias = new List<MediaRow>();

            // Récupère le type de média selon le besoin ; si null on récupère tout
            if (tousLesTypes || kind == MediaKind.Livre)
            {
                medias.AddRange(await ProjectAsync(db.Livres, MediaKind.Livre, tousLesTypes));
            }
            if (tousLesTypes || kind == MediaKind.Magazine)
            {
                medias.AddRange(await ProjectAsync(db.Magazines, MediaKind.Magazine, tousLesTypes));
            }
            if (tousLesTypes || kind == MediaKind.DVD)
            {
                medias.AddRange(await ProjectAsync(db.DVDs, MediaKind.DVD, tousLesTypes));
            }
            if (tousLesTypes || kind == MediaKind.CD)
            {
                medias.AddRange(await ProjectAsync(db.CDs, MediaKind.CD, tousLesTypes));
            }

            medias = medias.OrderBy(media => media.Titre, StringComparer.CurrentCultureIgnoreCase).ToList(); // Trie les médias par titre

            // Récupère les genres de la bdd
            var genres = await db.Genres.AsNoTracking()
                .Select(genre => new GenreInfo(genre.Id, genre.Libelle, genre.IdGenreParent))
                .ToListAsync();

            return (medias, genres); // retourne une liste de médias et une liste de genres
        }

        private static async Task<List<MediaRow>> ProjectAsync<TMedia>(IQueryable<TMedia> requete, MediaKind kind, bool afficherLeType) // Transforme les données de la base en objets
            where TMedia : Media
        {
            // Demande à la bdd les champs du média
            var mediasBruts = await requete.AsNoTracking().Select(media => new
            {
                media.Id,
                media.Titre,
                Auteur = media.Auteur != null ? media.Auteur.Nom : null,
                Editeur = media.Editeur.Nom,
                media.DateSortie,
                media.Stock,
                Genres = media.Genres.Select(genre => new { genre.Id, genre.Libelle }).ToList(),
            }).ToListAsync();

            // Crée un objet MediaRow pour chaque média récupéré
            return mediasBruts.Select(media =>
            {
                int? annee = media.DateSortie?.Year;
                List<string> morceauxSousTitre = new List<string>();
                if (afficherLeType)
                {
                    morceauxSousTitre.Add(SingularLabel(kind));
                }
                if (!string.IsNullOrEmpty(media.Auteur))
                {
                    morceauxSousTitre.Add(media.Auteur);
                }
                morceauxSousTitre.Add(media.Editeur);
                if (annee != null)
                {
                    morceauxSousTitre.Add(annee.Value.ToString(CultureInfo.InvariantCulture));
                }

                List<string> libellesGenres = media.Genres.Select(genre => genre.Libelle).OrderBy(libelle => libelle).ToList(); // Récupère les noms des genres et les trie

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
                    GenresText = string.Join(" · ", libellesGenres),
                    Subtitle = string.Join(" · ", morceauxSousTitre),
                    SearchText = Normalize($"{media.Titre} {media.Auteur}"), // Affine la recherche en normalisant le titre et le nom de l'auteur
                };
            }).ToList();
        }

        public static async Task<MediaDetail?> GetDetailAsync(MediaKind kind, int id) // Récupère des infos précises sur un média
        {
            await using MediaTechContext db = new MediaTechContext();

            var infosMedia = await db.Medias.AsNoTracking().Where(media => media.Id == id).Select(media => new // Cherche le média via son ID
            {
                media.Titre,
                Auteur = media.Auteur != null ? media.Auteur.Nom : null,
                Editeur = media.Editeur.Nom,
                media.DateSortie,
                media.Stock,
                Genres = media.Genres.Select(genre => genre.Libelle).ToList(),
            }).FirstOrDefaultAsync();

            if (infosMedia is null)
            {
                return null;
            }

            // Crée un objet MediaDetail qui contient certaines infos sur le média
            MediaDetail detail = new MediaDetail
            {
                Kind = kind,
                Titre = infosMedia.Titre,
                Auteur = infosMedia.Auteur,
                Stock = infosMedia.Stock,
                Genres = infosMedia.Genres.OrderBy(libelle => libelle).ToList(),
            };
            detail.Fields.Add(new FieldRow("Éditeur", infosMedia.Editeur)); // ajoute l'éditeur
            if (infosMedia.DateSortie is DateOnly dateSortie) // ajoute la date de parution
            {
                detail.Fields.Add(new FieldRow("Parution", dateSortie.ToString("d MMMM yyyy", CultureFrancaise)));
            }

            // Regarde le type du média traité et adapte les données récupérées en conséquence
            switch (kind)
            {
                case MediaKind.Livre:
                    var livre = await db.Livres.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.NombreDePages, media.Resume }).FirstOrDefaultAsync();
                    if (livre?.NombreDePages is int nbPagesLivre)
                    {
                        detail.Fields.Add(new FieldRow("Pages", nbPagesLivre.ToString(CultureFrancaise)));
                    }
                    detail.Resume = livre?.Resume;
                    break;

                case MediaKind.Magazine:
                    var magazine = await db.Magazines.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.Numero, media.NombreDePages }).FirstOrDefaultAsync();
                    if (magazine?.Numero is int numeroMagazine)
                    {
                        detail.Fields.Add(new FieldRow("Numéro", numeroMagazine.ToString(CultureFrancaise)));
                    }
                    if (magazine?.NombreDePages is int nbPagesMagazine)
                    {
                        detail.Fields.Add(new FieldRow("Pages", nbPagesMagazine.ToString(CultureFrancaise)));
                    }
                    break;

                case MediaKind.DVD:
                    var dvd = await db.DVDs.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.Duree }).FirstOrDefaultAsync();
                    if (dvd?.Duree is TimeSpan dureeDvd)
                    {
                        detail.Fields.Add(new FieldRow("Durée", $"{(int)dureeDvd.TotalHours} h {dureeDvd.Minutes:00} min"));
                    }
                    break;

                case MediaKind.CD:
                    var cd = await db.CDs.AsNoTracking().Where(media => media.Id == id)
                        .Select(media => new { media.NombreDePiste }).FirstOrDefaultAsync();
                    if (cd?.NombreDePiste is int nbPistes)
                    {
                        detail.Fields.Add(new FieldRow("Nombre de pistes", nbPistes.ToString(CultureFrancaise)));
                    }

                    var pistesBrutes = await db.Pistes.AsNoTracking().Where(piste => piste.IdMedia == id) // Récupère les pistes d'un CD
                        .OrderBy(piste => piste.Numero) // Trie les pistes
                        .Select(piste => new { piste.Numero, piste.Titre, piste.Duree }).ToListAsync();
                    detail.Pistes = pistesBrutes.Select(piste => new PisteRow(
                        piste.Numero?.ToString(CultureFrancaise) ?? "",
                        piste.Titre,
                        piste.Duree is TimeSpan dureePiste ? $"{(int)dureePiste.TotalMinutes}:{dureePiste.Seconds:00}" : "")).ToList();
                    break;
            }

            return detail;
        }
    }
}
