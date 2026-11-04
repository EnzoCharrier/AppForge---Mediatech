namespace MediaTech_AppForge.Models
{
    // Clé composite (IdNotification, IdUtilisateur) -> configurée en Fluent API
    public class NotificationUtilisateur
    {
        public int IdNotification { get; set; }
        public Notification Notification { get; set; } = null!;
    
        public int IdUtilisateur { get; set; }
        public Utilisateur Utilisateur { get; set; } = null!;
    
        public bool EstLue { get; set; }
    }
}
