using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;
using Microsoft.EntityFrameworkCore.Storage;
using MediaTech_AppForge.ViewModels;

namespace MediaTech_AppForge.Services
{
    public record BorrowResult(bool Success, string? Error, DateOnly? DueDate); // le résultat d'un emprunt (succès, erreur, date limite)
    public record ActiveLoan(int EmpruntId, DateOnly DueDate, bool Physique); // les données d'un emprunt en cours

    public static class EmpruntService
    {
        // Règles métier
        public const int DureeJours = 14;
        public const int MaxEmpruntsActifs = 5;
        public static bool NumeriqueDisponible(MediaKind kind) => true; // indique si un média est dispo en numérique (à changer, ne sert à rien en l'état)

        private static readonly CultureInfo CultureFrancaise = CultureInfo.GetCultureInfo("fr-FR"); // les dates sont affichées au format français
        public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);
        private static string Fmt(DateOnly date, string format = "d MMMM yyyy") => date.ToString(format, CultureFrancaise); // formate une date en français (6/10/2026 => 6 octobre 2026)

        private static BorrowResult Fail(string erreur) => new(false, erreur, null);

        // EMPRUNTER : contrôles + décrément du stock + création, en une transaction
        public static async Task<BorrowResult> BorrowAsync(int userId, int mediaId, bool physique)
        {
            await using MediaTechContext db = new MediaTechContext(); // connexion bdd
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(); // ouvre la transaction

            Utilisateur? utilisateur = await db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId); // cherche l'utilisateur dans la bdd
            if (utilisateur is null || utilisateur.Etat != EtatUtilisateur.Actif) // l'utilisateur existe et son compte est actif ?
            {
                return Fail("Votre compte ne permet pas d'emprunter actuellement.");
            }

            if (!await db.Medias.AnyAsync(media => media.Id == mediaId)) // le média existe ?
            {
                return Fail("Ce média n'existe plus.");
            }

            List<int> idsMediasDejaEmpruntes = await db.Emprunts
                .Where(emprunt => emprunt.IdUtilisateur == userId && emprunt.DateRetour == null)
                .Select(emprunt => emprunt.IdMedia).ToListAsync();

            if (idsMediasDejaEmpruntes.Contains(mediaId)) // média déjà emprunté ?
            {
                return Fail("Vous avez déjà emprunté ce média.");
            }
            if (idsMediasDejaEmpruntes.Count >= MaxEmpruntsActifs) // limite d'emprunts ?
            {
                return Fail($"Vous avez atteint la limite de {MaxEmpruntsActifs} emprunts en cours.");
            }

            // Exemplaire physique demandé : on diminue le stock de 1 s'il est supérieur à 0
            int nbLignesModifiees = physique
                ? await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Media SET Stock = Stock - 1 WHERE Id = {mediaId} AND Stock > 0")
                : 1;
            if (nbLignesModifiees == 0) // stock épuisé
            {
                return Fail("Plus aucun exemplaire physique n'est disponible.");
            }

            DateOnly aujourdHui = Today();
            DateOnly dateRetourPrevue = aujourdHui.AddDays(DureeJours); // date de retour

            // Crée l'emprunt
            db.Emprunts.Add(new Emprunt
            {
                DateEmprunt = aujourdHui,
                DateRetourPrevue = dateRetourPrevue,
                EstPhysique = physique,
                IdUtilisateur = userId,
                IdMedia = mediaId,
            });

