using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    /// <summary>Une ligne de la page Notifications. IsRead notifie l'interface (mise à jour immédiate au clic).</summary>
    public class NotificationRow : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Footer { get; set; } = "";
        public bool IsGlobal { get; set; }
        public int? TargetMediaId { get; set; }
        public MediaKind? Kind { get; set; }

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead == value) return;
                _isRead = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRead)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public static class NotificationService
    {
        public const string TypeNouveauMedia = "NouveauMedia";
        public const string TypeRappel = "RappelRetour";
        public const string TypeRetard = "Retard";

        // ===== Règles (à ajuster ici) =====
        public const int RappelAvantJours = 3;   // rappel envoyé à partir de J-3
        public const int GlobalesJours = 30;     // une notif globale reste visible 30 jours

        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        /// <summary>Levé quand des notifications ont changé (pastille de la sidebar à rafraîchir).</summary>
        public static event Action? Changed;
        public static void NotifyChanged() => Changed?.Invoke();

        // Visible pour l'utilisateur : une globale récente, OU une notif qui lui est adressée (ligne existante).
        private static Expression<Func<Notification, bool>> Visible(int userId, DateTime since)
            => n => (n.EstGlobale && n.DateEnvoi >= since)
                    || n.Destinataires.Any(d => d.IdUtilisateur == userId);

        // Non lue = aucune ligne "EstLue = true" pour cet utilisateur.
        // (globale jamais cliquée : pas de ligne du tout ; individuelle : ligne avec EstLue = false)
        private static Expression<Func<Notification, bool>> Unread(int userId)
            => n => !n.Destinataires.Any(d => d.IdUtilisateur == userId && d.EstLue);

        // ---------------------------------------------------------------
        // LECTURE
        // ---------------------------------------------------------------
        public static async Task<int> CountUnreadAsync(int userId)
        {
            await using var db = new MediaTechContext();
            var since = DateTime.Now.AddDays(-GlobalesJours);
            return await db.Notifications
                .Where(Visible(userId, since))
                .Where(Unread(userId))
                .CountAsync();
        }

        public static async Task<List<NotificationRow>> GetForUserAsync(int userId)
        {
            await using var db = new MediaTechContext();
            var since = DateTime.Now.AddDays(-GlobalesJours);

            var raw = await db.Notifications.AsNoTracking()
                .Where(Visible(userId, since))
                .OrderByDescending(n => n.DateEnvoi).ThenByDescending(n => n.Id)
                .Select(n => new
                {
                    n.Id, n.Type, n.Message, n.DateEnvoi, n.EstGlobale,
                    MediaId = n.IdMedia ?? (n.Emprunt != null ? (int?)n.Emprunt.IdMedia : null),
                    EstLue = n.Destinataires.Any(d => d.IdUtilisateur == userId && d.EstLue),
                })
                .Take(100)
                .ToListAsync();

            var kinds = await EmpruntService.KindsOfAsync(db,
                raw.Where(r => r.MediaId != null).Select(r => r.MediaId!.Value).Distinct().ToList());

            return raw.Select(n =>
            {
                MediaKind? kind = n.MediaId is int mid && kinds.TryGetValue(mid, out var k) ? k : null;
                var label = TypeLabel(n.Type);
                return new NotificationRow
                {
                    Id = n.Id,
                    Type = n.Type,
                    Message = n.Message,
                    Icon = TypeIcon(n.Type),
                    Footer = $"{label} · {Relative(n.DateEnvoi)}",
                    IsGlobal = n.EstGlobale,
                    TargetMediaId = kind != null ? n.MediaId : null,
                    Kind = kind,
                    IsRead = n.EstLue,
                };
            }).ToList();
        }

        // ---------------------------------------------------------------
        // MARQUER COMME LU
        //   globale cliquée   -> INSERT (IdNotification, IdUtilisateur, EstLue = TRUE)
        //   individuelle      -> UPDATE de la ligne existante (EstLue = TRUE)
        // ---------------------------------------------------------------
        public static Task MarkReadAsync(int userId, int notificationId)
            => MarkAsync(userId, new List<int> { notificationId });

        public static async Task MarkAllReadAsync(int userId)
        {
            List<int> ids;
            await using (var db = new MediaTechContext())
            {
                var since = DateTime.Now.AddDays(-GlobalesJours);
                ids = await db.Notifications
                    .Where(Visible(userId, since)).Where(Unread(userId))
                    .Select(n => n.Id).ToListAsync();
            }
            await MarkAsync(userId, ids);
        }

        private static async Task MarkAsync(int userId, List<int> ids)
        {
            if (ids.Count == 0) return;

            await using var db = new MediaTechContext();

            var notifs = await db.Notifications.AsNoTracking()
                .Where(n => ids.Contains(n.Id))
                .Select(n => new { n.Id, n.EstGlobale }).ToListAsync();

            var set = db.Set<NotificationUtilisateur>();
            var existing = await set
                .Where(x => x.IdUtilisateur == userId && ids.Contains(x.IdNotification))
                .ToListAsync();

            foreach (var n in notifs)
            {
                var row = existing.FirstOrDefault(x => x.IdNotification == n.Id);
                if (row != null)
                {
                    if (!row.EstLue) row.EstLue = true;
                }
                else if (n.EstGlobale)
                {
                    // Le trigger SQL impose EstLue = TRUE pour une globale.
                    set.Add(new NotificationUtilisateur { IdNotification = n.Id, IdUtilisateur = userId, EstLue = true });
                }
                // individuelle sans ligne pour cet utilisateur : elle ne lui est pas destinée, on ne crée rien.
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Double clic / autre session : la ligne existe déjà, l'état voulu est atteint.
            }

            Changed?.Invoke();
        }

        // ---------------------------------------------------------------
        // GENERATION
        // ---------------------------------------------------------------

        /// <summary>
        /// Crée les rappels de retour (individuels) de l'utilisateur : J-3 puis retard, une fois chacun par emprunt.
        /// Appelé à la connexion et à l'ouverture de la page. Une ligne NotificationUtilisateur (EstLue = false)
        /// est créée tout de suite : c'est elle qui désigne le destinataire.
        /// </summary>
        public static async Task GenerateRemindersAsync(int userId)
        {
            await using var db = new MediaTechContext();
            var today = EmpruntService.Today();
            var limit = today.AddDays(RappelAvantJours);

            var loans = await db.Emprunts.AsNoTracking()
                .Where(e => e.IdUtilisateur == userId && e.DateRetour == null && e.DateRetourPrevue <= limit)
                .Select(e => new { e.Id, Titre = e.Media.Titre, e.DateRetourPrevue })
                .ToListAsync();
            if (loans.Count == 0) return;

            var loanIds = loans.Select(l => l.Id).ToList();
            var already = (await db.Notifications.AsNoTracking()
                    .Where(n => n.IdEmprunt != null && loanIds.Contains(n.IdEmprunt.Value))
                    .Select(n => new { n.IdEmprunt, n.Type }).ToListAsync())
                .Select(x => (x.IdEmprunt!.Value, x.Type))
                .ToHashSet();

            int created = 0;
            foreach (var l in loans)
            {
                bool late = l.DateRetourPrevue.DayNumber < today.DayNumber;
                var type = late ? TypeRetard : TypeRappel;
                if (already.Contains((l.Id, type))) continue;

                var date = l.DateRetourPrevue.ToString("d MMMM yyyy", Fr);
                var message = late
                    ? $"« {l.Titre} » devait être rendu le {date}. Merci de le rapporter."
                    : $"« {l.Titre} » est à rendre le {date}.";

                db.Notifications.Add(new Notification
                {
                    Type = type,
                    Message = Truncate(message, 255),
                    DateEnvoi = DateTime.Now,
                    EstGlobale = false,
                    IdEmprunt = l.Id,
                    Destinataires = { new NotificationUtilisateur { IdUtilisateur = userId, EstLue = false } },
                });
                created++;
            }

            if (created > 0)
            {
                await db.SaveChangesAsync();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Notification globale "nouveau média". Aucune ligne NotificationUtilisateur n'est créée ici.
        /// À appeler depuis la gestion des contenus (admin) juste après l'ajout d'un média.
        /// </summary>
        public static async Task PublishNewMediaAsync(int mediaId, string titre)
        {
            await using var db = new MediaTechContext();
            db.Notifications.Add(new Notification
            {
                Type = TypeNouveauMedia,
                Message = Truncate($"Nouveau : « {titre} » est disponible au catalogue.", 255),
                DateEnvoi = DateTime.Now,
                EstGlobale = true,
                IdMedia = mediaId,
            });
            await db.SaveChangesAsync();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------
        // Présentation
        // ---------------------------------------------------------------
        private static string TypeLabel(string type) => type switch
        {
            TypeNouveauMedia => "Nouveauté",
            TypeRappel => "Rappel de retour",
            TypeRetard => "Retard",
            _ => type,
        };

        private static string TypeIcon(string type) => type switch
        {
            TypeNouveauMedia => "\u2728",   // ✨
            TypeRappel => "\u23F0",         // ⏰
            TypeRetard => "\u26A0\uFE0F",   // ⚠️
            _ => "\U0001F514",              // 🔔
        };

        private static string Relative(DateTime d)
        {
            var span = DateTime.Now - d;
            if (span.TotalMinutes < 1) return "à l'instant";
            if (span.TotalMinutes < 60) return $"il y a {(int)span.TotalMinutes} min";
            if (span.TotalHours < 24) return $"il y a {(int)span.TotalHours} h";
            if (span.TotalDays < 7) return $"il y a {(int)span.TotalDays} j";
            return d.ToString("d MMM yyyy", Fr);
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
