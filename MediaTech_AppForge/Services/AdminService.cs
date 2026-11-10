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
	// -----------------------------------------------------------------
	// Petits objets de transfert de données (DTO)
	// Un "record" est une classe simple, immuable, qui sert à transporter des valeurs.
	// -----------------------------------------------------------------

	/// <summary>Une piste d'un CD : un titre et une durée facultative.</summary>
	public record PisteInput(string Titre, TimeSpan? Duree);

	/// <summary>Listes utilisées pour remplir les menus déroulants du formulaire.</summary>
	public record LookupData(List<string> Editeurs, List<string> Auteurs, List<GenreInfo> Genres);

	/// <summary>Résultat d'un enregistrement : l'id du média et un éventuel avertissement.</summary>
	public record SaveResult(int Id, string? Warning);

	/// <summary>
	/// Opérations d'administration du catalogue (lecture, création, modification, suppression).
	/// Chaque méthode publique vérifie d'abord que l'utilisateur connecté est administrateur.
	/// </summary>
	public static class AdminService
	{
		// Champs communs à tous les médias, lus depuis la base pour pré-remplir le formulaire.
		private record MediaCommonInfo(
			string Titre,
			DateOnly DateSortie,
			int Stock,
			string Editeur,
			string? Auteur,
			List<int> GenreIds);

		// =================================================================
		// Sécurité
		// =================================================================

		/// <summary>Lève une exception si l'utilisateur courant n'est pas administrateur.</summary>
		private static void RequireAdmin()
		{
			if (!Session.IsAdmin)
			{
				throw new UnauthorizedAccessException("Action réservée aux administrateurs.");
			}
		}

		// =================================================================
		// Listes de choix (éditeurs, auteurs, genres)
		// =================================================================

		public static async Task<LookupData> GetLookupsAsync()
		{
			RequireAdmin();

			await using var db = new MediaTechContext();

			// AsNoTracking : on ne fait que lire, EF n'a pas besoin de suivre les objets (plus rapide).
			List<string> nomsEditeurs = await db.Editeurs
				.AsNoTracking()
				.OrderBy(editeur => editeur.Nom)
				.Select(editeur => editeur.Nom)
				.ToListAsync();

			List<string> nomsAuteurs = await db.Auteurs
				.AsNoTracking()
				.OrderBy(auteur => auteur.Nom)
				.Select(auteur => auteur.Nom)
				.ToListAsync();

			List<GenreInfo> genres = await db.Genres
				.AsNoTracking()
				.Select(genre => new GenreInfo(genre.Id, genre.Libelle, genre.IdGenreParent))
				.ToListAsync();

			return new LookupData(nomsEditeurs, nomsAuteurs, genres);
		}

		// =================================================================
		// Lecture d'un média pour le modifier
		// =================================================================

		public static async Task<MediaEditModel?> GetMediaForEditAsync(int id, MediaKind kind)
		{
			RequireAdmin();

			await using var db = new MediaTechContext();

			// Étape 1 : lire les informations communes à tous les types de médias.
			MediaCommonInfo? infosCommunes = await db.Medias
				.AsNoTracking()
				.Where(media => media.Id == id)
				.Select(media => new MediaCommonInfo(
					media.Titre,
					media.DateSortie,
					media.Stock,
					media.Editeur.Nom,
					media.Auteur != null ? media.Auteur.Nom : null,
					media.Genres.Select(genre => genre.Id).ToList()))
				.FirstOrDefaultAsync();

			if (infosCommunes is null)
			{
				return null; // Le média n'existe pas.
			}

			MediaEditModel model = new MediaEditModel
			{
				Kind = kind,
				Titre = infosCommunes.Titre,
				DateSortie = infosCommunes.DateSortie,
				Stock = infosCommunes.Stock,
				Editeur = infosCommunes.Editeur,
				Auteur = infosCommunes.Auteur,
				GenreIds = infosCommunes.GenreIds,
			};

			// Étape 2 : lire les champs propres au type (livre, magazine, DVD ou CD).
			await LoadSpecificFieldsAsync(db, model, id, kind);

			return model;
		}

		/// <summary>Complète le modèle avec les champs qui dépendent du type de média.</summary>
		private static async Task LoadSpecificFieldsAsync(MediaTechContext db, MediaEditModel model, int id, MediaKind kind)
		{
			switch (kind)
			{
				case MediaKind.Livre:
					var infosLivre = await db.Livres
						.AsNoTracking()
						.Where(livre => livre.Id == id)
						.Select(livre => new { livre.NombreDePages, livre.Resume })
						.FirstOrDefaultAsync();

					model.NombreDePages = infosLivre.NombreDePages;
					model.Resume = infosLivre.Resume;
					break;

				case MediaKind.Magazine:
					var infosMagazine = await db.Magazines
						.AsNoTracking()
						.Where(magazine => magazine.Id == id)
						.Select(magazine => new { magazine.NombreDePages, magazine.Numero })
						.FirstOrDefaultAsync();

					model.NombreDePages = infosMagazine.NombreDePages;
					model.Numero = infosMagazine.Numero;
					break;

				case MediaKind.DVD:
					var infosDvd = await db.DVDs
						.AsNoTracking()
						.Where(dvd => dvd.Id == id)
						.Select(dvd => new { dvd.Duree })
						.FirstOrDefaultAsync();

					model.Duree = infosDvd?.Duree;
					break;

				case MediaKind.CD:
					var infosCd = await db.CDs
						.AsNoTracking()
						.Where(cd => cd.Id == id)
						.Select(cd => new { cd.NombreDePiste })
						.FirstOrDefaultAsync();

					model.NombreDePiste = infosCd?.NombreDePiste;

					var pistesBrutes = await db.Pistes
						.AsNoTracking()
						.Where(piste => piste.IdMedia == id)
						.OrderBy(piste => piste.Numero)
						.Select(piste => new { piste.Titre, piste.Duree })
						.ToListAsync();

					model.Pistes = pistesBrutes
						.Select(piste => new PisteInput(piste.Titre, piste.Duree))
						.ToList();
					break;
			}
		}

		// =================================================================
		// Création / modification
		// Média + sous-type + genres + pistes sont enregistrés dans UNE seule transaction :
		// si une étape échoue, rien n'est enregistré.
		// =================================================================

		public static async Task<SaveResult> SaveMediaAsync(int? id, MediaEditModel model, bool notify)
		{
			RequireAdmin();

			await using var db = new MediaTechContext();
			await using var transaction = await db.Database.BeginTransactionAsync();

			bool estCreation = id is null;

			// 1. Éditeur et auteur : on réutilise ceux qui existent, sinon on les crée.
			Editeur editeur = await GetOrCreateEditeurAsync(db, model.Editeur);
			Auteur? auteur = await GetOrCreateAuteurAsync(db, model.Auteur);

			// 2. Média : nouvel objet (création) ou objet existant chargé depuis la base (modification).
			Media media = await CreateOrLoadMediaAsync(db, id, model.Kind);

			// 3. Champs communs à tous les médias.
			media.Titre = model.Titre.Trim();
			media.DateSortie = model.DateSortie;
			media.Stock = model.Stock;
			media.Editeur = editeur;
			media.Auteur = auteur;

			// 4. Genres (table de liaison Media_Genre).
			await UpdateGenresAsync(db, media, model.GenreIds);

			// 5. Champs propres au type de média.
			await ApplySpecificFieldsAsync(db, media, model, id, estCreation);

			// 6. Enregistrement en base puis validation de la transaction.
			await db.SaveChangesAsync();
			await transaction.CommitAsync();

			// 7. Notification (uniquement pour un nouveau média, si l'admin l'a demandée).
			string? avertissement = null;
			if (estCreation && notify)
			{
				avertissement = await TryPublishNotificationAsync(media);
			}

			return new SaveResult(media.Id, avertissement);
		}

		/// <summary>Retourne l'éditeur portant ce nom, ou en prépare un nouveau s'il n'existe pas.</summary>
		private static async Task<Editeur> GetOrCreateEditeurAsync(MediaTechContext db, string nomSaisi)
		{
			string nomEditeur = nomSaisi.Trim();

			Editeur? editeur = await db.Editeurs
				.FirstOrDefaultAsync(editeurExistant => editeurExistant.Nom == nomEditeur);

			if (editeur is null)
			{
				editeur = new Editeur { Nom = nomEditeur };
				db.Editeurs.Add(editeur);
			}

			return editeur;
		}

		/// <summary>
		/// Retourne l'auteur portant ce nom, ou en prépare un nouveau.
		/// Retourne null si aucun nom n'est saisi (l'auteur est facultatif).
		/// </summary>
		private static async Task<Auteur?> GetOrCreateAuteurAsync(MediaTechContext db, string? nomSaisi)
		{
			string? nomAuteur = nomSaisi?.Trim();

			if (string.IsNullOrEmpty(nomAuteur))
			{
				return null;
			}

			Auteur? auteur = await db.Auteurs
				.FirstOrDefaultAsync(auteurExistant => auteurExistant.Nom == nomAuteur);

			if (auteur is null)
			{
				auteur = new Auteur { Nom = nomAuteur };
				db.Auteurs.Add(auteur);
			}

			return auteur;
		}

		/// <summary>Crée un média du bon type (création) ou charge le média existant (modification).</summary>
		private static async Task<Media> CreateOrLoadMediaAsync(MediaTechContext db, int? id, MediaKind kind)
		{
			if (id is null)
			{
				Media nouveauMedia = kind switch
				{
					MediaKind.Livre => new Livre(),
					MediaKind.Magazine => new Magazine(),
					MediaKind.DVD => new DVD(),
					_ => new CD(),
				};

				db.Medias.Add(nouveauMedia);
				return nouveauMedia;
			}

			// Include : charge aussi la liste des genres du média.
			Media? mediaExistant = await db.Medias
				.Include(media => media.Genres)
				.FirstOrDefaultAsync(media => media.Id == id);

			if (mediaExistant is null)
			{
				throw new InvalidOperationException("Ce média n'existe plus.");
			}

			return mediaExistant;
		}

		/// <summary>Remplace les genres du média par ceux choisis dans le formulaire.</summary>
		private static async Task UpdateGenresAsync(MediaTechContext db, Media media, IEnumerable<int> genreIds)
		{
			List<int> idsGenresVoulus = genreIds.ToList();

			List<Genre> genresVoulus = await db.Genres
				.Where(genre => idsGenresVoulus.Contains(genre.Id))
				.ToListAsync();

			media.Genres.Clear();
			media.Genres.AddRange(genresVoulus);
		}

		/// <summary>Renseigne les champs qui dépendent du type réel du média.</summary>
		private static async Task ApplySpecificFieldsAsync(
			MediaTechContext db,
			Media media,
			MediaEditModel model,
			int? id,
			bool estCreation)
		{
			switch (media)
			{
				case Livre livre:
					livre.NombreDePages = model.NombreDePages;
					livre.Resume = string.IsNullOrWhiteSpace(model.Resume)
						? null
						: model.Resume.Trim();
					break;

				case Magazine magazine:
					magazine.NombreDePages = model.NombreDePages;
					magazine.Numero = model.Numero;
					break;

				case DVD dvd:
					dvd.Duree = model.Duree;
					break;

				case CD cd:
					await ApplyCdFieldsAsync(db, cd, model, id, estCreation);
					break;
			}
		}

		/// <summary>Renseigne le nombre de pistes et remplace toutes les pistes d'un CD.</summary>
		private static async Task ApplyCdFieldsAsync(
			MediaTechContext db,
			CD cd,
			MediaEditModel model,
			int? id,
			bool estCreation)
		{
			// Si le nombre de pistes n'est pas saisi, on le déduit de la liste des pistes.
			int? nombreDePistesDeduit = model.Pistes.Count > 0 ? model.Pistes.Count : null;
			cd.NombreDePiste = model.NombreDePiste ?? nombreDePistesDeduit;

			// En modification, on supprime d'abord les anciennes pistes (elles sont remplacées en bloc).
			if (!estCreation)
			{
				await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Piste WHERE IdMedia = {id}");
			}

			// Puis on ajoute les nouvelles pistes, numérotées à partir de 1.
			int numeroPiste = 1;
			foreach (PisteInput pisteSaisie in model.Pistes)
			{
				Piste nouvellePiste = new Piste
				{
					Titre = pisteSaisie.Titre,
					Duree = pisteSaisie.Duree,
					Numero = numeroPiste,
				};

				cd.Pistes.Add(nouvellePiste);
				numeroPiste++;
			}
		}

		/// <summary>
		/// Publie la notification globale d'un nouveau média.
		/// Aucune ligne NotificationUtilisateur n'est créée ici : la notification est commune à tous.
		/// Retourne un message d'avertissement en cas d'échec, ou null si tout s'est bien passé.
		/// </summary>
		private static async Task<string?> TryPublishNotificationAsync(Media media)
		{
			try
			{
				await NotificationService.PublishNewMediaAsync(media.Id, media.Titre);
				return null;
			}
			catch (Exception erreur)
			{
				// Le média est déjà enregistré : on ne fait pas échouer l'opération,
				// mais on prévient l'administrateur.
				Debug.WriteLine(erreur);
				return "Le média est enregistré, mais la notification n'a pas pu être publiée : "
					   + erreur.GetBaseException().Message;
			}
		}

		// =================================================================
		// Suppression
		// =================================================================

		public static async Task<(bool Success, string? Error)> DeleteMediaAsync(int id)
		{
			RequireAdmin();

			await using var db = new MediaTechContext();

			// La clé étrangère Emprunt.IdMedia est en ON DELETE RESTRICT :
			// on vérifie nous-mêmes pour afficher un message clair plutôt qu'une erreur MySQL.
			int nombreEmprunts = await db.Emprunts.CountAsync(emprunt => emprunt.IdMedia == id);

			if (nombreEmprunts > 0)
			{
				string texteEmprunts = CatalogueService.Plural(nombreEmprunts, "emprunt", "emprunts");

				return (false,
					$"Suppression impossible : {texteEmprunts} enregistré(s) pour ce média " +
					"(historique compris). Passez plutôt son stock à 0.");
			}

			// Les pistes, genres, notifications liées et tables filles
			// sont supprimés automatiquement (clés étrangères ON DELETE CASCADE).
			await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Media WHERE Id = {id}");

			NotificationService.NotifyChanged();

			return (true, null);
		}
	}
}