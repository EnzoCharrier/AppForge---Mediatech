using System.Collections.Generic;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
    public class MediaDetail
    {
        public MediaKind Kind { get; set; }
        public string Titre { get; set; } = "";
        public string? Auteur { get; set; }
        public int Stock { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<FieldRow> Fields { get; set; } = new();
        public string? Resume { get; set; }
        public List<PisteRow> Pistes { get; set; } = new();
    }
}
