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
    public record BorrowResult(bool Success, string? Error, DateOnly? DueDate); // le resultat d'un emprunt(succès, erreur,date limite)
    public record ActiveLoan(int EmpruntId, DateOnly DueDate, bool Physique); // les donnée d'un emprunt en cours 

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
        // Règles métier 
        public const int DureeJours = 14;
        public const int MaxEmpruntsActifs = 5;
        public static bool NumeriqueDisponible(MediaKind kind) => true; // indique si un média est dispo en numérique (a changer sert a rien dans l'etat)

        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR"); // indique que les dates doivent être affichés dans le format francais
        public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);
        private static string Fmt(DateOnly d, string f = "d MMMM yyyy") => d.ToString(f, Fr); // formate date en francais(6/10/2026 => 6 Octobre 2026)

        private static BorrowResult Fail(string error) => new(false, error, null);

        
        // EMPRUNTER : contrôles + décrément du stock + création, en une transaction
        public static async Task<BorrowResult> BorrowAsync(int userId, int mediaId, bool physique)
        {
            await using var db = new MediaTechContext(); // connexion bdd
            await using var tx = await db.Database.BeginTransactionAsync(); // ouvre transaction

            
            var user = await db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId); // cherche user dans la bdd
            if (user is null || user.Etat != EtatUtilisateur.Actif) // verif si user existe et que son compte est actif
                return Fail("Votre compte ne permet pas d'emprunter actuellement.");

            if (!await db.Medias.AnyAsync(m => m.Id == mediaId)) // verif si media existe
                return Fail("Ce média n'existe plus.");

            var actifs = await db.Emprunts                     
                .Where(e => e.IdUtilisateur == userId && e.DateRetour == null)
                .Select(e => e.IdMedia).ToListAsync();

            if (actifs.Contains(mediaId))                    // verif si media deja emprunté
				return Fail("Vous avez déjà emprunté ce média.");
            if (actifs.Count >= MaxEmpruntsActifs)          // verif limite d'emprunt
                return Fail($"Vous avez atteint la limite de {MaxEmpruntsActifs} emprunts en cours.");

            if (physique) // verfie le user demande un exemplaire physique ou non
            {
                // diminue stock de 1 si stock est sup a 0 
                int n = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Media SET Stock = Stock - 1 WHERE Id = {mediaId} AND Stock > 0");
                // permet de faire savoir quand le stock atteint 0
                if (n == 0) return Fail("Plus aucun exemplaire physique n'est disponible.");
            }

            var today = Today();
            var due = today.AddDays(DureeJours); // date de retour

            //crée un emprunt 
            db.Emprunts.Add(new Emprunt
            {
                DateEmprunt = today,
                DateRetourPrevue = due,
                EstPhysique = physique,
                IdUtilisateur = userId,
                IdMedia = mediaId,
            });

            //enregistre et valide la transaction
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return new BorrowResult(true, null, due);
        }

        
        // RENDRE : date de retour + réintégration du stock si physique
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
        public static async Task<(bool Success, string? Error)> ReturnAsync(int userId, int empruntId) // Permet de rendre un emprunt
        {
            await using var db = new MediaTechContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            var e = await db.Emprunts.FirstOrDefaultAsync(x => x.Id == empruntId && x.IdUtilisateur == userId); // recherche un emprunt 
            if (e is null) return (false, "Emprunt introuvable."); // verifie si l'emprunt n'existe pas
            if (e.DateRetour != null) return (false, "Cet emprunt a déjà été rendu."); // verifie si il a déja été rendu 

            e.DateRetour = Today(); // enregistre la date de retour

            // Les rappels liés à cet emprunt (échéance, retard) sont supprimé.
            var rappels = await db.Notifications.Where(n => n.IdEmprunt == empruntId).ToListAsync();
            db.Notifications.RemoveRange(rappels);

            await db.SaveChangesAsync();

            if (e.EstPhysique) // verifie si le media était physique
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Media SET Stock = Stock + 1 WHERE Id = {e.IdMedia}"); // si oui on update le stock

            await tx.CommitAsync(); // valide transaction
            NotificationService.NotifyChanged(); // notifie l'application qu'il y a eu une modification
            return (true, null);
        }

        // LECTURES
       
        public static async Task<ActiveLoan?> GetActiveLoanAsync(int userId, int mediaId)
        {
            await using var db = new MediaTechContext();
            var e = await db.Emprunts.AsNoTracking()
                .Where(x => x.IdUtilisateur == userId && x.IdMedia == mediaId && x.DateRetour == null)
                .Select(x => new { x.Id, x.DateRetourPrevue, x.EstPhysique })
                .FirstOrDefaultAsync();
            return e is null ? null : new ActiveLoan(e.Id, e.DateRetourPrevue, e.EstPhysique);
        }

        public static async Task<List<EmpruntRow>> GetUserLoansAsync(int userId) // Recupere les emprunts de l'utilisateur
        {
            await using var db = new MediaTechContext();

            // Recupere les emprunts et les trie
            var raw = await db.Emprunts.AsNoTracking()
                .Where(e => e.IdUtilisateur == userId)
                .OrderByDescending(e => e.DateEmprunt).ThenByDescending(e => e.Id)
                .Select(e => new
                {
                    e.Id, e.IdMedia,
                    Titre = e.Media.Titre,
                    e.EstPhysique, e.DateEmprunt, e.DateRetourPrevue, e.DateRetour,
                }).ToListAsync();

            var kinds = await KindsOfAsync(db, raw.Select(r => r.IdMedia).Distinct().ToList()); // Recherche du type de chaque media
            var today = Today();

            return raw.Select(e =>
            {
                bool active = e.DateRetour == null;
                int days = e.DateRetourPrevue.DayNumber - today.DayNumber; // calcul du jour de nombre restant
                bool late = active && days < 0; // detecte si il y a un retard

                string due;
                if (!active) due = $"Rendu le {Fmt(e.DateRetour!.Value)}"; // affichage si le media a été rendu
                else if (late) due = $"En retard de {-days} jour{(days < -1 ? "s" : "")} · à rendre le {Fmt(e.DateRetourPrevue)}";
                else if (days == 0) due = "À rendre aujourd'hui";
                else if (days == 1) due = "À rendre demain";
                else due = $"À rendre le {Fmt(e.DateRetourPrevue)} (dans {days} jours)";

                var kind = kinds.TryGetValue(e.IdMedia, out var k) ? k : MediaKind.Livre; // trouver type du media(livre par defaut)

                return new EmpruntRow // crée l'objet emprunt
                {
                    EmpruntId = e.Id,
                    MediaId = e.IdMedia,
                    Kind = kind,
                    Titre = e.Titre,
                    Subtitle = $"{CatalogueService.SingularLabel(kind)} · {(e.EstPhysique ? "Exemplaire physique" : "Numérique")} · emprunté le {Fmt(e.DateEmprunt, "d MMM yyyy")}", // construit une ligne recap(ex : Livre · Exemplaire physique · emprunté le 6 oct. 2026)
					DueText = due,
                    IsLate = late,
                    IsActive = active,
                    DateRetourPrevue = e.DateRetourPrevue,
                    DateRetour = e.DateRetour,
                };
            }).ToList();
        }

        // Le schéma n'a pas de colonne "type" : on déduit le sous-type par la table.
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
