using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    public record PisteInput(string Titre, TimeSpan? Duree);
    public record LookupData(List<string> Editeurs, List<string> Auteurs, List<GenreInfo> Genres);

    /// <summary>Données saisies dans le formulaire média (création ou modification).</summary>
    public class MediaEditModel
    {
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public DateOnly? DateSortie { get; set; }
        public int Stock { get; set; }
        public string Editeur { get; set; } = "";
        public string? Auteur { get; set; }
        public List<int> GenreIds { get; set; } = new();

        // Spécifiques selon le type
        public int? NombreDePages { get; set; }   // Livre, Magazine
        public string? Resume { get; set; }       // Livre
        public int? Numero { get; set; }          // Magazine
        public TimeSpan? Duree { get; set; }      // DVD
        public int? NombreDePiste { get; set; }   // CD
        public List<PisteInput> Pistes { get; set; } = new(); // CD
    }

    /// <summary>Opérations d'administration. Chaque méthode vérifie que l'utilisateur courant est admin.</summary>
    public static class AdminService
    {
        private static void RequireAdmin()
        {
            if (!Session.IsAdmin)
                throw new UnauthorizedAccessException("Action réservée aux administrateurs.");
        }

        public static async Task<LookupData> GetLookupsAsync()
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            var editeurs = await db.Editeurs.AsNoTracking().OrderBy(e => e.Nom).Select(e => e.Nom).ToListAsync();
            var auteurs = await db.Auteurs.AsNoTracking().OrderBy(a => a.Nom).Select(a => a.Nom).ToListAsync();
            var genres = await db.Genres.AsNoTracking()
                .Select(g => new GenreInfo(g.Id, g.Libelle, g.IdGenreParent)).ToListAsync();

            return new LookupData(editeurs, auteurs, genres);
        }

        // ---------------------------------------------------------------
        // Lecture pour modification
        // ---------------------------------------------------------------
        /// 
        /// 
        /// 
        /// 
        /// 
        /// 
        /// 
        /// A RELIRE POUR MIEUX COMPRENDRE
        ///.
        ///.
        ///.
        ///.
        ///.
        public static async Task<MediaEditModel?> GetMediaForEditAsync(int id, MediaKind kind)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            var m = await db.Medias.AsNoTracking().Where(x => x.Id == id).Select(x => new
            {
                x.Titre,
                x.DateSortie,
                x.Stock,
                Editeur = x.Editeur.Nom,
                Auteur = x.Auteur != null ? x.Auteur.Nom : null,
                GenreIds = x.Genres.Select(g => g.Id).ToList(),
            }).FirstOrDefaultAsync();
            if (m is null) return null;

            var model = new MediaEditModel
            {
                Kind = kind,
                Titre = m.Titre,
                DateSortie = m.DateSortie,
                Stock = m.Stock,
                Editeur = m.Editeur,
                Auteur = m.Auteur,
                GenreIds = m.GenreIds,
            };

            switch (kind)
            {
                case MediaKind.Livre:
                    var l = await db.Livres.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.NombreDePages, x.Resume }).FirstOrDefaultAsync();
                    model.NombreDePages = l?.NombreDePages;
                    model.Resume = l?.Resume;
                    break;

                case MediaKind.Magazine:
                    var mg = await db.Magazines.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.NombreDePages, x.Numero }).FirstOrDefaultAsync();
                    model.NombreDePages = mg?.NombreDePages;
                    model.Numero = mg?.Numero;
                    break;

                case MediaKind.DVD:
                    var dv = await db.DVDs.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.Duree }).FirstOrDefaultAsync();
                    model.Duree = dv?.Duree;
                    break;

                case MediaKind.CD:
                    var cd = await db.CDs.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.NombreDePiste }).FirstOrDefaultAsync();
                    model.NombreDePiste = cd?.NombreDePiste;
                    var pistes = await db.Pistes.AsNoTracking().Where(p => p.IdMedia == id)
                        .OrderBy(p => p.Numero).Select(p => new { p.Titre, p.Duree }).ToListAsync();
                    model.Pistes = pistes.Select(p => new PisteInput(p.Titre, p.Duree)).ToList();
                    break;
            }
            return model;
        }

        // ---------------------------------------------------------------
        // Création / modification : Media + sous-type + genres + pistes en UNE transaction
        // ---------------------------------------------------------------
        public static async Task<int> SaveMediaAsync(int? id, MediaEditModel model, bool notify)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            bool creating = id is null;

            // Éditeur / auteur : on réutilise l'existant (même nom) ou on le crée.
            var editeurNom = model.Editeur.Trim();
            var editeur = await db.Editeurs.FirstOrDefaultAsync(e => e.Nom == editeurNom);
            if (editeur is null)
            {
                editeur = new Editeur { Nom = editeurNom };
                db.Editeurs.Add(editeur);
            }

            Auteur? auteur = null;
            var auteurNom = model.Auteur?.Trim();
            if (!string.IsNullOrEmpty(auteurNom))
            {
                auteur = await db.Auteurs.FirstOrDefaultAsync(a => a.Nom == auteurNom);
                if (auteur is null)
                {
                    auteur = new Auteur { Nom = auteurNom };
                    db.Auteurs.Add(auteur);
                }
            }

            Media entity;
            if (creating)
            {
                entity = model.Kind switch
                {
                    MediaKind.Livre => new Livre(),
                    MediaKind.Magazine => new Magazine(),
                    MediaKind.DVD => new DVD(),
                    _ => new CD(),
                };
                db.Medias.Add(entity);
            }
            else
            {
                entity = await db.Medias.Include(x => x.Genres).FirstOrDefaultAsync(x => x.Id == id)
                         ?? throw new InvalidOperationException("Ce média n'existe plus.");
            }

            entity.Titre = model.Titre.Trim();
            entity.DateSortie = model.DateSortie;
            entity.Stock = model.Stock;
            entity.Editeur = editeur;
            entity.Auteur = auteur;

            // Genres (table Media_Genre)
            var genreIds = model.GenreIds.ToList();
            var wanted = await db.Genres.Where(g => genreIds.Contains(g.Id)).ToListAsync();
            entity.Genres.Clear();
            entity.Genres.AddRange(wanted);

            // Champs propres au sous-type
            switch (entity)
            {
                case Livre l:
                    l.NombreDePages = model.NombreDePages;
                    l.Resume = string.IsNullOrWhiteSpace(model.Resume) ? null : model.Resume.Trim();
                    break;
                case Magazine mg:
                    mg.NombreDePages = model.NombreDePages;
                    mg.Numero = model.Numero;
                    break;
                case DVD d:
                    d.Duree = model.Duree;
                    break;
                case CD c:
                    c.NombreDePiste = model.NombreDePiste ?? (model.Pistes.Count > 0 ? model.Pistes.Count : null);
                    // Les pistes sont remplacées en bloc.
                    if (!creating)
                        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Piste WHERE IdMedia = {id}");
                    int numero = 1;
                    foreach (var p in model.Pistes)
                        c.Pistes.Add(new Piste { Titre = p.Titre, Duree = p.Duree, Numero = numero++ });
                    break;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // Nouveau média => notification GLOBALE (aucune ligne NotificationUtilisateur créée ici).
            if (creating && notify)
            {
                try { await NotificationService.PublishNewMediaAsync(entity.Id, entity.Titre); }
                catch (Exception ex) { Debug.WriteLine(ex); } // le média est déjà enregistré : on n'échoue pas pour ça
            }

            return entity.Id;
        }

        // ---------------------------------------------------------------
        // Suppression
        // ---------------------------------------------------------------
        public static async Task<(bool Success, string? Error)> DeleteMediaAsync(int id)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            // Emprunt.IdMedia est en ON DELETE RESTRICT : on explique plutôt que de laisser MySQL refuser.
            int loans = await db.Emprunts.CountAsync(e => e.IdMedia == id);
            if (loans > 0)
                return (false, $"Suppression impossible : {CatalogueService.Plural(loans, "emprunt", "emprunts")} " +
                               "enregistré(s) pour ce média (historique compris). Passez plutôt son stock à 0.");

            // Les tables filles, pistes, genres et notifications liées partent en cascade (FK ON DELETE CASCADE).
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Media WHERE Id = {id}");
            NotificationService.NotifyChanged();
            return (true, null);
        }
    }
}
