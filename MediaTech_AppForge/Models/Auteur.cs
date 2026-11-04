using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    public class Auteur
    {
        public int Id { get; set; }
        public string Nom { get; set; } = null!;
    
        public List<Media> Medias { get; set; } = new();
    }
}
