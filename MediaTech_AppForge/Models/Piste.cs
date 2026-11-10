using System;

namespace MediaTech_AppForge.Models
{
    public class Piste
    {
        public int Id { get; set; }
        public string Titre { get; set; } = null!;
        public TimeSpan? Duree { get; set; }
        public int? Numero { get; set; }
    
        public int? IdMedia { get; set; }
        public Media? Media { get; set; }
    }
}
