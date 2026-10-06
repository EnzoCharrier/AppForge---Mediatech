-- Comptes de test. Les mots de passe en clair sont automatiquement
-- re-hachés (PBKDF2) par l'application à la première connexion réussie.
USE MediaTech;
INSERT INTO Utilisateur (Username, Password, IsAdmin, Etat) VALUES
  ('admin',    'admin123',    TRUE,  'Actif'),
  ('camille',  'camille123',  FALSE, 'Actif'),
  ('suspendu', 'suspendu123', FALSE, 'Suspendu');
