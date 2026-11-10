using System;
using System.Security.Cryptography;
using System.Text;

namespace MediaTech_AppForge.Services
{
    /// <summary>
    /// Hash PBKDF2-SHA256. Format stocké : pbkdf2$iterations$sel$hash (tient dans VARCHAR(255)).
    /// Les anciens mots de passe en clair sont acceptés UNE fois puis re-hachés (needsUpgrade).
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "pbkdf2$";
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            byte[] sel = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, sel, Iterations, HashAlgorithmName.SHA256, 32);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(sel)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored, out bool needsUpgrade)
        {
            bool estAncienMotDePasseEnClair = !stored.StartsWith(Prefix, StringComparison.Ordinal);
            needsUpgrade = estAncienMotDePasseEnClair;

            // Ancien mot de passe en clair (données de départ) : comparaison, puis migration.
            if (estAncienMotDePasseEnClair)
            {
                return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(stored));
            }

            try
            {
                string[] morceaux = stored.Split('$'); // pbkdf2 | iterations | sel | hash
                if (morceaux.Length != 4 || !int.TryParse(morceaux[1], out int nbIterations))
                {
                    return false;
                }

                byte[] sel = Convert.FromBase64String(morceaux[2]);
                byte[] hashAttendu = Convert.FromBase64String(morceaux[3]);
                byte[] hashCalcule = Rfc2898DeriveBytes.Pbkdf2(password, sel, nbIterations, HashAlgorithmName.SHA256, hashAttendu.Length);
                return CryptographicOperations.FixedTimeEquals(hashCalcule, hashAttendu);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
