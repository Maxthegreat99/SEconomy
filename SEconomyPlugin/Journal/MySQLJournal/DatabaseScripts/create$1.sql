CREATE DATABASE  IF NOT EXISTS `$CATALOG` /*!40100 DEFAULT CHARACTER SET utf8 */;
USE `$CATALOG`;

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `bank_account`
--

DROP TABLE IF EXISTS `bank_account`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `bank_account` (
  `bank_account_id` bigint(20) NOT NULL AUTO_INCREMENT,
  `user_account_name` varchar(64) NOT NULL,
  `world_id` bigint(20) NOT NULL,
  `flags` int(11) NOT NULL,
  `flags2` int(11) NOT NULL,
  `description` varchar(512) NOT NULL,
  `old_bank_account_k` bigint(20) DEFAULT NULL,
  PRIMARY KEY (`bank_account_id`)
) ENGINE=InnoDB AUTO_INCREMENT=29941 DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bank_account_transaction`
--

DROP TABLE IF EXISTS `bank_account_transaction`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `bank_account_transaction` (
  `bank_account_transaction_id` bigint(20) NOT NULL AUTO_INCREMENT,
  `bank_account_transaction_fk` bigint(20) DEFAULT NULL,
  `bank_account_fk` bigint(20) NOT NULL,
  `amount` bigint(20) NOT NULL,
  `message` varchar(512) DEFAULT NULL,
  `flags` int(11) NOT NULL,
  `flags2` int(11) NOT NULL,
  `transaction_date_utc` datetime NOT NULL,
  `old_bank_account_transaction_k` bigint(20) DEFAULT NULL,
  PRIMARY KEY (`bank_account_transaction_id`),
  KEY `fk_transaction-bank_account_idx` (`bank_account_fk`),
  CONSTRAINT `fk_transaction-bank_account` FOREIGN KEY (`bank_account_fk`) REFERENCES `bank_account` (`bank_account_id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB AUTO_INCREMENT=0 DEFAULT CHARSET=utf8;

DROP PROCEDURE IF EXISTS `seconomy_squash`;


CREATE DEFINER=`root`@`localhost` PROCEDURE `seconomy_squash`()
BEGIN
	drop temporary table if exists seconomy_squash_temp;
	create temporary table if not exists seconomy_squash_temp engine=memory
		select bank_account_id, ifnull(sum(amount),0) AS total_amount
		from bank_account
		left outer join bank_account_transaction on bank_account_id = bank_account_fk
		group by bank_account_id;

	truncate table bank_account_transaction;

	insert into bank_account_transaction (bank_account_fk, amount, message, flags, flags2, transaction_date_utc)
		select bank_account_id, total_amount, 'Transaction Squash', 3, 0, UTC_TIMESTAMP()
		from seconomy_squash_temp;
END;


/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

