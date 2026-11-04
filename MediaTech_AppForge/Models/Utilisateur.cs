using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    public class Utilisateur
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool IsAdmin { get; set; }
        public EtatUtilisateur Etat { get; set; } = EtatUtilisateur.Actif;
    
        public List<Emprunt> Emprunts { get; set; } = new();
        public List<NotificationUtilisateur> Notifications { get; set; } = new();
    }
}
