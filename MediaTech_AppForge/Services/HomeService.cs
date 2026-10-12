using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;

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

            int total = await db.Medias.CountAsync();

            // Le schéma n'a pas de date d'ajout : "dernier ajouté" = identifiant le plus élevé.
            var raw = await db.Medias.AsNoTracking()
                .OrderByDescending(m => m.Id).Take(ShowcaseCount)
                .Select(m => new
                {
                    m.Id, m.Titre, m.Stock,
                    Auteur = m.Auteur != null ? m.Auteur.Nom : null,
                    Editeur = m.Editeur.Nom,
                }).ToListAsync();

            var kinds = await EmpruntService.KindsOfAsync(db, raw.Select(r => r.Id).ToList());

            var latest = raw.Select(r => new MediaRow
            {
                Id = r.Id,
                Kind = kinds.TryGetValue(r.Id, out var k) ? k : MediaKind.Livre,
                Titre = r.Titre,
                Auteur = r.Auteur ?? "",
                Editeur = r.Editeur,
                Stock = r.Stock,
            }).ToList();

            return new Showcase(total, latest);
        }
    }
}
