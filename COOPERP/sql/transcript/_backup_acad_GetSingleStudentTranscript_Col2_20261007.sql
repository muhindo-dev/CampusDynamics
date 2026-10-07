DROP PROCEDURE IF EXISTS `acad_GetSingleStudentTranscript_Col2`;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `acad_GetSingleStudentTranscript_Col2`(reg CHAR(30))
BEGIN

DECLARE maxYr,maxSem INT;
DECLARE prog,sys CHAR(45);

SET prog=acad_GetProgCodeByRegNo(reg);
SELECT acad_proper_case(study_system) INTO sys FROM acad_programme WHERE progcode=prog LIMIT 1;

SELECT MAX(studyyear),MAX(semester) INTO maxYr,maxSem FROM acad_transcript_results WHERE regno=reg;

IF maxSem<=2 THEN

SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename,sys,acad_GetDissertation(reg) AS topic,
r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester)acad, studyyear, score, grade, gradept,
gpa, result_comment, CreditUnits, r.progid, r.study_system,CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT(' CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores
FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno
WHERE g.regno=reg AND trans_status='Ready' AND r.semester=2 ;


ELSE

SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename,sys,acad_GetDissertation(reg) AS topic,
r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester)acad, studyyear, score, grade, gradept,
gpa, result_comment, CreditUnits, r.progid, r.study_system,CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT(' CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores
FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno
WHERE g.regno=reg AND trans_status='Ready' AND r.studyyear=1  AND r.semester=2

UNION
SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename,sys,acad_GetDissertation(reg) AS topic,
r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester)acad, studyyear, score, grade, gradept,
gpa, result_comment, CreditUnits, r.progid, r.study_system,CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT(' CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores
FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno
WHERE g.regno=reg AND trans_status='Ready' AND r.studyyear=2 AND r.semester=1

UNION

SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename,sys,acad_GetDissertation(reg) AS topic,
r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester)acad, studyyear, score, grade, gradept,
gpa, result_comment, CreditUnits, r.progid, r.study_system,CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT(' CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores
FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno
WHERE g.regno=reg AND trans_status='Ready' AND r.studyyear=2 AND r.semester=3

UNION
SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename,sys,acad_GetDissertation(reg) AS topic,
r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester)acad, studyyear, score, grade, gradept,
gpa, result_comment, CreditUnits, r.progid, r.study_system,CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT(' CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores
FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno
WHERE g.regno=reg AND trans_status='Ready' AND r.studyyear=3 AND r.semester=2;

END IF;






END$$
DELIMITER ;
