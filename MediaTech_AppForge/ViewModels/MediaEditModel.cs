using System;
using System.Collections.Generic;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
    /// <summary>Données saisies dans le formulaire média (création ou modification).</summary>
    public class MediaEditModel
    {
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public DateOnly DateSortie { get; set; }
        public int Stock { get; set; }
        public string Editeur { get; set; } = "";
        public string? Auteur { get; set; }
        public List<int> GenreIds { get; set; } = new();

        // Spécifiques selon le type
        public int? NombreDePages { get; set; }   // Livre, Magazine
        public string? Resume { get; set; }       // Livre
        public int? Numero { get; set; }          // Magazine
        public TimeSpan? Duree { get; set; }      // DVD
        public int? NombreDePiste { get; set; }   // CD
        public List<PisteInput> Pistes { get; set; } = new(); // CD
    }
}
