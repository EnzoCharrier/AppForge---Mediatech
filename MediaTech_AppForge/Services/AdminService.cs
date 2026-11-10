using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;
using MediaTech_AppForge.ViewModels;

namespace MediaTech_AppForge.Services
{
    public record PisteInput(string Titre, TimeSpan? Duree);
    public record LookupData(List<string> Editeurs, List<string> Auteurs, List<GenreInfo> Genres);
    public record SaveResult(int Id, string? Warning);

    /// <summary>Opérations d'administration. Chaque méthode vérifie que l'utilisateur courant est admin.</summary>
    public static class AdminService
    {
        private static void RequireAdmin()
        {
            if (!Session.IsAdmin)
            {
                throw new UnauthorizedAccessException("Action réservée aux administrateurs.");
            }
        }

        public static async Task<LookupData> GetLookupsAsync()
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            var nomsEditeurs = await db.Editeurs.AsNoTracking().OrderBy(editeur => editeur.Nom).Select(editeur => editeur.Nom).ToListAsync();
            var nomsAuteurs = await db.Auteurs.AsNoTracking().OrderBy(auteur => auteur.Nom).Select(auteur => auteur.Nom).ToListAsync();
            var genres = await db.Genres.AsNoTracking()
                .Select(genre => new GenreInfo(genre.Id, genre.Libelle, genre.IdGenreParent)).ToListAsync();

            return new LookupData(nomsEditeurs, nomsAuteurs, genres);
        }

        // ---------------------------------------------------------------
        // Lecture pour modification
        // ---------------------------------------------------------------
        public static async Task<MediaEditModel?> GetMediaForEditAsync(int id, MediaKind kind)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            var infosMedia = await db.Medias.AsNoTracking().Where(media => media.Id == id).Select(media => new
            {
                media.Titre,
                media.DateSortie,
                media.Stock,
                Editeur = media.Editeur.Nom,
                Auteur = media.Auteur != null ? media.Auteur.Nom : null,
                GenreIds = media.Genres.Select(genre => genre.Id).ToList(),
            }).FirstOrDefaultAsync();
            if (infosMedia is null)
            {
                return null;
            }

            var model = new MediaEditModel
            {
                Kind = kind,
                Titre = infosMedia.Titre,
                DateSortie = infosMedia.DateSortie,
                Stock = infosMedia.Stock,
                Editeur = infosMedia.Editeur,
                Auteur = infosMedia.Auteur,
                GenreIds = infosMedia.GenreIds,
            };

