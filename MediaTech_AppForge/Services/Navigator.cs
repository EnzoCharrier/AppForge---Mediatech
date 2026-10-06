using System;
using System.Windows.Controls;

namespace MediaTech_AppForge.Services
{
    /// <summary>Permet à une page d'en afficher une autre dans la zone de contenu de MainWindow.</summary>
    public static class Navigator
    {
        public static Action<UserControl>? Handler { get; set; }
        public static void Show(UserControl page) => Handler?.Invoke(page);
    }
}
