using System;
using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    // ===================== MEDIA (base, table-per-type) =====================
    // Les colonnes ici sont communes à tous les sous-types. Chaque sous-type
    // (Livre, Magazine, CD, DVD) a sa propre table qui partage la même PK
    // (IdMedia). C'est la config Fluent API dans MediaTechContext qui active
    // le mode "table-per-type" — voir HasBaseType / ToTable sur chaque sous-type.
    public abstract class Media
    {
        public int Id { get; set; }
        public string Titre { get; set; } = null!;
        public DateOnly DateSortie { get; set; }
        public int Stock { get; set; }
    
        public int IdEditeur { get; set; }
        public Editeur Editeur { get; set; } = null!;
    
        public int? IdAuteur { get; set; }
        public Auteur? Auteur { get; set; }
    
        public List<Genre> Genres { get; set; } = new();
        public List<Piste> Pistes { get; set; } = new();
        public List<Emprunt> Emprunts { get; set; } = new();
    }
}
