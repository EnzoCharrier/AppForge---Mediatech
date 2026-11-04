using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Data;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    public static class AuthService
    {
        public static async Task<(Utilisateur? User, string? Error)> LoginAsync(string username, string password)
        {
            await using MediaTechContext db = new MediaTechContext();

            Utilisateur? utilisateur = await db.Utilisateurs.FirstOrDefaultAsync(utilisateurTrouve => utilisateurTrouve.Username == username);

            // Même message si l'utilisateur n'existe pas ou si le mot de passe est faux.
            if (utilisateur is null || !PasswordHasher.Verify(password, utilisateur.Password, out bool motDePasseAMigrer))
            {
                return (null, "Identifiant ou mot de passe incorrect.");
            }

            if (utilisateur.Etat == EtatUtilisateur.Suspendu)
            {
                return (null, "Votre compte est suspendu. Contactez la médiathèque.");
            }
            if (utilisateur.Etat == EtatUtilisateur.Banni)
            {
                return (null, "Votre compte a été banni.");
            }

            if (motDePasseAMigrer)
            {
                utilisateur.Password = PasswordHasher.Hash(password);
                await db.SaveChangesAsync();
            }

            return (utilisateur, null);
        }
    }
}
