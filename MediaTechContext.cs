using Microsoft.EntityFrameworkCore;
using MediaTech_AppForge.Models;

namespace MediaTech_AppForge.Data
{
    public class MediaTechContext : DbContext
    {
        public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
        public DbSet<Genre> Genres => Set<Genre>();
        public DbSet<Editeur> Editeurs => Set<Editeur>();
        public DbSet<Auteur> Auteurs => Set<Auteur>();
        public DbSet<Media> Medias => Set<Media>();
        public DbSet<Livre> Livres => Set<Livre>();
        public DbSet<Magazine> Magazines => Set<Magazine>();
        public DbSet<CD> CDs => Set<CD>();
        public DbSet<DVD> DVDs => Set<DVD>();
        public DbSet<Piste> Pistes => Set<Piste>();
        public DbSet<Emprunt> Emprunts => Set<Emprunt>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Adapter le user/mot de passe/port à ta config locale (XAMPP/WAMP/MySQL Workbench...).
            // Pomelo.EntityFrameworkCore.MySql doit être installé (voir README plus bas).
            const string connectionString =
                 "Server=172.16.119.144;Port=3306;Database=MediaTech;User=adminAppforge;Password=adminAppforge;";

            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ===== Noms de tables (singulier, comme dans le script SQL) =====
            modelBuilder.Entity<Utilisateur>().ToTable("Utilisateur");
            modelBuilder.Entity<Genre>().ToTable("Genre");
            modelBuilder.Entity<Editeur>().ToTable("Editeur");
            modelBuilder.Entity<Auteur>().ToTable("Auteur");
            modelBuilder.Entity<Media>().ToTable("Media");
            modelBuilder.Entity<Piste>().ToTable("Piste");
            modelBuilder.Entity<Emprunt>().ToTable("Emprunt");
            modelBuilder.Entity<Notification>().ToTable("Notification");
            modelBuilder.Entity<NotificationUtilisateur>().ToTable("NotificationUtilisateur");

            // ===== Héritage Media -> Livre/Magazine/CD/DVD en Table-Per-Type =====
            // La PK des tables filles s'appelle IdMedia dans le SQL (Id dans Media).
            modelBuilder.Entity<Livre>().ToTable("Livre", t => t.Property(l => l.Id).HasColumnName("IdMedia"));
            modelBuilder.Entity<Magazine>().ToTable("Magazine", t => t.Property(m => m.Id).HasColumnName("IdMedia"));
            modelBuilder.Entity<CD>().ToTable("CD", t => t.Property(c => c.Id).HasColumnName("IdMedia"));
            modelBuilder.Entity<DVD>().ToTable("DVD", t => t.Property(d => d.Id).HasColumnName("IdMedia"));

            // ===== Clés étrangères (noms IdXxx du script SQL, non reconnus par convention) =====
            modelBuilder.Entity<Media>()
                .HasOne(m => m.Editeur).WithMany(e => e.Medias)
                .HasForeignKey(m => m.IdEditeur).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Media>()
                .HasOne(m => m.Auteur).WithMany(a => a.Medias)
                .HasForeignKey(m => m.IdAuteur).OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Piste>()
                .HasOne(p => p.Media).WithMany(m => m.Pistes)
                .HasForeignKey(p => p.IdMedia).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Emprunt>()
                .HasOne(e => e.Utilisateur).WithMany(u => u.Emprunts)
                .HasForeignKey(e => e.IdUtilisateur).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Emprunt>()
                .HasOne(e => e.Media).WithMany(m => m.Emprunts)
                .HasForeignKey(e => e.IdMedia).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Media).WithMany()
                .HasForeignKey(n => n.IdMedia).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Emprunt).WithMany(e => e.Notifications)
                .HasForeignKey(n => n.IdEmprunt).OnDelete(DeleteBehavior.Cascade);

            // ===== Genre auto-associé (hiérarchique) =====
            modelBuilder.Entity<Genre>()
                .HasOne(g => g.GenreParent)
                .WithMany(g => g.SousGenres)
                .HasForeignKey(g => g.IdGenreParent)
                .OnDelete(DeleteBehavior.SetNull);

            // ===== Media <-> Genre (many-to-many via Media_Genre : IdMedia, IdGenre) =====
            modelBuilder.Entity<Media>()
                .HasMany(m => m.Genres)
                .WithMany(g => g.Medias)
                .UsingEntity<Dictionary<string, object>>(
                    "Media_Genre",
                    right => right.HasOne<Genre>().WithMany().HasForeignKey("IdGenre"),
                    left => left.HasOne<Media>().WithMany().HasForeignKey("IdMedia"),
                    j =>
                    {
                        j.ToTable("Media_Genre");
                        j.HasKey("IdMedia", "IdGenre");
                    });

            // ===== NotificationUtilisateur : clé composite =====
            modelBuilder.Entity<NotificationUtilisateur>()
                .HasKey(nu => new { nu.IdNotification, nu.IdUtilisateur });

            modelBuilder.Entity<NotificationUtilisateur>()
                .HasOne(nu => nu.Notification)
                .WithMany(n => n.Destinataires)
                .HasForeignKey(nu => nu.IdNotification);

            modelBuilder.Entity<NotificationUtilisateur>()
                .HasOne(nu => nu.Utilisateur)
                .WithMany(u => u.Notifications)
                .HasForeignKey(nu => nu.IdUtilisateur);

            // ===== Contraintes simples =====
            modelBuilder.Entity<Utilisateur>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Utilisateur>()
                .Property(u => u.Etat)
                .HasConversion<string>();
        }
    }
}