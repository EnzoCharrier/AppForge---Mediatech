using System;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
    /// <summary>Une ligne de la page "Mes emprunts".</summary>
    public class EmpruntRow
    {
        public int EmpruntId { get; set; }
        public int MediaId { get; set; }
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string DueText { get; set; } = "";
        public bool IsLate { get; set; }
        public bool IsActive { get; set; }
        public DateOnly DateRetourPrevue { get; set; }
        public DateOnly? DateRetour { get; set; }
    }
}
