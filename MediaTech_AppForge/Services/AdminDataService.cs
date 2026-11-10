using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;
using MediaTech_AppForge.ViewModels;

namespace MediaTech_AppForge.Services
{
    public enum RefKind { Auteur, Editeur, Genre }

    /// <summary>Gestion des auteurs, éditeurs, genres et utilisateurs. Toutes les méthodes exigent un admin.</summary>
    public static class AdminDataService
    {
        private static void RequireAdmin()
        {
            if (!Session.IsAdmin)
            {
                throw new UnauthorizedAccessException("Action réservée aux administrateurs.");
            }
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
                    var auteurs = await db.Auteurs.AsNoTracking().OrderBy(auteur => auteur.Nom).Select(auteur =>
                        new
                        {
                            auteur.Id,
                            auteur.Nom,
                            NbMedias = auteur.Medias.Count()
                        }).ToListAsync();



                    return auteurs.Select(auteur => 
                    new RefRow { 
                        Id = auteur.Id,
                        Nom = auteur.Nom,
                        Usage = auteur.NbMedias }).ToList();

                case RefKind.Editeur:
                    var editeurs = await db.Editeurs.AsNoTracking().OrderBy(editeur => editeur.Nom).Select(editeur =>
                    new
                    {
                        editeur.Id,
                        editeur.Nom,
                        NbMedias = editeur.Medias.Count()
                    }).ToListAsync();

                    return editeurs.Select(editeur => new RefRow { Id = editeur.Id, Nom = editeur.Nom, Usage = editeur.NbMedias }).ToList();

                default:
                    var genres = await db.Genres.AsNoTracking().OrderBy(genre => genre.Libelle)
                        .Select(genre => new
                        {
                            genre.Id,
                            genre.Libelle,
                            NomParent = genre.GenreParent != null ? genre.GenreParent.Libelle : null,
                            NbMedias = genre.Medias.Count(),
                        }).ToListAsync();
                    return genres.Select(genre => new RefRow { Id = genre.Id, Nom = genre.Libelle, ParentNom = genre.NomParent, Usage = genre.NbMedias }).ToList();
            }
        }

