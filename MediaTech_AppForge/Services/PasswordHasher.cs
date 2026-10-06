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
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored, out bool needsUpgrade)
        {
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                // Ancien mot de passe en clair (données de départ) : comparaison, puis migration.
                needsUpgrade = true;
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(stored));
            }

            needsUpgrade = false;
            try
            {
                var parts = stored.Split('$');
                if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
