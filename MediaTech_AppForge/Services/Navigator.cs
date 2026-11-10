using System;
using System.Windows.Controls;

namespace MediaTech_AppForge.Services
{
    public enum NavTarget { Accueil, Catalogue, Emprunts, Notifications, Gestion }

    /// <summary>Permet à une page d'en afficher une autre, ou d'aller à une section de la sidebar.</summary>
    public static class Navigator
    {
        /// <summary>Affiche une page dans la zone de contenu (la sidebar ne change pas).</summary>
        public static Action<UserControl>? Handler { get; set; }
        public static void Show(UserControl page) => Handler?.Invoke(page);

        /// <summary>Va à une section et met la sidebar à jour.</summary>
        public static Action<NavTarget>? SectionHandler { get; set; }
        public static void GoTo(NavTarget target) => SectionHandler?.Invoke(target);
    }
}
