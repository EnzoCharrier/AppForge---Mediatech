using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    public record BorrowResult(bool Success, string? Error, DateOnly? DueDate);
    public record ActiveLoan(int EmpruntId, DateOnly DueDate, bool Physique);

    /// <summary>Une ligne de la page "Mes emprunts".</summary>
    public class EmpruntRow
    {
        public int EmpruntId { get; set; }
        public int MediaId { get; set; }
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string DueText { get; set; } = "";
        public bool IsLate { get; set; }
        public bool IsActive { get; set; }
        public DateOnly DateRetourPrevue { get; set; }
        public DateOnly? DateRetour { get; set; }
    }

    public static class EmpruntService
    {
        // ===== Règles métier (à ajuster ici) =====
        public const int DureeJours = 14;
        public const int MaxEmpruntsActifs = 5;
        /// <summary>Un média est-il empruntable en numérique ? Ici : tous.</summary>
        public static bool NumeriqueDisponible(MediaKind kind) => true;

        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
        public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);
        private static string Fmt(DateOnly d, string f = "d MMMM yyyy") => d.ToString(f, Fr);

        private static BorrowResult Fail(string error) => new(false, error, null);

        // ---------------------------------------------------------------
        // EMPRUNTER : contrôles + décrément du stock + création, en une transaction
        // ---------------------------------------------------------------
        public static async Task<BorrowResult> BorrowAsync(int userId, int mediaId, bool physique)
        {
            await using var db = new MediaTechContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            // On relit le compte en base : son état a pu changer depuis la connexion.
            var user = await db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null || user.Etat != EtatUtilisateur.Actif)
                return Fail("Votre compte ne permet pas d'emprunter actuellement.");

            if (!await db.Medias.AnyAsync(m => m.Id == mediaId))
                return Fail("Ce média n'existe plus.");

            var actifs = await db.Emprunts
                .Where(e => e.IdUtilisateur == userId && e.DateRetour == null)
                .Select(e => e.IdMedia).ToListAsync();

            if (actifs.Contains(mediaId))
                return Fail("Vous avez déjà emprunté ce média.");
            if (actifs.Count >= MaxEmpruntsActifs)
                return Fail($"Vous avez atteint la limite de {MaxEmpruntsActifs} emprunts en cours.");

            if (physique)
            {
                // Décrément atomique : la condition Stock > 0 évite que deux personnes
                // prennent le dernier exemplaire en même temps.
                int n = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Media SET Stock = Stock - 1 WHERE Id = {mediaId} AND Stock > 0");
                if (n == 0) return Fail("Plus aucun exemplaire physique n'est disponible.");
            }

            var today = Today();
            var due = today.AddDays(DureeJours);
            db.Emprunts.Add(new Emprunt
            {
                DateEmprunt = today,
                DateRetourPrevue = due,
                EstPhysique = physique,
                IdUtilisateur = userId,
                IdMedia = mediaId,
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return new BorrowResult(true, null, due);
        }

        // ---------------------------------------------------------------
        // RENDRE : date de retour + réintégration du stock si physique
        // ---------------------------------------------------------------
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
        public static async Task<(bool Success, string? Error)> ReturnAsync(int userId, int empruntId)
        {
            await using var db = new MediaTechContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            var e = await db.Emprunts.FirstOrDefaultAsync(x => x.Id == empruntId && x.IdUtilisateur == userId);
            if (e is null) return (false, "Emprunt introuvable.");
            if (e.DateRetour != null) return (false, "Cet emprunt a déjà été rendu.");

            e.DateRetour = Today();

            // Les rappels liés à cet emprunt (échéance, retard) n'ont plus lieu d'être.
            var rappels = await db.Notifications.Where(n => n.IdEmprunt == empruntId).ToListAsync();
            db.Notifications.RemoveRange(rappels);

            await db.SaveChangesAsync();

            if (e.EstPhysique)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Media SET Stock = Stock + 1 WHERE Id = {e.IdMedia}");

            await tx.CommitAsync();
            NotificationService.NotifyChanged();
            return (true, null);
        }

        // ---------------------------------------------------------------
        // LECTURES
        // ---------------------------------------------------------------
        public static async Task<ActiveLoan?> GetActiveLoanAsync(int userId, int mediaId)
        {
            await using var db = new MediaTechContext();
            var e = await db.Emprunts.AsNoTracking()
                .Where(x => x.IdUtilisateur == userId && x.IdMedia == mediaId && x.DateRetour == null)
                .Select(x => new { x.Id, x.DateRetourPrevue, x.EstPhysique })
                .FirstOrDefaultAsync();
            return e is null ? null : new ActiveLoan(e.Id, e.DateRetourPrevue, e.EstPhysique);
        }

        public static async Task<List<EmpruntRow>> GetUserLoansAsync(int userId)
        {
            await using var db = new MediaTechContext();

            var raw = await db.Emprunts.AsNoTracking()
                .Where(e => e.IdUtilisateur == userId)
                .OrderByDescending(e => e.DateEmprunt).ThenByDescending(e => e.Id)
                .Select(e => new
                {
                    e.Id, e.IdMedia,
                    Titre = e.Media.Titre,
                    e.EstPhysique, e.DateEmprunt, e.DateRetourPrevue, e.DateRetour,
                }).ToListAsync();

            var kinds = await KindsOfAsync(db, raw.Select(r => r.IdMedia).Distinct().ToList());
            var today = Today();

            return raw.Select(e =>
            {
                bool active = e.DateRetour == null;
                int days = e.DateRetourPrevue.DayNumber - today.DayNumber;
                bool late = active && days < 0;

                string due;
                if (!active) due = $"Rendu le {Fmt(e.DateRetour!.Value)}";
                else if (late) due = $"En retard de {-days} jour{(days < -1 ? "s" : "")} · à rendre le {Fmt(e.DateRetourPrevue)}";
                else if (days == 0) due = "À rendre aujourd'hui";
                else if (days == 1) due = "À rendre demain";
                else due = $"À rendre le {Fmt(e.DateRetourPrevue)} (dans {days} jours)";

                var kind = kinds.TryGetValue(e.IdMedia, out var k) ? k : MediaKind.Livre;

                return new EmpruntRow
                {
                    EmpruntId = e.Id,
                    MediaId = e.IdMedia,
                    Kind = kind,
                    Titre = e.Titre,
                    Subtitle = $"{CatalogueService.SingularLabel(kind)} · {(e.EstPhysique ? "Exemplaire physique" : "Numérique")} · emprunté le {Fmt(e.DateEmprunt, "d MMM yyyy")}",
                    DueText = due,
                    IsLate = late,
                    IsActive = active,
                    DateRetourPrevue = e.DateRetourPrevue,
                    DateRetour = e.DateRetour,
                };
            }).ToList();
        }

        // Le schéma n'a pas de colonne "type" : on déduit le sous-type par la table fille.
        public static async Task<Dictionary<int, MediaKind>> KindsOfAsync(MediaTechContext db, List<int> ids)
        {
            var map = new Dictionary<int, MediaKind>();
            if (ids.Count == 0) return map;

            foreach (var id in await db.Livres.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync()) map[id] = MediaKind.Livre;
            foreach (var id in await db.Magazines.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync()) map[id] = MediaKind.Magazine;
            foreach (var id in await db.DVDs.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync()) map[id] = MediaKind.DVD;
            foreach (var id in await db.CDs.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync()) map[id] = MediaKind.CD;
            return map;
        }
    }
}
