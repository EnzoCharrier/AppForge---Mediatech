using System;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Services
{
    /// <summary>
    /// Utilisateur courant. CurrentUser == null => mode invité (consultation seule).
    /// </summary>
    public static class Session
    {
        public static Utilisateur? CurrentUser { get; private set; }
        public static bool IsLoggedIn => CurrentUser != null;
        public static bool IsAdmin => CurrentUser?.IsAdmin == true;

        public static event Action? Changed;

        public static void SignIn(Utilisateur user) { CurrentUser = user; Changed?.Invoke(); }
        public static void SignOut() { CurrentUser = null; Changed?.Invoke(); }
    }
}
