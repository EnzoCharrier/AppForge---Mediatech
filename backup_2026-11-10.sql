/*M!999999\- enable the sandbox mode */ 
-- MariaDB dump 10.19-11.8.3-MariaDB, for debian-linux-gnu (x86_64)
--
-- Host: localhost    Database: MediaTech
-- ------------------------------------------------------
-- Server version	11.8.3-MariaDB-0+deb13u1 from Debian

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*M!100616 SET @OLD_NOTE_VERBOSITY=@@NOTE_VERBOSITY, NOTE_VERBOSITY=0 */;

--
-- Table structure for table `Auteur`
--

DROP TABLE IF EXISTS `Auteur`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Auteur` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Nom` varchar(100) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Auteur`
--

LOCK TABLES `Auteur` WRITE;
/*!40000 ALTER TABLE `Auteur` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Auteur` VALUES
(1,'Frank Herbert'),
(2,'Daft Punk'),
(3,'Albert Camus'),
(4,'Marguerite Duras'),
(5,'Hergé'),
(7,'Christopher Nolan'),
(8,'As December Falls'),
(9,'Test');
/*!40000 ALTER TABLE `Auteur` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `CD`
--

DROP TABLE IF EXISTS `CD`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `CD` (
  `IdMedia` int(11) NOT NULL,
  `NombreDePiste` int(11) DEFAULT NULL,
  PRIMARY KEY (`IdMedia`),
  CONSTRAINT `fk_cd_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `CD`
--

LOCK TABLES `CD` WRITE;
/*!40000 ALTER TABLE `CD` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `CD` VALUES
(8,3),
(9,14);
/*!40000 ALTER TABLE `CD` ENABLE KEYS */;
UNLOCK TABLES;
commit;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_uca1400_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`adminAppforge`@`%`*/ /*!50003 TRIGGER trg_cd_check_partition
BEFORE INSERT ON CD
FOR EACH ROW
BEGIN
    IF EXISTS (SELECT 1 FROM Livre WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM Magazine WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM DVD WHERE IdMedia = NEW.IdMedia) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Ce Media possede deja un autre sous-type (violation du disjoint).';
    END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `DVD`
--

DROP TABLE IF EXISTS `DVD`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `DVD` (
  `IdMedia` int(11) NOT NULL,
  `Duree` time DEFAULT NULL,
  PRIMARY KEY (`IdMedia`),
  CONSTRAINT `fk_dvd_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `DVD`
--

LOCK TABLES `DVD` WRITE;
/*!40000 ALTER TABLE `DVD` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `DVD` VALUES
(7,'02:49:00');
/*!40000 ALTER TABLE `DVD` ENABLE KEYS */;
UNLOCK TABLES;
commit;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_uca1400_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`adminAppforge`@`%`*/ /*!50003 TRIGGER trg_dvd_check_partition
BEFORE INSERT ON DVD
FOR EACH ROW
BEGIN
    IF EXISTS (SELECT 1 FROM Livre WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM Magazine WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM CD WHERE IdMedia = NEW.IdMedia) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Ce Media possede deja un autre sous-type (violation du disjoint).';
    END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `Editeur`
--

DROP TABLE IF EXISTS `Editeur`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Editeur` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Nom` varchar(100) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Editeur`
--

LOCK TABLES `Editeur` WRITE;
/*!40000 ALTER TABLE `Editeur` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Editeur` VALUES
(1,'Gallimard'),
(2,'Universal Music'),
(4,'Casterman'),
(5,'Parlophone'),
(6,'Universal Pictures'),
(7,'Le Monde SA'),
(8,'ADF Records'),
(9,'Test');
/*!40000 ALTER TABLE `Editeur` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `Emprunt`
--

DROP TABLE IF EXISTS `Emprunt`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Emprunt` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `DateEmprunt` date NOT NULL,
  `DateRetourPrevue` date NOT NULL,
  `DateRetour` date DEFAULT NULL,
  `EstPhysique` tinyint(1) NOT NULL DEFAULT 0,
  `IdUtilisateur` int(11) NOT NULL,
  `IdMedia` int(11) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_emprunt_media` (`IdMedia`),
  KEY `idx_emprunt_user` (`IdUtilisateur`),
  CONSTRAINT `fk_emprunt_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON UPDATE CASCADE,
  CONSTRAINT `fk_emprunt_utilisateur` FOREIGN KEY (`IdUtilisateur`) REFERENCES `Utilisateur` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Emprunt`
--

LOCK TABLES `Emprunt` WRITE;
/*!40000 ALTER TABLE `Emprunt` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Emprunt` VALUES
(1,'2026-09-29','2026-10-13','2026-09-29',0,11,1),
(2,'2026-09-29','2026-10-13','2026-09-29',0,11,4),
(3,'2026-10-13','2026-10-27','2026-10-13',0,11,1),
(4,'2026-11-04','2026-11-18','2026-11-04',0,11,3),
(5,'2026-11-04','2026-11-18',NULL,1,11,1);
/*!40000 ALTER TABLE `Emprunt` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `Genre`
--

DROP TABLE IF EXISTS `Genre`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Genre` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Libelle` varchar(100) NOT NULL,
  `IdGenreParent` int(11) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_genre_parent` (`IdGenreParent`),
  CONSTRAINT `fk_genre_parent` FOREIGN KEY (`IdGenreParent`) REFERENCES `Genre` (`Id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=12 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Genre`
--

LOCK TABLES `Genre` WRITE;
/*!40000 ALTER TABLE `Genre` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Genre` VALUES
(1,'Fiction',NULL),
(2,'Musique',NULL),
(3,'Science-Fiction',1),
(4,'Littérature',NULL),
(5,'Bande dessinée',NULL),
(7,'Cinéma',NULL),
(8,'Actualité',NULL),
(9,'Roman',4),
(10,'Électro',2),
(11,'Science-fiction',7);
/*!40000 ALTER TABLE `Genre` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `Livre`
--

DROP TABLE IF EXISTS `Livre`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Livre` (
  `IdMedia` int(11) NOT NULL,
  `NombreDePages` int(11) DEFAULT NULL,
  `Resume` text DEFAULT NULL,
  PRIMARY KEY (`IdMedia`),
  CONSTRAINT `fk_livre_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Livre`
--

LOCK TABLES `Livre` WRITE;
/*!40000 ALTER TABLE `Livre` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Livre` VALUES
(1,412,'Sur la planète désertique Arrakis, Paul Atréides doit survivre aux intrigues politiques et écologiques.'),
(3,186,'Meursault, employé à Alger, assiste à l\'enterrement de sa mère puis commet un meurtre sur une plage baignée de soleil.'),
(4,142,NULL),
(5,62,NULL),
(10,1,'C\'est un test');
/*!40000 ALTER TABLE `Livre` ENABLE KEYS */;
UNLOCK TABLES;
commit;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_uca1400_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`adminAppforge`@`%`*/ /*!50003 TRIGGER trg_livre_check_partition
BEFORE INSERT ON Livre
FOR EACH ROW
BEGIN
    IF EXISTS (SELECT 1 FROM Magazine WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM CD WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM DVD WHERE IdMedia = NEW.IdMedia) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Ce Media possede deja un autre sous-type (violation du disjoint).';
    END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `Magazine`
--

DROP TABLE IF EXISTS `Magazine`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Magazine` (
  `IdMedia` int(11) NOT NULL,
  `NombreDePages` int(11) DEFAULT NULL,
  `Numero` int(11) DEFAULT NULL,
  PRIMARY KEY (`IdMedia`),
  CONSTRAINT `fk_magazine_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Magazine`
--

LOCK TABLES `Magazine` WRITE;
/*!40000 ALTER TABLE `Magazine` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Magazine` VALUES
(6,98,42);
/*!40000 ALTER TABLE `Magazine` ENABLE KEYS */;
UNLOCK TABLES;
commit;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_uca1400_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`adminAppforge`@`%`*/ /*!50003 TRIGGER trg_magazine_check_partition
BEFORE INSERT ON Magazine
FOR EACH ROW
BEGIN
    IF EXISTS (SELECT 1 FROM Livre WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM CD WHERE IdMedia = NEW.IdMedia)
       OR EXISTS (SELECT 1 FROM DVD WHERE IdMedia = NEW.IdMedia) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Ce Media possede deja un autre sous-type (violation du disjoint).';
    END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `Media`
--

DROP TABLE IF EXISTS `Media`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Media` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Titre` varchar(150) NOT NULL,
  `DateSortie` date DEFAULT NULL,
  `Stock` int(11) NOT NULL DEFAULT 0,
  `IdEditeur` int(11) NOT NULL,
  `IdAuteur` int(11) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_media_editeur` (`IdEditeur`),
  KEY `idx_media_auteur` (`IdAuteur`),
  CONSTRAINT `fk_media_auteur` FOREIGN KEY (`IdAuteur`) REFERENCES `Auteur` (`Id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_media_editeur` FOREIGN KEY (`IdEditeur`) REFERENCES `Editeur` (`Id`) ON UPDATE CASCADE,
  CONSTRAINT `chk_media_stock` CHECK (`Stock` >= 0)
) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Media`
--

LOCK TABLES `Media` WRITE;
/*!40000 ALTER TABLE `Media` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Media` VALUES
(1,'Dune','1965-08-01',2,1,1),
(2,'Discovery','2001-03-12',2,2,2),
(3,'L\'Étranger','1942-05-19',3,1,3),
(4,'L\'Amant','1984-09-13',0,1,4),
(5,'Tintin au Tibet','1960-01-01',2,4,5),
(6,'Le Monde – Hors-série','2025-03-01',4,7,NULL),
(7,'Interstellar','2014-11-05',1,6,7),
(8,'Discovery','2001-03-12',2,5,2),
(9,'Everything\'s On Fire But I\'m Fine','2025-08-08',3,8,8),
(10,'Test','2000-01-01',1,9,9);
/*!40000 ALTER TABLE `Media` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `Media_Genre`
--

DROP TABLE IF EXISTS `Media_Genre`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Media_Genre` (
  `IdMedia` int(11) NOT NULL,
  `IdGenre` int(11) NOT NULL,
  PRIMARY KEY (`IdMedia`,`IdGenre`),
  KEY `fk_mediagenre_genre` (`IdGenre`),
  CONSTRAINT `fk_mediagenre_genre` FOREIGN KEY (`IdGenre`) REFERENCES `Genre` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_mediagenre_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Media_Genre`
--

LOCK TABLES `Media_Genre` WRITE;
/*!40000 ALTER TABLE `Media_Genre` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Media_Genre` VALUES
(10,1),
(2,2),
(9,2),
(1,3),
(7,3),
(5,5),
(6,8),
(3,9),
(4,9),
(8,10);
/*!40000 ALTER TABLE `Media_Genre` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `Notification`
--

DROP TABLE IF EXISTS `Notification`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Notification` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Type` varchar(50) NOT NULL,
  `Message` varchar(255) NOT NULL,
  `DateEnvoi` datetime NOT NULL DEFAULT current_timestamp(),
  `EstGlobale` tinyint(1) NOT NULL DEFAULT 0,
  `IdMedia` int(11) DEFAULT NULL,
  `IdEmprunt` int(11) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_notification_media` (`IdMedia`),
  KEY `idx_notification_emprunt` (`IdEmprunt`),
  CONSTRAINT `fk_notification_emprunt` FOREIGN KEY (`IdEmprunt`) REFERENCES `Emprunt` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_notification_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Notification`
--

LOCK TABLES `Notification` WRITE;
/*!40000 ALTER TABLE `Notification` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Notification` VALUES
(1,'NouveauMedia','Nouveau : « Interstellar » est disponible au catalogue.','2026-09-29 12:11:58',1,7,NULL),
(2,'NouveauMedia','Nouveau : « Discovery » est disponible au catalogue.','2026-09-28 14:11:58',1,2,NULL),
(3,'NouveauMedia','Nouveau : « Test » est disponible au catalogue.','2026-10-12 09:47:26',1,10,NULL);
/*!40000 ALTER TABLE `Notification` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `NotificationUtilisateur`
--

DROP TABLE IF EXISTS `NotificationUtilisateur`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `NotificationUtilisateur` (
  `IdNotification` int(11) NOT NULL,
  `IdUtilisateur` int(11) NOT NULL,
  `EstLue` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`IdNotification`,`IdUtilisateur`),
  KEY `fk_notifutilisateur_utilisateur` (`IdUtilisateur`),
  CONSTRAINT `fk_notifutilisateur_notification` FOREIGN KEY (`IdNotification`) REFERENCES `Notification` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_notifutilisateur_utilisateur` FOREIGN KEY (`IdUtilisateur`) REFERENCES `Utilisateur` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `NotificationUtilisateur`
--

LOCK TABLES `NotificationUtilisateur` WRITE;
/*!40000 ALTER TABLE `NotificationUtilisateur` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `NotificationUtilisateur` VALUES
(1,10,1),
(1,11,1),
(2,10,1),
(2,11,1),
(3,10,1),
(3,11,1);
/*!40000 ALTER TABLE `NotificationUtilisateur` ENABLE KEYS */;
UNLOCK TABLES;
commit;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_uca1400_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`adminAppforge`@`%`*/ /*!50003 TRIGGER trg_notifutilisateur_check_globale
BEFORE INSERT ON NotificationUtilisateur
FOR EACH ROW
BEGIN
    DECLARE v_est_globale BOOLEAN;

    SELECT EstGlobale INTO v_est_globale
    FROM Notification
    WHERE Id = NEW.IdNotification;

    IF v_est_globale = TRUE AND NEW.EstLue = FALSE THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Une ligne NotificationUtilisateur pour une notification globale doit être créée avec EstLue = TRUE.';
    END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `Piste`
--

DROP TABLE IF EXISTS `Piste`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Piste` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Titre` varchar(150) NOT NULL,
  `Duree` time DEFAULT NULL,
  `Numero` int(11) DEFAULT NULL,
  `IdMedia` int(11) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_piste_media` (`IdMedia`),
  CONSTRAINT `fk_piste_media` FOREIGN KEY (`IdMedia`) REFERENCES `Media` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Piste`
--

LOCK TABLES `Piste` WRITE;
/*!40000 ALTER TABLE `Piste` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Piste` VALUES
(1,'One More Time','00:05:20',1,8),
(2,'Aerodynamic','00:03:27',2,8),
(3,'Digital Love','00:04:58',3,8);
/*!40000 ALTER TABLE `Piste` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `Utilisateur`
--

DROP TABLE IF EXISTS `Utilisateur`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `Utilisateur` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Username` varchar(50) NOT NULL,
  `Password` varchar(255) NOT NULL,
  `IsAdmin` tinyint(1) NOT NULL DEFAULT 0,
  `Etat` enum('Actif','Suspendu','Banni') NOT NULL DEFAULT 'Actif',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uq_utilisateur_username` (`Username`)
) ENGINE=InnoDB AUTO_INCREMENT=13 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Utilisateur`
--

LOCK TABLES `Utilisateur` WRITE;
/*!40000 ALTER TABLE `Utilisateur` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `Utilisateur` VALUES
(10,'admin','pbkdf2$100000$Nhc6Lgb976ihETq7EW7L0g==$h415q9+s2BBmUZJsHZr0vzyOqbdUCZMujulU4A9goHk=',1,'Actif'),
(11,'camille','pbkdf2$100000$dq6RO155A4ALnzN4u1Yfhg==$YaO1ts9PHensIouLZ8rBRhl0kEKNYcrRi7zNzxyjHpk=',0,'Actif'),
(12,'suspendu','pbkdf2$100000$pQQ6gogdf8sHgiWsFzFlGQ==$JSP47AXV/eKTVJsjX7gSiHfVS+HHp26+cg9SUu7oWTU=',0,'Suspendu');
/*!40000 ALTER TABLE `Utilisateur` ENABLE KEYS */;
UNLOCK TABLES;
commit;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*M!100616 SET NOTE_VERBOSITY=@OLD_NOTE_VERBOSITY */;

-- Dump completed on 2026-11-10 14:54:21
