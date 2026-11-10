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
using MediaTech_AppForge.ViewModels;

namespace MediaTech_AppForge.Services
{

    public static class NotificationService
    {
        public const string TypeNouveauMedia = "NouveauMedia";
        public const string TypeRappel = "RappelRetour";
        public const string TypeRetard = "Retard";

        // ===== Règles (à ajuster ici) =====
        public const int RappelAvantJours = 3;   // rappel envoyé à partir de J-3
        public const int GlobalesJours = 30;     // une notif globale reste visible 30 jours

        private static readonly CultureInfo CultureFrancaise = CultureInfo.GetCultureInfo("fr-FR");

        /// <summary>Levé quand des notifications ont changé (pastille de la sidebar à rafraîchir).</summary>
        public static event Action? Changed;
        public static void NotifyChanged() => Changed?.Invoke();

        // Visible pour l'utilisateur : une globale récente, OU une notif qui lui est adressée (ligne existante).
        private static Expression<Func<Notification, bool>> Visible(int userId, DateTime dateLimite)
            => notification => (notification.EstGlobale && notification.DateEnvoi >= dateLimite)
                               || notification.Destinataires.Any(destinataire => destinataire.IdUtilisateur == userId);

        // Non lue = aucune ligne "EstLue = true" pour cet utilisateur.
        // (globale jamais cliquée : pas de ligne du tout ; individuelle : ligne avec EstLue = false)
        private static Expression<Func<Notification, bool>> Unread(int userId)
            => notification => !notification.Destinataires.Any(destinataire => destinataire.IdUtilisateur == userId && destinataire.EstLue);

        // ---------------------------------------------------------------
        // LECTURE
        // ---------------------------------------------------------------
        public static async Task<int> CountUnreadAsync(int userId)
        {
            await using var db = new MediaTechContext();
            DateTime dateLimite = DateTime.Now.AddDays(-GlobalesJours);
            return await db.Notifications
                .Where(Visible(userId, dateLimite))
                .Where(Unread(userId))
                .CountAsync();
        }