        /// <summary>Création (id == null) ou renommage. parentNom ne sert qu'aux genres.</summary>
        public static async Task<(bool Success, string? Error)> SaveRefAsync(RefKind kind, int? id, string? nom, string? parentNom)
        {
            RequireAdmin();

            nom = (nom ?? "").Trim();
            if (nom.Length == 0)
            {
                return (false, "Le nom est obligatoire.");
            }
            if (nom.Length > 100)
            {
                return (false, "Le nom ne peut pas dépasser 100 caractères.");
            }

            await using var db = new MediaTechContext();
            int idAIgnorer = id ?? 0; // pour ignorer l'élément lui-même dans le contrôle des doublons

            switch (kind)
            {
                case RefKind.Auteur:
                    if (await db.Auteurs.AnyAsync(auteur => auteur.Nom == nom && auteur.Id != idAIgnorer))
                    {
                        return (false, "Un auteur porte déjà ce nom.");
                    }

                    if (id is null)
                    {
                        db.Auteurs.Add(new Auteur { Nom = nom });
                    }
                    else if (await db.Auteurs.FindAsync(id.Value) is { } auteurExistant)
                    {
                        auteurExistant.Nom = nom;
                    }
                    else
                    {
                        return (false, "Cet auteur n'existe plus.");
                    }
                    break;

                case RefKind.Editeur:
                    if (await db.Editeurs.AnyAsync(editeur => editeur.Nom == nom && editeur.Id != idAIgnorer))
                    {
                        return (false, "Un éditeur porte déjà ce nom.");
                    }

                    if (id is null)
                    {
                        db.Editeurs.Add(new Editeur { Nom = nom });
                    }
                    else if (await db.Editeurs.FindAsync(id.Value) is { } editeurExistant)
                    {
                        editeurExistant.Nom = nom;
                    }
                    else
                    {
                        return (false, "Cet éditeur n'existe plus.");
                    }
                    break;

                default:
                    if (await db.Genres.AnyAsync(genre => genre.Libelle == nom && genre.Id != idAIgnorer))
                    {
                        return (false, "Un genre porte déjà ce libellé.");
                    }

                    int idParent = 0;
                    string nomParent = parentNom?.Trim();
                    if (!string.IsNullOrEmpty(nomParent))
                    {
                        var genreParent = await db.Genres.AsNoTracking().FirstOrDefaultAsync(genre => genre.Libelle == nomParent);
                        if (genreParent is null)
                        {
                            return (false, $"Genre parent « {nomParent} » introuvable. Créez-le d'abord.");
                        }
                        idParent = genreParent.Id;

                        if (id is int idGenreModifie)
                        {
                            if (genreParent.Id == idGenreModifie)
                            {
                                return (false, "Un genre ne peut pas être son propre parent.");
                            }

                            // Pas de boucle : on remonte la chaîne des parents du parent choisi.
                            var idParentParIdGenre = (await db.Genres.AsNoTracking().Select(genre => new { genre.Id, genre.IdGenreParent }).ToListAsync())
                                                     .ToDictionary(genre => genre.Id, genre => genre.IdGenreParent);
                            int? idAncetre = genreParent.IdGenreParent;
                            int compteurSecurite = 0; // évite une boucle infinie si les données sont incohérentes
                            while (idAncetre is int idCourant && compteurSecurite++ < 100)
                            {
                                if (idCourant == idGenreModifie)
                                {
                                    return (false, "Ce parent est un sous-genre de ce genre : cela créerait une boucle.");
                                }
                                idAncetre = idParentParIdGenre.TryGetValue(idCourant, out var idParentSuivant) ? idParentSuivant : null;
                            }
                        }
                    }

                    if (id is null)
                    {
                        db.Genres.Add(new Genre { Libelle = nom, IdGenreParent = idParent });
                    }
                    else if (await db.Genres.FindAsync(id.Value) is { } genreExistant)
                    {
                        genreExistant.Libelle = nom;
                        genreExistant.IdGenreParent = idParent;
                    }
                    else
                    {
                        return (false, "Ce genre n'existe plus.");
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
                    int nbMediasLies = await db.Medias.CountAsync(media => media.IdEditeur == id);
                    if (nbMediasLies > 0)
                    {
                        return (false, $"Suppression impossible : {CatalogueService.Plural(nbMediasLies, "média", "médias")} " +
                                       $"{(nbMediasLies > 1 ? "utilisent" : "utilise")} cet éditeur. Modifiez d'abord ces médias.");
                    }
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

            var utilisateursBruts = await db.Utilisateurs.AsNoTracking().OrderBy(utilisateur => utilisateur.Username)
                .Select(utilisateur => new
                {
                    utilisateur.Id, utilisateur.Username, utilisateur.IsAdmin, utilisateur.Etat,
                    NbEmpruntsActifs = utilisateur.Emprunts.Count(emprunt => emprunt.DateRetour == null),
                }).ToListAsync();

            return utilisateursBruts.Select(utilisateur => new UserRow
            {
                Id = utilisateur.Id, Username = utilisateur.Username, IsAdmin = utilisateur.IsAdmin,
                Etat = utilisateur.Etat, ActiveLoans = utilisateur.NbEmpruntsActifs,
            }).ToList();
        }

        /// <summary>password vide en modification = mot de passe inchangé.</summary>
        public static async Task<(bool Success, string? Error)> SaveUserAsync(
            int? id, string? username, string? password, bool isAdmin, EtatUtilisateur etat)
        {
            RequireAdmin();

            username = (username ?? "").Trim();
            if (username.Length < 3)
            {
                return (false, "L'identifiant doit contenir au moins 3 caractères.");
            }
            if (username.Length > 50)
            {
                return (false, "L'identifiant ne peut pas dépasser 50 caractères.");
            }

            bool estCreation = id is null;
            password ??= "";
            if ((estCreation || password.Length > 0) && password.Length < 6)
            {
                return (false, "Le mot de passe doit contenir au moins 6 caractères.");
            }

            await using var db = new MediaTechContext();
            int idAIgnorer = id ?? 0;
            if (await db.Utilisateurs.AnyAsync(utilisateur => utilisateur.Username == username && utilisateur.Id != idAIgnorer))
            {
                return (false, "Cet identifiant est déjà utilisé.");
            }

            bool aRenommeSonCompte = false;
            Utilisateur? utilisateurModifie = null;

            if (estCreation)
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
                utilisateurModifie = await db.Utilisateurs.FindAsync(id!.Value);
                if (utilisateurModifie is null)
                {
                    return (false, "Cet utilisateur n'existe plus.");
                }

                bool estSoiMeme = utilisateurModifie.Id == Session.CurrentUser?.Id;
                // Évite de se retirer ses propres droits (et donc l'accès à cet écran).
                if (estSoiMeme && (utilisateurModifie.IsAdmin != isAdmin || utilisateurModifie.Etat != etat))
                {
                    return (false, "Vous ne pouvez pas modifier vos propres droits ni votre état.");
                }

                aRenommeSonCompte = estSoiMeme && utilisateurModifie.Username != username;
                utilisateurModifie.Username = username;
                utilisateurModifie.IsAdmin = isAdmin;
                utilisateurModifie.Etat = etat;
                if (password.Length > 0)
                {
                    utilisateurModifie.Password = PasswordHasher.Hash(password);
                }
            }

            await db.SaveChangesAsync();

            // Garde la sidebar à jour si l'admin a renommé son propre compte.
            if (aRenommeSonCompte && utilisateurModifie != null)
            {
                Session.SignIn(utilisateurModifie);
            }

            return (true, null);
        }

        public static async Task<(bool Success, string? Error)> DeleteUserAsync(int id)
        {
            RequireAdmin();

            if (id == Session.CurrentUser?.Id)
            {
                return (false, "Vous ne pouvez pas supprimer votre propre compte.");
            }

            await using var db = new MediaTechContext();

            // Supprimer un compte supprimerait aussi ses emprunts (CASCADE) : un exemplaire physique
            // encore dehors ne reviendrait jamais en stock. On exige donc que tout soit rendu.
            int nbEmpruntsActifs = await db.Emprunts.CountAsync(emprunt => emprunt.IdUtilisateur == id && emprunt.DateRetour == null);
            if (nbEmpruntsActifs > 0)
            {
                return (false, $"Suppression impossible : {CatalogueService.Plural(nbEmpruntsActifs, "emprunt", "emprunts")} en cours. " +
                               "Faites-les rendre d'abord, ou passez plutôt le compte à « Banni ».");
            }

            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Utilisateur WHERE Id = {id}");
            return (true, null);
        }
    }
}
