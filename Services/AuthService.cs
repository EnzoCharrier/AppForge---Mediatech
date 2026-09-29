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
            await using var db = new MediaTechContext();

            var user = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Username == username);

            // Même message si l'utilisateur n'existe pas ou si le mot de passe est faux.
            if (user is null || !PasswordHasher.Verify(password, user.Password, out var needsUpgrade))
                return (null, "Identifiant ou mot de passe incorrect.");

            if (user.Etat == EtatUtilisateur.Suspendu)
                return (null, "Votre compte est suspendu. Contactez la médiathèque.");
            if (user.Etat == EtatUtilisateur.Banni)
                return (null, "Votre compte a été banni.");

            if (needsUpgrade)
            {
                user.Password = PasswordHasher.Hash(password);
                await db.SaveChangesAsync();
            }

            return (user, null);
        }
    }
}
