using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    public class Genre
    {
        public int Id { get; set; }
        public string Libelle { get; set; } = null!;
    
        public int? IdGenreParent { get; set; }
        public Genre? GenreParent { get; set; }
        public List<Genre> SousGenres { get; set; } = new();
    
        public List<Media> Medias { get; set; } = new();
    }
}
