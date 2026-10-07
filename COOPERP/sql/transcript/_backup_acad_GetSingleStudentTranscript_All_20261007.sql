DROP PROCEDURE IF EXISTS `acad_GetSingleStudentTranscript_All`;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `acad_GetSingleStudentTranscript_All`(reg CHAR(30))
BEGIN

DECLARE prog,sys CHAR(45);

SET prog=acad_GetProgCodeByRegNo(reg);
SELECT acad_proper_case(study_system) INTO sys FROM acad_programme WHERE progcode=prog LIMIT 1;

SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename, sys,
r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester) acad, studyyear, score, grade, gradept,
gpa, result_comment, CreditUnits, r.progid, r.study_system,
CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT('      CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores
FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno
WHERE g.regno=reg AND trans_status='Ready'
ORDER BY r.studyyear, r.semester, r.ID;

END$$
DELIMITER ;
