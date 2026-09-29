using System;
using System.Collections.Generic;

namespace MediaTech_AppForge.Models
{
    public enum EtatUtilisateur { Actif, Suspendu, Banni }

    public class Utilisateur
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool IsAdmin { get; set; }
        public EtatUtilisateur Etat { get; set; } = EtatUtilisateur.Actif;

        public List<Emprunt> Emprunts { get; set; } = new();
        public List<NotificationUtilisateur> Notifications { get; set; } = new();
    }

    public class Genre
    {
        public int Id { get; set; }
        public string Libelle { get; set; } = null!;

        public int? IdGenreParent { get; set; }
        public Genre? GenreParent { get; set; }
        public List<Genre> SousGenres { get; set; } = new();

        public List<Media> Medias { get; set; } = new();
    }

    public class Editeur
    {
        public int Id { get; set; }
        public string Nom { get; set; } = null!;

        public List<Media> Medias { get; set; } = new();
    }

    public class Auteur
    {
        public int Id { get; set; }
        public string Nom { get; set; } = null!;

        public List<Media> Medias { get; set; } = new();
    }

    // ===================== MEDIA (base, table-per-type) =====================
    // Les colonnes ici sont communes à tous les sous-types. Chaque sous-type
    // (Livre, Magazine, CD, DVD) a sa propre table qui partage la même PK
    // (IdMedia). C'est la config Fluent API dans MediaTechContext qui active
    // le mode "table-per-type" — voir HasBaseType / ToTable sur chaque sous-type.
    public abstract class Media
    {
        public int Id { get; set; }
        public string Titre { get; set; } = null!;
        public DateOnly? DateSortie { get; set; }
        public int Stock { get; set; }

        public int IdEditeur { get; set; }
        public Editeur Editeur { get; set; } = null!;

        public int? IdAuteur { get; set; }
        public Auteur? Auteur { get; set; }

        public List<Genre> Genres { get; set; } = new();
        public List<Piste> Pistes { get; set; } = new();
        public List<Emprunt> Emprunts { get; set; } = new();
    }

    public class Livre : Media
    {
        public int? NombreDePages { get; set; }
        public string? Resume { get; set; }
    }

    public class Magazine : Media
    {
        public int? NombreDePages { get; set; }
        public int? Numero { get; set; }
    }

    public class CD : Media
    {
        public int? NombreDePiste { get; set; }
    }

    public class DVD : Media
    {
        public TimeSpan? Duree { get; set; }
    }

    public class Piste
    {
        public int Id { get; set; }
        public string Titre { get; set; } = null!;
        public TimeSpan? Duree { get; set; }
        public int? Numero { get; set; }

        public int? IdMedia { get; set; }
        public Media? Media { get; set; }
    }

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

    // Clé composite (IdNotification, IdUtilisateur) -> configurée en Fluent API
    public class NotificationUtilisateur
    {
        public int IdNotification { get; set; }
        public Notification Notification { get; set; } = null!;

        public int IdUtilisateur { get; set; }
        public Utilisateur Utilisateur { get; set; } = null!;

        public bool EstLue { get; set; }
    }
}
