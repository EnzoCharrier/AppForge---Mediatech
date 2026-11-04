using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
    /// <summary>Une ligne de la liste Auteurs / Éditeurs / Genres.</summary>
    public class RefRow
    {
        public int Id { get; set; }
        public string Nom { get; set; } = "";
        public string? ParentNom { get; set; }   // genres uniquement
        public int Usage { get; set; }           // nombre de médias liés

        public string Detail => ParentNom is null ? "" : $"Sous-genre de {ParentNom}";
        public string UsageText => Usage == 0 ? "Aucun média" : CatalogueService.Plural(Usage, "média", "médias");
    }
}