            // Enregistre et valide la transaction
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new BorrowResult(true, null, dateRetourPrevue);
        }

        // RENDRE : date de retour + réintégration du stock si physique

        public static async Task<(bool Success, string? Error)> ReturnAsync(int userId, int empruntId) // Permet de rendre un emprunt
        {
            await using MediaTechContext db = new MediaTechContext();
            await using var transaction = await db.Database.BeginTransactionAsync();

            Emprunt? emprunt = await db.Emprunts.FirstOrDefaultAsync(e => e.Id == empruntId && e.IdUtilisateur == userId); // recherche l'emprunt
            if (emprunt is null) // l'emprunt n'existe pas
            {
                return (false, "Emprunt introuvable.");
            }
            if (emprunt.DateRetour != null) // déjà rendu
            {
                return (false, "Cet emprunt a déjà été rendu.");
            }

            emprunt.DateRetour = Today(); // enregistre la date de retour

            // Les rappels liés à cet emprunt (échéance, retard) sont supprimés.
            List<Notification> rappels = await db.Notifications.Where(notification => notification.IdEmprunt == empruntId).ToListAsync();
            db.Notifications.RemoveRange(rappels);

            await db.SaveChangesAsync();

            // Média physique : on remet l'exemplaire en stock
            if (emprunt.EstPhysique)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Media SET Stock = Stock + 1 WHERE Id = {emprunt.IdMedia}");
            }

            await transaction.CommitAsync(); // valide la transaction
            NotificationService.NotifyChanged(); // prévient l'application qu'il y a eu une modification
            return (true, null);
        }

        // LECTURES

        public static async Task<ActiveLoan?> GetActiveLoanAsync(int userId, int mediaId)
        {
            await using MediaTechContext db = new MediaTechContext();
            var empruntEnCours = await db.Emprunts.AsNoTracking()
                .Where(emprunt => emprunt.IdUtilisateur == userId && emprunt.IdMedia == mediaId && emprunt.DateRetour == null)
                .Select(emprunt => new { emprunt.Id, emprunt.DateRetourPrevue, emprunt.EstPhysique })
                .FirstOrDefaultAsync();
            return empruntEnCours is null ? null : new ActiveLoan(empruntEnCours.Id, empruntEnCours.DateRetourPrevue, empruntEnCours.EstPhysique);
        }

        public static async Task<List<EmpruntRow>> GetUserLoansAsync(int userId) // Récupère les emprunts de l'utilisateur
        {
            await using var db = new MediaTechContext();

            // Récupère les emprunts et les trie
            var empruntsBruts = await db.Emprunts.AsNoTracking()
                .Where(emprunt => emprunt.IdUtilisateur == userId)
                .OrderByDescending(emprunt => emprunt.DateEmprunt).ThenByDescending(emprunt => emprunt.Id)
                .Select(emprunt => new
                {
                    emprunt.Id, emprunt.IdMedia,
                    Titre = emprunt.Media.Titre,
                    emprunt.EstPhysique, emprunt.DateEmprunt, emprunt.DateRetourPrevue, emprunt.DateRetour,
                }).ToListAsync();

            Dictionary<int, MediaKind> typesParIdMedia = await KindsOfAsync(db, empruntsBruts.Select(emprunt => emprunt.IdMedia).Distinct().ToList()); // Recherche le type de chaque média
            DateOnly aujourdHui = Today();

            return empruntsBruts.Select(emprunt =>
            {
                bool estActif = emprunt.DateRetour == null;
                int joursRestants = emprunt.DateRetourPrevue.DayNumber - aujourdHui.DayNumber; // nombre de jours restants (négatif si en retard)
                bool enRetard = estActif && joursRestants < 0; // détecte un retard

                string texteEcheance = (estActif, enRetard, joursRestants) switch
                {
                    (false, _, _) => $"Rendu le {Fmt(emprunt.DateRetour!.Value)}", // le média a été rendu
                    (_, true, _) => $"En retard de {-joursRestants} jour{(joursRestants < -1 ? "s" : "")} · à rendre le {Fmt(emprunt.DateRetourPrevue)}",
                    (_, _, 0) => "À rendre aujourd'hui",
                    (_, _, 1) => "À rendre demain",
                    _ => $"À rendre le {Fmt(emprunt.DateRetourPrevue)} (dans {joursRestants} jours)",
                };

                MediaKind kind = typesParIdMedia.TryGetValue(emprunt.IdMedia, out var typeTrouve) ? typeTrouve : MediaKind.Livre; // type du média (Livre par défaut)
                string typeExemplaire = emprunt.EstPhysique ? "Exemplaire physique" : "Numérique";

                return new EmpruntRow // crée l'objet emprunt
                {
                    EmpruntId = emprunt.Id,
                    MediaId = emprunt.IdMedia,
                    Kind = kind,
                    Titre = emprunt.Titre,
                    Subtitle = $"{CatalogueService.SingularLabel(kind)} · {typeExemplaire} · emprunté le {Fmt(emprunt.DateEmprunt, "d MMM yyyy")}", // ligne récap (ex : Livre · Exemplaire physique · emprunté le 6 oct. 2026)
                    DueText = texteEcheance,
                    IsLate = enRetard,
                    IsActive = estActif,
                    DateRetourPrevue = emprunt.DateRetourPrevue,
                    DateRetour = emprunt.DateRetour,
                };
            }).ToList();
        }

        // Le schéma n'a pas de colonne "type" : on déduit le sous-type par la table.
        public static async Task<Dictionary<int, MediaKind>> KindsOfAsync(MediaTechContext db, List<int> ids)
        {
            Dictionary<int, MediaKind> typesParIdMedia = new Dictionary<int, MediaKind>();
            if (ids.Count == 0)
            {
                return typesParIdMedia;
            }

            foreach (int idMedia in await db.Livres.Where(livre => ids.Contains(livre.Id)).Select(livre => livre.Id).ToListAsync())
            {
                typesParIdMedia[idMedia] = MediaKind.Livre;
            }
            foreach (int idMedia in await db.Magazines.Where(magazine => ids.Contains(magazine.Id)).Select(magazine => magazine.Id).ToListAsync())
            {
                typesParIdMedia[idMedia] = MediaKind.Magazine;
            }
            foreach (int idMedia in await db.DVDs.Where(dvd => ids.Contains(dvd.Id)).Select(dvd => dvd.Id).ToListAsync())
            {
                typesParIdMedia[idMedia] = MediaKind.DVD;
            }
            foreach (int idMedia in await db.CDs.Where(cd => ids.Contains(cd.Id)).Select(cd => cd.Id).ToListAsync())
            {
                typesParIdMedia[idMedia] = MediaKind.CD;
            }
            return typesParIdMedia;
        }
    }
}
