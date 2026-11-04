using System;
using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    public class Emprunt
    {
        public int Id { get; set; }
        public DateOnly DateEmprunt { get; set; }
        public DateOnly DateRetourPrevue { get; set; }
        public DateOnly? DateRetour { get; set; }
        public bool EstPhysique { get; set; }
    
        public int IdUtilisateur { get; set; }
        public Utilisateur Utilisateur { get; set; } = null!;
    
        public int IdMedia { get; set; }
        public Media Media { get; set; } = null!;
    
        public List<Notification> Notifications { get; set; } = new();
    }
}