            switch (kind)
            {
                case MediaKind.Livre:
                    var infosLivre = await db.Livres.AsNoTracking().Where(livre => livre.Id == id)
                        .Select(livre => new { livre.NombreDePages, livre.Resume }).FirstOrDefaultAsync();
                    model.NombreDePages = infosLivre?.NombreDePages;
                    model.Resume = infosLivre?.Resume;
                    break;

                case MediaKind.Magazine:
                    var infosMagazine = await db.Magazines.AsNoTracking().Where(magazine => magazine.Id == id)
                        .Select(magazine => new { magazine.NombreDePages, magazine.Numero }).FirstOrDefaultAsync();
                    model.NombreDePages = infosMagazine?.NombreDePages;
                    model.Numero = infosMagazine?.Numero;
                    break;

                case MediaKind.DVD:
                    var infosDvd = await db.DVDs.AsNoTracking().Where(dvd => dvd.Id == id)
                        .Select(dvd => new { dvd.Duree }).FirstOrDefaultAsync();
                    model.Duree = infosDvd?.Duree;
                    break;

                case MediaKind.CD:
                    var infosCd = await db.CDs.AsNoTracking().Where(cd => cd.Id == id)
                        .Select(cd => new { cd.NombreDePiste }).FirstOrDefaultAsync();
                    model.NombreDePiste = infosCd?.NombreDePiste;
                    var pistesBrutes = await db.Pistes.AsNoTracking().Where(piste => piste.IdMedia == id)
                        .OrderBy(piste => piste.Numero).Select(piste => new { piste.Titre, piste.Duree }).ToListAsync();
                    model.Pistes = pistesBrutes.Select(piste => new PisteInput(piste.Titre, piste.Duree)).ToList();
                    break;
            }
            return model;
        }

        // ---------------------------------------------------------------
        // Création / modification : Media + sous-type + genres + pistes en UNE transaction
        // ---------------------------------------------------------------
        public static async Task<SaveResult> SaveMediaAsync(int? id, MediaEditModel model, bool notify)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();
            await using var transaction = await db.Database.BeginTransactionAsync();

            bool estCreation = id is null;

            // Éditeur / auteur : on réutilise l'existant (même nom) ou on le crée.
            string nomEditeur = model.Editeur.Trim();
            Editeur? editeur = await db.Editeurs.FirstOrDefaultAsync(editeurExistant => editeurExistant.Nom == nomEditeur);
            if (editeur is null)
            {
                db.Editeurs.Add(editeur = new Editeur { Nom = nomEditeur });
            }

            Auteur? auteur = null;
            string? nomAuteur = model.Auteur?.Trim();
            if (!string.IsNullOrEmpty(nomAuteur))
            {
                auteur = await db.Auteurs.FirstOrDefaultAsync(auteurExistant => auteurExistant.Nom == nomAuteur);
                if (auteur is null)
                {
                    db.Auteurs.Add(auteur = new Auteur { Nom = nomAuteur });
                }
            }

            Media media;
            if (estCreation)
            {
                media = model.Kind switch
                {
                    MediaKind.Livre => new Livre(),
                    MediaKind.Magazine => new Magazine(),
                    MediaKind.DVD => new DVD(),
                    _ => new CD(),
                };
                db.Medias.Add(media);
            }
            else
            {
                media = await db.Medias.Include(mediaExistant => mediaExistant.Genres).FirstOrDefaultAsync(mediaExistant => mediaExistant.Id == id)
                        ?? throw new InvalidOperationException("Ce média n'existe plus.");
            }

            media.Titre = model.Titre.Trim();
            media.DateSortie = model.DateSortie;
            media.Stock = model.Stock;
            media.Editeur = editeur;
            media.Auteur = auteur;

            // Genres (table Media_Genre)
            List<int> idsGenresVoulus = model.GenreIds.ToList();
            List<Genre> genresVoulus = await db.Genres.Where(genre => idsGenresVoulus.Contains(genre.Id)).ToListAsync();
            media.Genres.Clear();
            media.Genres.AddRange(genresVoulus);

            // Champs propres au sous-type
            switch (media)
            {
                case Livre livre:
                    livre.NombreDePages = model.NombreDePages;
                    livre.Resume = string.IsNullOrWhiteSpace(model.Resume) ? null : model.Resume.Trim();
                    break;
                case Magazine magazine:
                    magazine.NombreDePages = model.NombreDePages;
                    magazine.Numero = model.Numero;
                    break;
                case DVD dvd:
                    dvd.Duree = model.Duree;
                    break;
                case CD cd:
                    cd.NombreDePiste = model.NombreDePiste ?? (model.Pistes.Count > 0 ? model.Pistes.Count : null);
                    // Les pistes sont remplacées en bloc.
                    if (!estCreation)
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Piste WHERE IdMedia = {id}");
                    }
                    int numeroPiste = 1;
                    foreach (PisteInput piste in model.Pistes)
                    {
                        cd.Pistes.Add(new Piste { Titre = piste.Titre, Duree = piste.Duree, Numero = numeroPiste++ });
                    }
                    break;
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            // Nouveau média => notification GLOBALE (aucune ligne NotificationUtilisateur créée ici).
            string? avertissement = null;
            if (estCreation && notify)
            {
                try
                {
                    await NotificationService.PublishNewMediaAsync(media.Id, media.Titre);
                }
                catch (Exception erreur)
                {
                    // Le média est déjà enregistré : on n'échoue pas, mais on le dit à l'administrateur.
                    Debug.WriteLine(erreur);
                    avertissement = "Le média est enregistré, mais la notification n'a pas pu être publiée : " + erreur.GetBaseException().Message;
                }
            }

            return new SaveResult(media.Id, avertissement);
        }

        // ---------------------------------------------------------------
        // Suppression
        // ---------------------------------------------------------------
        public static async Task<(bool Success, string? Error)> DeleteMediaAsync(int id)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            // Emprunt.IdMedia est en ON DELETE RESTRICT : on explique plutôt que de laisser MySQL refuser.
            int nbEmprunts = await db.Emprunts.CountAsync(emprunt => emprunt.IdMedia == id);
            if (nbEmprunts > 0)
            {
                return (false, $"Suppression impossible : {CatalogueService.Plural(nbEmprunts, "emprunt", "emprunts")} " +
                               "enregistré(s) pour ce média (historique compris). Passez plutôt son stock à 0.");
            }

            // Les tables filles, pistes, genres et notifications liées partent en cascade (FK ON DELETE CASCADE).
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Media WHERE Id = {id}");
            NotificationService.NotifyChanged();
            return (true, null);
        }
    }
}