        public static async Task<List<NotificationRow>> GetForUserAsync(int userId)
        {
            await using var db = new MediaTechContext();
            DateTime dateLimite = DateTime.Now.AddDays(-GlobalesJours);

            var notificationsBrutes = await db.Notifications.AsNoTracking()
                .Where(Visible(userId, dateLimite))
                .OrderByDescending(notification => notification.DateEnvoi).ThenByDescending(notification => notification.Id)
                .Select(notification => new
                {
                    notification.Id, notification.Type, notification.Message, notification.DateEnvoi, notification.EstGlobale,
                    MediaId = notification.IdMedia ?? (notification.Emprunt != null ? (int?)notification.Emprunt.IdMedia : null),
                    EstLue = notification.Destinataires.Any(destinataire => destinataire.IdUtilisateur == userId && destinataire.EstLue),
                })
                .Take(100)
                .ToListAsync();

            var typesParIdMedia = await EmpruntService.KindsOfAsync(db,
                notificationsBrutes.Where(notification => notification.MediaId != null).Select(notification => notification.MediaId!.Value).Distinct().ToList());

            return notificationsBrutes.Select(notification =>
            {
                MediaKind? kind = notification.MediaId is int idMedia && typesParIdMedia.TryGetValue(idMedia, out var typeTrouve) ? typeTrouve : null;
                string libelleType = TypeLabel(notification.Type);
                return new NotificationRow
                {
                    Id = notification.Id,
                    Type = notification.Type,
                    Message = notification.Message,
                    Icon = TypeIcon(notification.Type),
                    Footer = $"{libelleType} · {Relative(notification.DateEnvoi)}",
                    IsGlobal = notification.EstGlobale,
                    TargetMediaId = kind != null ? notification.MediaId : null,
                    Kind = kind,
                    IsRead = notification.EstLue,
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
            List<int> idsNotificationsNonLues;
            await using (var db = new MediaTechContext())
            {
                DateTime dateLimite = DateTime.Now.AddDays(-GlobalesJours);
                idsNotificationsNonLues = await db.Notifications
                    .Where(Visible(userId, dateLimite)).Where(Unread(userId))
                    .Select(notification => notification.Id).ToListAsync();
            }
            await MarkAsync(userId, idsNotificationsNonLues);
        }

        private static async Task MarkAsync(int userId, List<int> idsNotifications)
        {
            if (idsNotifications.Count == 0)
            {
                return;
            }

            await using var db = new MediaTechContext();

            var notificationsConcernees = await db.Notifications.AsNoTracking()
                .Where(notification => idsNotifications.Contains(notification.Id))
                .Select(notification => new { notification.Id, notification.EstGlobale }).ToListAsync();

            var tableDestinataires = db.Set<NotificationUtilisateur>();
            var lignesExistantes = await tableDestinataires
                .Where(ligne => ligne.IdUtilisateur == userId && idsNotifications.Contains(ligne.IdNotification))
                .ToListAsync();

            foreach (var notification in notificationsConcernees)
            {
                var ligneExistante = lignesExistantes.FirstOrDefault(ligne => ligne.IdNotification == notification.Id);

                if (ligneExistante != null)
                {
                    ligneExistante.EstLue = true;
                }
                // Le trigger SQL impose EstLue = TRUE pour une globale.
                else if (notification.EstGlobale)
                {
                    tableDestinataires.Add(new NotificationUtilisateur { IdNotification = notification.Id, IdUtilisateur = userId, EstLue = true });
                }
                // individuelle sans ligne pour cet utilisateur : elle ne lui est pas destinée, on ne crée rien.
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Double clic / autre session : on ne tolère l'erreur que si l'état voulu est bien en base.
                // Sinon (colonne inconnue, trigger, FK...) on remonte la vraie erreur à l'écran.
                await using var dbVerification = new MediaTechContext();
                int nbDejaLues = await dbVerification.Set<NotificationUtilisateur>()
                    .CountAsync(ligne => ligne.IdUtilisateur == userId && idsNotifications.Contains(ligne.IdNotification) && ligne.EstLue);
                if (nbDejaLues < notificationsConcernees.Count)
                {
                    throw;
                }
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
            DateOnly aujourdHui = EmpruntService.Today();
            DateOnly dateLimiteRappel = aujourdHui.AddDays(RappelAvantJours);

            var empruntsARappeler = await db.Emprunts.AsNoTracking()
                .Where(emprunt => emprunt.IdUtilisateur == userId && emprunt.DateRetour == null && emprunt.DateRetourPrevue <= dateLimiteRappel)
                .Select(emprunt => new { emprunt.Id, Titre = emprunt.Media.Titre, emprunt.DateRetourPrevue })
                .ToListAsync();
            if (empruntsARappeler.Count == 0)
            {
                return;
            }

            var idsEmprunts = empruntsARappeler.Select(emprunt => emprunt.Id).ToList();
            var rappelsDejaCrees = (await db.Notifications.AsNoTracking()
                    .Where(notification => notification.IdEmprunt != null && idsEmprunts.Contains(notification.IdEmprunt.Value))
                    .Select(notification => new { notification.IdEmprunt, notification.Type }).ToListAsync())
                .Select(rappel => (rappel.IdEmprunt!.Value, rappel.Type))
                .ToHashSet();

            int nbRappelsCrees = 0;
            foreach (var emprunt in empruntsARappeler)
            {
                bool enRetard = emprunt.DateRetourPrevue.DayNumber < aujourdHui.DayNumber;
                string typeRappel = enRetard ? TypeRetard : TypeRappel;
                if (rappelsDejaCrees.Contains((emprunt.Id, typeRappel)))
                {
                    continue;
                }

                string dateTexte = emprunt.DateRetourPrevue.ToString("d MMMM yyyy", CultureFrancaise);
                string message = enRetard
                    ? $"« {emprunt.Titre} » devait être rendu le {dateTexte}. Merci de le rapporter."
                    : $"« {emprunt.Titre} » est à rendre le {dateTexte}.";

                db.Notifications.Add(new Notification
                {
                    Type = typeRappel,
                    Message = Truncate(message, 255),
                    DateEnvoi = DateTime.Now,
                    EstGlobale = false,
                    IdEmprunt = emprunt.Id,
                    Destinataires = { new NotificationUtilisateur { IdUtilisateur = userId, EstLue = false } },
                });
                nbRappelsCrees++;
            }

            if (nbRappelsCrees == 0)
            {
                return;
            }

            await db.SaveChangesAsync();
            Changed?.Invoke();
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

        private static string Relative(DateTime dateEnvoi)
        {
            TimeSpan tempsEcoule = DateTime.Now - dateEnvoi;
            if (tempsEcoule.TotalMinutes < 1)
            {
                return "à l'instant";
            }
            if (tempsEcoule.TotalMinutes < 60)
            {
                return $"il y a {(int)tempsEcoule.TotalMinutes} min";
            }
            if (tempsEcoule.TotalHours < 24)
            {
                return $"il y a {(int)tempsEcoule.TotalHours} h";
            }
            if (tempsEcoule.TotalDays < 7)
            {
                return $"il y a {(int)tempsEcoule.TotalDays} j";
            }
            return dateEnvoi.ToString("d MMM yyyy", CultureFrancaise);
        }

        private static string Truncate(string texte, int longueurMax) => texte.Length <= longueurMax ? texte : texte[..(longueurMax - 1)] + "…";
    }
}
