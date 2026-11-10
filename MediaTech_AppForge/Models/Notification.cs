using System;
using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string Type { get; set; } = null!;
        public string Message { get; set; } = null!;
        public DateTime DateEnvoi { get; set; } = DateTime.Now;
        public bool EstGlobale { get; set; }
    
        public int? IdMedia { get; set; }
        public Media? Media { get; set; }
    
        public int? IdEmprunt { get; set; }
        public Emprunt? Emprunt { get; set; }
    
        public List<NotificationUtilisateur> Destinataires { get; set; } = new();
    }
}
