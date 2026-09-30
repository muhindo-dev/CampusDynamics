-- The 18 fin_ledger rows of the nine duplicate postings of one Centenary deposit
-- (MRU2024000688, UGX 1,455,000, ref 041717454181-BOA-130447, deposited 17/04/2026).
-- Voucher 116522 (TIDs 215553/215554) was KEPT; the other eight pairs were deleted.
-- Taken 2026-09-30 11:54:27 UTC from campus_dynamics_accounts.

-- MariaDB dump 10.19  Distrib 10.4.32-MariaDB, for Win64 (AMD64)
--
-- Host: localhost    Database: campus_dynamics_accounts
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
-- Dumping data for table `fin_ledger`
--
-- WHERE:  particulars LIKE '%Kasumba Simon on 17/04/2026%'

LOCK TABLES `fin_ledger` WRITE;
/*!40000 ALTER TABLE `fin_ledger` DISABLE KEYS */;
INSERT INTO `fin_ledger` (`TID`, `accountcode`, `account_type`, `transactionType`, `transaction_amount`, `particulars`, `voucherNo`, `RefNo`, `transactionDate`, `teller`, `timeLog`, `folio`, `tracking_ref`, `source_system`, `journal_no`, `trans_currency`, `actual_amount`, `curr_balance`, `forex_rate`, `ugx_amount`, `InvoiceDate`) VALUES (215553,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116522,'137','2026-05-19','sharifah','2026-05-19 12:48:53','12439',NULL,NULL,'00003','UGX',1455000,'0DR',1,1455000,NULL),(215554,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116522,'137','2026-05-19','sharifah','2026-05-19 12:48:53','12439',NULL,NULL,'06198','UGX',1455000,'255,651,393DR',1,1455000,NULL),(215609,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116549,'137','2026-05-19','autocapture','2026-05-19 13:30:23','12439',NULL,NULL,'00003','UGX',1455000,'1,455,000CR',1,1455000,NULL),(215610,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116549,'137','2026-05-19','autocapture','2026-05-19 13:30:23','12439',NULL,NULL,'06189','UGX',1455000,'257,106,393DR',1,1455000,NULL),(215677,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116575,'137','2026-05-19','sharifah','2026-05-19 14:15:31','12439',NULL,NULL,'00004','UGX',1455000,'2,910,000CR',1,1455000,NULL),(215678,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116575,'137','2026-05-19','sharifah','2026-05-19 14:15:31','12439',NULL,NULL,'06199','UGX',1455000,'258,561,393DR',1,1455000,NULL),(215829,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116626,'137','2026-05-19','autocapture','2026-05-19 16:04:07','12439',NULL,NULL,'00005','UGX',1455000,'4,365,000CR',1,1455000,NULL),(215830,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116626,'137','2026-05-19','autocapture','2026-05-19 16:04:07','12439',NULL,NULL,'06200','UGX',1455000,'260,016,393DR',1,1455000,NULL),(215831,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116627,'137','2026-05-19','autocapture','2026-05-19 16:04:43','12439',NULL,NULL,'00006','UGX',1455000,'5,820,000CR',1,1455000,NULL),(215832,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116627,'137','2026-05-19','autocapture','2026-05-19 16:04:43','12439',NULL,NULL,'06201','UGX',1455000,'261,471,393DR',1,1455000,NULL),(215833,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116628,'137','2026-05-19','autocapture','2026-05-19 16:05:14','12439',NULL,NULL,'00007','UGX',1455000,'7,275,000CR',1,1455000,NULL),(215834,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116628,'137','2026-05-19','autocapture','2026-05-19 16:05:14','12439',NULL,NULL,'06202','UGX',1455000,'262,926,393DR',1,1455000,NULL),(215835,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116629,'137','2026-05-19','autocapture','2026-05-19 16:05:28','12439',NULL,NULL,'00008','UGX',1455000,'8,730,000CR',1,1455000,NULL),(215836,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116629,'137','2026-05-19','autocapture','2026-05-19 16:05:28','12439',NULL,NULL,'06203','UGX',1455000,'264,381,393DR',1,1455000,NULL),(215846,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116634,'137','2026-05-19','autocapture','2026-05-19 16:27:35','12439',NULL,NULL,'00004','UGX',1455000,'10,185,000CR',1,1455000,NULL),(215847,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116634,'137','2026-05-19','autocapture','2026-05-19 16:27:35','12439',NULL,NULL,'06189','UGX',1455000,'265,836,393DR',1,1455000,NULL),(216614,'MRU2024000688','FSTEADFees','CR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116956,'137','2026-05-20','sharifah','2026-05-20 13:36:12','12439',NULL,NULL,'00009','UGX',1455000,'11,640,000CR',1,1455000,NULL),(216615,'AC1303','Chart Account','DR',1455000,'Centenary deposit 041717454181-BOA-130447 IFO Kasumba Simon on 17/04/2026',116956,'137','2026-05-20','sharifah','2026-05-20 13:36:12','12439',NULL,NULL,'06190','UGX',1455000,'267,291,393DR',1,1455000,NULL);
/*!40000 ALTER TABLE `fin_ledger` ENABLE KEYS */;
UNLOCK TABLES;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER campus_dynamics_accounts.trg_fin_ledger_before_insert
BEFORE INSERT ON campus_dynamics_accounts.fin_ledger
FOR EACH ROW
BEGIN
    IF NEW.transaction_amount = 0 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'transaction_amount must be greater than 0';
    END IF;

    IF NEW.transactionType NOT IN ('DR', 'CR') THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'transactionType must be DR or CR';
    END IF;

    IF NEW.accountcode = '' OR NEW.accountcode IS NULL THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'accountcode must not be empty';
    END IF;

    
    SET NEW.teller = IFNULL(
        (SELECT post_as FROM campus_dynamics_accounts.fin_teller_alias
          WHERE username = NEW.teller), NEW.teller);
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER campus_dynamics_accounts.trg_fin_ledger_before_update
BEFORE UPDATE ON campus_dynamics_accounts.fin_ledger
FOR EACH ROW
BEGIN
    IF NEW.transaction_amount <> OLD.transaction_amount AND NEW.transaction_amount = 0 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'transaction_amount must be greater than 0';
    END IF;

    IF NEW.transactionType <> OLD.transactionType AND NEW.transactionType NOT IN ('DR', 'CR') THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'transactionType must be DR or CR';
    END IF;

    IF (NEW.accountcode <> OLD.accountcode OR (OLD.accountcode IS NOT NULL AND NEW.accountcode IS NULL))
       AND (NEW.accountcode = '' OR NEW.accountcode IS NULL) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'accountcode must not be empty';
    END IF;

    SET NEW.teller = IFNULL(
        (SELECT post_as FROM campus_dynamics_accounts.fin_teller_alias
          WHERE username = NEW.teller), NEW.teller);
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER campus_dynamics_accounts.trg_fin_ledger_after_update
AFTER UPDATE ON campus_dynamics_accounts.fin_ledger
FOR EACH ROW
BEGIN
    IF OLD.transaction_amount <> NEW.transaction_amount
       OR OLD.transactionType <> NEW.transactionType THEN

        INSERT INTO campus_dynamics_accounts.edit_ledger
            (TID, accountcode, account_type, transactionType, transaction_amount,
             particulars, voucherNo, transactionDate, teller, timeLog, folio,
             journal_no, trans_currency, actual_amount, curr_balance,
             trigger_tid, old_transaction_amount, new_transaction_amount,
             old_transactionType, new_transactionType, triggered_by, trigger_date)
        VALUES
            (OLD.TID, OLD.accountcode, OLD.account_type, OLD.transactionType, OLD.transaction_amount,
             OLD.particulars, OLD.voucherNo, OLD.transactionDate, OLD.teller,
             IFNULL(OLD.timeLog, NOW()), OLD.folio,
             IFNULL(OLD.journal_no,'-'), IFNULL(OLD.trans_currency,'UGX'),
             IFNULL(OLD.actual_amount,0), IFNULL(OLD.curr_balance,'-'),
             OLD.TID, OLD.transaction_amount, NEW.transaction_amount,
             OLD.transactionType, NEW.transactionType, USER(), NOW());
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
/*!50003 SET character_set_client  = latin1 */ ;
/*!50003 SET character_set_results = latin1 */ ;
/*!50003 SET collation_connection  = latin1_swedish_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'STRICT_TRANS_TABLES,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`dbmanager`@`%`*/ /*!50003 TRIGGER trg_fin_ledger_before_delete
BEFORE DELETE ON fin_ledger
FOR EACH ROW
BEGIN
    INSERT INTO fin_deleted_ledger 
        (TID, accountcode, account_type, transactionType, transaction_amount,
         particulars, voucherNo, transactionDate, teller, timeLog, folio,
         journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount,
         delete_date, deleted_by)
    VALUES
        (OLD.TID, OLD.accountcode, OLD.account_type, OLD.transactionType, OLD.transaction_amount,
         OLD.particulars, OLD.voucherNo, OLD.transactionDate, OLD.teller, IFNULL(OLD.timeLog, NOW()), OLD.folio,
         OLD.journal_no, OLD.trans_currency, OLD.actual_amount, IFNULL(OLD.curr_balance, '-'), OLD.forex_rate, OLD.ugx_amount,
         NOW(), USER());
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

-- Dump completed on 2026-09-30 14:54:27
