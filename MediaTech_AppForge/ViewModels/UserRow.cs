using MediaTech_AppForge.Models;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
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
}
