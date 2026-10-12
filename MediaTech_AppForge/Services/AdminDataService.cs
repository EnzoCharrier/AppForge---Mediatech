using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    public enum RefKind { Auteur, Editeur, Genre }

    /// <summary>Une ligne de la liste Auteurs / Éditeurs / Genres.</summary>
    public class RefRow
    {
        public int Id { get; set; }
        public string Nom { get; set; } = "";
        public string? ParentNom { get; set; }   // genres uniquement
        public int Usage { get; set; }           // nombre de médias liés

        public string Detail => ParentNom is null ? "" : $"Sous-genre de {ParentNom}";
        public string UsageText => Usage == 0 ? "Aucun média" : CatalogueService.Plural(Usage, "média", "médias");
    }

    /// <summary>Une ligne de la liste des utilisateurs.</summary>
    public class UserRow
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public bool IsAdmin { get; set; }
        public EtatUtilisateur Etat { get; set; }
        public int ActiveLoans { get; set; }

        public string Subtitle =>
            $"{(IsAdmin ? "Administrateur" : "Membre")} · " +
            (ActiveLoans == 0 ? "aucun emprunt en cours" : CatalogueService.Plural(ActiveLoans, "emprunt en cours", "emprunts en cours"));

        // Vide si le compte est actif (le badge est alors masqué).
        public string EtatBadge => Etat == EtatUtilisateur.Actif ? "" : Etat.ToString();
    }

    /// <summary>Gestion des auteurs, éditeurs, genres et utilisateurs. Toutes les méthodes exigent un admin.</summary>
    public static class AdminDataService
    {
        private static void RequireAdmin()
        {
            if (!Session.IsAdmin)
                throw new UnauthorizedAccessException("Action réservée aux administrateurs.");
        }

        // =====================================================================
        // AUTEURS / ÉDITEURS / GENRES
        // =====================================================================
        public static async Task<List<RefRow>> GetRefsAsync(RefKind kind)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            switch (kind)
            {
                case RefKind.Auteur:
                    var auteurs = await db.Auteurs.AsNoTracking().OrderBy(a => a.Nom)
                        .Select(a => new { a.Id, a.Nom, Usage = a.Medias.Count() }).ToListAsync();
                    return auteurs.Select(x => new RefRow { Id = x.Id, Nom = x.Nom, Usage = x.Usage }).ToList();

                case RefKind.Editeur:
                    var editeurs = await db.Editeurs.AsNoTracking().OrderBy(e => e.Nom)
                        .Select(e => new { e.Id, e.Nom, Usage = e.Medias.Count() }).ToListAsync();
                    return editeurs.Select(x => new RefRow { Id = x.Id, Nom = x.Nom, Usage = x.Usage }).ToList();

                default:
                    var genres = await db.Genres.AsNoTracking().OrderBy(g => g.Libelle)
                        .Select(g => new
                        {
                            g.Id,
                            g.Libelle,
                            Parent = g.GenreParent != null ? g.GenreParent.Libelle : null,
                            Usage = g.Medias.Count(),
                        }).ToListAsync();
                    return genres.Select(x => new RefRow { Id = x.Id, Nom = x.Libelle, ParentNom = x.Parent, Usage = x.Usage }).ToList();
            }
        }

        /// <summary>Création (id == null) ou renommage. parentNom ne sert qu'aux genres.</summary>
        public static async Task<(bool Success, string? Error)> SaveRefAsync(RefKind kind, int? id, string? nom, string? parentNom)
        {
            RequireAdmin();

            nom = (nom ?? "").Trim();
            if (nom.Length == 0) return (false, "Le nom est obligatoire.");
            if (nom.Length > 100) return (false, "Le nom ne peut pas dépasser 100 caractères.");

            await using var db = new MediaTechContext();
            int excludeId = id ?? 0; // pour ignorer l'élément lui-même dans le contrôle des doublons

            switch (kind)
            {
                case RefKind.Auteur:
                    if (await db.Auteurs.AnyAsync(a => a.Nom == nom && a.Id != excludeId))
                        return (false, "Un auteur porte déjà ce nom.");
                    if (id is null) db.Auteurs.Add(new Auteur { Nom = nom });
                    else
                    {
                        var a = await db.Auteurs.FindAsync(id.Value);
                        if (a is null) return (false, "Cet auteur n'existe plus.");
                        a.Nom = nom;
                    }
                    break;

                case RefKind.Editeur:
                    if (await db.Editeurs.AnyAsync(e => e.Nom == nom && e.Id != excludeId))
                        return (false, "Un éditeur porte déjà ce nom.");
                    if (id is null) db.Editeurs.Add(new Editeur { Nom = nom });
                    else
                    {
                        var e = await db.Editeurs.FindAsync(id.Value);
                        if (e is null) return (false, "Cet éditeur n'existe plus.");
                        e.Nom = nom;
                    }
                    break;

                default:
                    if (await db.Genres.AnyAsync(g => g.Libelle == nom && g.Id != excludeId))
                        return (false, "Un genre porte déjà ce libellé.");

                    int? parentId = null;
                    var parentTxt = parentNom?.Trim();
                    if (!string.IsNullOrEmpty(parentTxt))
                    {
                        var parent = await db.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.Libelle == parentTxt);
                        if (parent is null)
                            return (false, $"Genre parent « {parentTxt} » introuvable. Créez-le d'abord.");
                        parentId = parent.Id;

                        if (id is int self)
                        {
                            if (parent.Id == self) return (false, "Un genre ne peut pas être son propre parent.");

                            // Pas de boucle : on remonte la chaîne des parents du parent choisi.
                            var up = (await db.Genres.AsNoTracking().Select(g => new { g.Id, g.IdGenreParent }).ToListAsync())
                                     .ToDictionary(g => g.Id, g => g.IdGenreParent);
                            int? cur = parent.IdGenreParent;
                            int guard = 0;
                            while (cur is int c && guard++ < 100)
                            {
                                if (c == self)
                                    return (false, "Ce parent est un sous-genre de ce genre : cela créerait une boucle.");
                                cur = up.TryGetValue(c, out var next) ? next : null;
                            }
                        }
                    }

                    if (id is null) db.Genres.Add(new Genre { Libelle = nom, IdGenreParent = parentId });
                    else
                    {
                        var g = await db.Genres.FindAsync(id.Value);
                        if (g is null) return (false, "Ce genre n'existe plus.");
                        g.Libelle = nom;
                        g.IdGenreParent = parentId;
                    }
                    break;
            }

            await db.SaveChangesAsync();
            return (true, null);
        }

        public static async Task<(bool Success, string? Error)> DeleteRefAsync(RefKind kind, int id)
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            switch (kind)
            {
                case RefKind.Editeur:
                    // Media.IdEditeur est en ON DELETE RESTRICT : on explique plutôt que de laisser MySQL refuser.
                    int n = await db.Medias.CountAsync(m => m.IdEditeur == id);
                    if (n > 0)
                        return (false, $"Suppression impossible : {CatalogueService.Plural(n, "média", "médias")} " +
                                       $"{(n > 1 ? "utilisent" : "utilise")} cet éditeur. Modifiez d'abord ces médias.");
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Editeur WHERE Id = {id}");
                    break;

                case RefKind.Auteur:
                    // FK ON DELETE SET NULL : les médias restent, sans auteur.
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Auteur WHERE Id = {id}");
                    break;

                default:
                    // Media_Genre en CASCADE (liens retirés) ; les sous-genres passent à "sans parent" (SET NULL).
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Genre WHERE Id = {id}");
                    break;
            }
            return (true, null);
        }

        // =====================================================================
        // UTILISATEURS
        // =====================================================================
        public static async Task<List<UserRow>> GetUsersAsync()
        {
            RequireAdmin();
            await using var db = new MediaTechContext();

            var raw = await db.Utilisateurs.AsNoTracking().OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id, u.Username, u.IsAdmin, u.Etat,
                    Active = u.Emprunts.Count(e => e.DateRetour == null),
                }).ToListAsync();

            return raw.Select(u => new UserRow
            {
                Id = u.Id, Username = u.Username, IsAdmin = u.IsAdmin, Etat = u.Etat, ActiveLoans = u.Active,
            }).ToList();
        }

        /// <summary>password vide en modification = mot de passe inchangé.</summary>
        public static async Task<(bool Success, string? Error)> SaveUserAsync(
            int? id, string? username, string? password, bool isAdmin, EtatUtilisateur etat)
        {
            RequireAdmin();

            username = (username ?? "").Trim();
            if (username.Length < 3) return (false, "L'identifiant doit contenir au moins 3 caractères.");
            if (username.Length > 50) return (false, "L'identifiant ne peut pas dépasser 50 caractères.");

            bool creating = id is null;
            password ??= "";
            if ((creating || password.Length > 0) && password.Length < 6)
                return (false, "Le mot de passe doit contenir au moins 6 caractères.");

            await using var db = new MediaTechContext();
            int excludeId = id ?? 0;
            if (await db.Utilisateurs.AnyAsync(u => u.Username == username && u.Id != excludeId))
                return (false, "Cet identifiant est déjà utilisé.");

            bool renamedSelf = false;
            Utilisateur? edited = null;

            if (creating)
            {
                db.Utilisateurs.Add(new Utilisateur
                {
                    Username = username,
                    Password = PasswordHasher.Hash(password),
                    IsAdmin = isAdmin,
                    Etat = etat,
                });
            }
            else
            {
                var u = await db.Utilisateurs.FindAsync(id!.Value);
                if (u is null) return (false, "Cet utilisateur n'existe plus.");

                bool self = u.Id == Session.CurrentUser?.Id;
                // Évite de se retirer ses propres droits (et donc l'accès à cet écran).
                if (self && (u.IsAdmin != isAdmin || u.Etat != etat))
                    return (false, "Vous ne pouvez pas modifier vos propres droits ni votre état.");

                renamedSelf = self && u.Username != username;
                u.Username = username;
                u.IsAdmin = isAdmin;
                u.Etat = etat;
                if (password.Length > 0) u.Password = PasswordHasher.Hash(password);
                edited = u;
            }

            await db.SaveChangesAsync();

            // Garde la sidebar à jour si l'admin a renommé son propre compte.
            if (renamedSelf && edited != null) Session.SignIn(edited);

            return (true, null);
        }

        public static async Task<(bool Success, string? Error)> DeleteUserAsync(int id)
        {
            RequireAdmin();

            if (id == Session.CurrentUser?.Id)
                return (false, "Vous ne pouvez pas supprimer votre propre compte.");

            await using var db = new MediaTechContext();

            // Supprimer un compte supprimerait aussi ses emprunts (CASCADE) : un exemplaire physique
            // encore dehors ne reviendrait jamais en stock. On exige donc que tout soit rendu.
            int active = await db.Emprunts.CountAsync(e => e.IdUtilisateur == id && e.DateRetour == null);
            if (active > 0)
                return (false, $"Suppression impossible : {CatalogueService.Plural(active, "emprunt", "emprunts")} en cours. " +
                               "Faites-les rendre d'abord, ou passez plutôt le compte à « Banni ».");

            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Utilisateur WHERE Id = {id}");
            return (true, null);
        }
    }
}
