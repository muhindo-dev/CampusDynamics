-- 2026-10-07: no trans_status='Ready' filter - a printed transcript must not reprint blank.
-- Who may print is decided by GraduationPrintGate; every print is logged in acad_document_prints.
DROP PROCEDURE IF EXISTS `acad_GetSingleStudentTranscript`;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `acad_GetSingleStudentTranscript`(reg CHAR(30))
BEGIN

DECLARE prognm,prog,fax,deg,lev VARCHAR(150);



SET prog=acad_GetProgCodeByRegNo(reg);

SET prognm=acad_GetProgNameByCode(prog);

SET fax=acad_GetFacultyFromprogCode(prog);

SELECT levelCode INTO lev FROM acad_programme WHERE progcode=prog LIMIT 1;



IF lev='3' THEN

SET lev='Bachelors';

ELSEIF lev='2' THEN

SET lev='Diploma';

ELSE

SET lev='Masters';

END IF;

SET deg=acad_GetDegClass(acad_CGPAFinder(regno),acad_GetGIDByRegNo(regno),lev);



SELECT deg,fax,acad_GetTranscriptStudData(g.regno,'DOB') dobs,

UPPER(acad_GetTranscriptStudData(g.regno,'NAT')) nat,acad_GetTranscriptStudData(g.regno,'GENDER') gen,cgpa,

prognm AS prog,stud_name studnm,CONCAT('~/COOPERP/StudentInfo/photos/',acad_GetTranscriptStudData(g.regno,'PHOTO'))

photo,acad_GetCourseNameByCode(courseid) AS coursename,

acad_GetBridgingDeclaration(g.regno) AS disclaimer,

r.* FROM acad_results r JOIN acad_graduands g ON g.regno=r.regno

WHERE g.regno=reg LIMIT 1;



END$$
DELIMITER ;
