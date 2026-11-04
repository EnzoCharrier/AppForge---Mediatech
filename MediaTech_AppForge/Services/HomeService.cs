using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.ViewModels;

namespace MediaTech_AppForge.Services
{
    public record Showcase(int Total, List<MediaRow> Latest);

    public static class HomeService
    {
        public const int ShowcaseCount = 5;

        /// <summary>Nombre de titres et derniers médias ajoutés, pour la vitrine de l'accueil.</summary>
        public static async Task<Showcase> LoadShowcaseAsync()
        {
            await using var db = new MediaTechContext();

            int nbMediasTotal = await db.Medias.CountAsync();

            // Le schéma n'a pas de date d'ajout : "dernier ajouté" = identifiant le plus élevé.
            var derniersMediasBruts = await db.Medias.AsNoTracking()
                .OrderByDescending(media => media.Id).Take(ShowcaseCount)
                .Select(media => new
                {
                    media.Id, media.Titre, media.Stock,
                    Auteur = media.Auteur != null ? media.Auteur.Nom : null,
                    Editeur = media.Editeur.Nom,
                }).ToListAsync();

            var typesParIdMedia = await EmpruntService.KindsOfAsync(db, derniersMediasBruts.Select(media => media.Id).ToList());

            var derniersMedias = derniersMediasBruts.Select(media => new MediaRow
            {
                Id = media.Id,
                Kind = typesParIdMedia.TryGetValue(media.Id, out var typeTrouve) ? typeTrouve : MediaKind.Livre,
                Titre = media.Titre,
                Auteur = media.Auteur ?? "",
                Editeur = media.Editeur,
                Stock = media.Stock,
            }).ToList();

            return new Showcase(nbMediasTotal, derniersMedias);
        }
    }
}
