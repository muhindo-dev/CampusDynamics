-- acad_ApplicNoGenerator(yr): next application number MRU + yr + 6-digit sequence.
-- 2026-10-07: a number is only free if no application, student (acad_student.regno) or
-- portal login (my_aspnet_users.name) already holds it. Previously only acad_applications
-- was checked, so deleting an application re-issued its number to a different person while
-- the original student and login still carried it (MRU2027000003).
DROP FUNCTION IF EXISTS acad_ApplicNoGenerator;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` FUNCTION `acad_ApplicNoGenerator`(yr INT) RETURNS char(25) CHARSET utf8
BEGIN
DECLARE curNo CHAR(10);
DECLARE cnt INT;
DECLARE applic_no CHAR(30);

  SELECT MAX(CAST(SUBSTRING(stud_entry_no,8) AS UNSIGNED)) INTO curNo
  FROM acad_applications WHERE stud_entry_year=yr;

SET applic_no = CONCAT('MRU',yr,LPAD(IF(curNo IS NULL OR curNo='',1,CAST(curNo AS UNSIGNED)+1),6,0));

SELECT (SELECT COUNT(*) FROM acad_applications WHERE stud_entry_no=applic_no)
     + (SELECT COUNT(*) FROM acad_student WHERE regno=applic_no)
     + (SELECT COUNT(*) FROM campus_dynamics_portal.my_aspnet_users WHERE name=applic_no)
  INTO cnt;

WHILE cnt > 0 DO
SET applic_no = CONCAT('MRU',yr,LPAD(CAST(SUBSTRING(applic_no,8) AS UNSIGNED) + 1,6,0));

SELECT (SELECT COUNT(*) FROM acad_applications WHERE stud_entry_no=applic_no)
     + (SELECT COUNT(*) FROM acad_student WHERE regno=applic_no)
     + (SELECT COUNT(*) FROM campus_dynamics_portal.my_aspnet_users WHERE name=applic_no)
  INTO cnt;

END WHILE;

  RETURN applic_no;
END$$
DELIMITER ;
