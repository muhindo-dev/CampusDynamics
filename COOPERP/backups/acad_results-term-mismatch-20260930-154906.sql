-- The three acad_results rows realigned to their registration's term, 30 Sep 2026.
-- 561556 MRU2023001191 BEF2102 (restored after being deleted in error, then aligned)
-- 673154 MRU2023000407 BCE4202 (2025/2026 sem 1 -> sem 2)
-- 559000 MRU2024000046 PRC1101B (2024/2025 sem 1 -> 2023/2024 sem 1)

-- MariaDB dump 10.19  Distrib 10.4.32-MariaDB, for Win64 (AMD64)
--
-- Host: localhost    Database: campus_dynamics
-- ------------------------------------------------------
-- Server version	5.6.43-log

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Dumping data for table `acad_results`
--
-- WHERE:  ID IN (561556,673154,559000)

LOCK TABLES `acad_results` WRITE;
/*!40000 ALTER TABLE `acad_results` DISABLE KEYS */;
INSERT INTO `acad_results` (`ID`, `regno`, `courseid`, `semester`, `acad`, `studyyear`, `score`, `grade`, `gradept`, `gpa`, `result_comment`, `CreditUnits`, `progid`, `is_retake`) VALUES (559000,'MRU2024000046','PRC1101B',1,'2024/2025',1,78,'B+',4.5,4.50,'Published from provisional marks by muhindo [Overwrite: was 0/F]',3,'BPLM',0),(561556,'MRU2023001191','BEF2102',1,'2024/2025',2,78,'B+',4.5,4.13,'Restored 2026-09-30: deleted in error when a duplicate registration was removed',3,'BEICT',0),(673154,'MRU2023000407','BCE4202',1,'2025/2026',4,71,'B',4,3.62,'Published from provisional marks by Dr. Musisi [Overwrite: was 70/B]',4,NULL,0);
/*!40000 ALTER TABLE `acad_results` ENABLE KEYS */;
UNLOCK TABLES;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = cp850 */ ;
/*!50003 SET character_set_results = cp850 */ ;
/*!50003 SET collation_connection  = cp850_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER trg_acad_results_audit_ai AFTER INSERT ON acad_results FOR EACH ROW
BEGIN
  DECLARE v_actor VARCHAR(90);  DECLARE v_source VARCHAR(100);
  DECLARE v_reason VARCHAR(200); DECLARE v_ip VARCHAR(45);
  DECLARE CONTINUE HANDLER FOR NOT FOUND BEGIN END;
  SELECT actor, source, reason, ip INTO v_actor, v_source, v_reason, v_ip
    FROM mark_audit_context WHERE conn_id = CONNECTION_ID() AND set_at >= (NOW() - INTERVAL 60 SECOND) LIMIT 1;
  INSERT INTO acad_marks_audit
    (action_type, performed_by, ip_address, target_table, target_id, regno, course_id, acad_year, semester,
     field_changed, new_value, new_total, new_grade, change_reason, source_page, created_at)
  VALUES
    ('INSERT', IFNULL(NULLIF(TRIM(v_actor),''),'system'), NULLIF(TRIM(v_ip),''),
     'acad_results', IFNULL(NEW.ID,0), IFNULL(NEW.regno,''), IFNULL(NEW.courseid,''), NEW.acad, NEW.semester,
     'result', CONCAT('score=',IFNULL(NEW.score,'-'),' grade=',IFNULL(NEW.grade,'-')),
     NEW.score, NEW.grade, NULLIF(TRIM(v_reason),''),
     IFNULL(NULLIF(TRIM(v_source),''),'db-trigger'), NOW());
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = cp850 */ ;
/*!50003 SET character_set_results = cp850 */ ;
/*!50003 SET collation_connection  = cp850_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER trg_acad_results_audit_au AFTER UPDATE ON acad_results FOR EACH ROW
BEGIN
  DECLARE v_actor VARCHAR(90);  DECLARE v_source VARCHAR(100);
  DECLARE v_reason VARCHAR(200); DECLARE v_ip VARCHAR(45);
  DECLARE CONTINUE HANDLER FOR NOT FOUND BEGIN END;
  IF (NOT (OLD.score <=> NEW.score)) OR (NOT (OLD.grade <=> NEW.grade)) THEN
    SELECT actor, source, reason, ip INTO v_actor, v_source, v_reason, v_ip
      FROM mark_audit_context WHERE conn_id = CONNECTION_ID() AND set_at >= (NOW() - INTERVAL 60 SECOND) LIMIT 1;
    INSERT INTO acad_marks_audit
      (action_type, performed_by, ip_address, target_table, target_id, regno, course_id, acad_year, semester,
       field_changed, old_value, new_value, old_total, new_total, old_grade, new_grade, change_reason, source_page, created_at)
    VALUES
      ('UPDATE', IFNULL(NULLIF(TRIM(v_actor),''),'system'), NULLIF(TRIM(v_ip),''),
       'acad_results', IFNULL(NEW.ID,0), IFNULL(NEW.regno,''), IFNULL(NEW.courseid,''), NEW.acad, NEW.semester,
       'result', CONCAT('score=',IFNULL(OLD.score,'-'),' grade=',IFNULL(OLD.grade,'-')),
       CONCAT('score=',IFNULL(NEW.score,'-'),' grade=',IFNULL(NEW.grade,'-')),
       OLD.score, NEW.score, OLD.grade, NEW.grade, NULLIF(TRIM(v_reason),''),
       IFNULL(NULLIF(TRIM(v_source),''),'db-trigger'), NOW());
  END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = cp850 */ ;
/*!50003 SET character_set_results = cp850 */ ;
/*!50003 SET collation_connection  = cp850_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER trg_acad_results_audit_ad AFTER DELETE ON acad_results FOR EACH ROW
BEGIN
  DECLARE v_actor VARCHAR(90);  DECLARE v_source VARCHAR(100);
  DECLARE v_reason VARCHAR(200); DECLARE v_ip VARCHAR(45);
  DECLARE CONTINUE HANDLER FOR NOT FOUND BEGIN END;
  SELECT actor, source, reason, ip INTO v_actor, v_source, v_reason, v_ip
    FROM mark_audit_context WHERE conn_id = CONNECTION_ID() AND set_at >= (NOW() - INTERVAL 60 SECOND) LIMIT 1;
  INSERT INTO acad_marks_audit
    (action_type, performed_by, ip_address, target_table, target_id, regno, course_id, acad_year, semester,
     field_changed, old_value, old_total, old_grade, change_reason, source_page, created_at)
  VALUES
    ('DELETE', IFNULL(NULLIF(TRIM(v_actor),''),'system'), NULLIF(TRIM(v_ip),''),
     'acad_results', IFNULL(OLD.ID,0), IFNULL(OLD.regno,''), IFNULL(OLD.courseid,''), OLD.acad, OLD.semester,
     'result', CONCAT('score=',IFNULL(OLD.score,'-'),' grade=',IFNULL(OLD.grade,'-')),
     OLD.score, OLD.grade, NULLIF(TRIM(v_reason),''),
     IFNULL(NULLIF(TRIM(v_source),''),'db-trigger'), NOW());
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-09-30 15:49:06
