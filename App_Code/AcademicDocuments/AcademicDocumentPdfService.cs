using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using MySql.Data.MySqlClient;
using DevExpress.XtraReports.UI;
using ResultsDataTableAdapters;

public sealed class AcademicDocumentPdfService
{
	public sealed class ExportResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public string FileName { get; set; }
		public string ContentType { get; set; }
		public byte[] Content { get; set; }
		public int GeneratedCount { get; set; }
	}

	public ExportResult GeneratePdf(string documentType, IEnumerable<string> regnos)
	{
		try
		{
			List<string> normalizedRegnos = NormalizeRegnos(regnos);
			if (normalizedRegnos.Count == 0)
			{
				return new ExportResult
				{
					Success = false,
					Message = "No student registration numbers were provided."
				};
			}

			string normalizedDocumentType = NormalizeDocumentType(documentType);
			if (string.IsNullOrEmpty(normalizedDocumentType))
			{
				return new ExportResult
				{
					Success = false,
					Message = "Unsupported document type. Choose Transcript or Certificate."
				};
			}

			// Template 2 (list format) is fed by the combined all-semesters stored
			// procedure. Best-effort self-heal so it works even where the SQL file was
			// not deployed manually. CREATE-only (never DROP) so an existing, working
			// procedure can never be destroyed by a partial-privilege database user.
			if (normalizedDocumentType == "TranscriptList")
				EnsureListTranscriptProc();

			string diagnosticMessage = string.Empty;
			ResultsData dataSource = BuildSelectedStudentsData(normalizedRegnos, out diagnosticMessage);
			if (dataSource == null || dataSource.acad_GetBatchStudentTranscriptData == null || dataSource.acad_GetBatchStudentTranscriptData.Count == 0)
			{
				return new ExportResult
				{
					Success = false,
					Message = string.IsNullOrEmpty(diagnosticMessage)
						? "No printable academic document data was found for the selected student(s)."
						: diagnosticMessage
				};
			}

			XtraReport report = null;
			try
			{
				report = CreateReport(normalizedDocumentType, dataSource);
				if (report == null)
				{
					return new ExportResult
					{
						Success = false,
						Message = "Could not create the requested report template."
					};
				}

				using (MemoryStream output = new MemoryStream())
				{
					report.CreateDocument();
					report.ExportToPdf(output);

					return new ExportResult
					{
						Success = true,
						Content = output.ToArray(),
						ContentType = "application/pdf",
						FileName = BuildFileName(normalizedDocumentType, normalizedRegnos),
						GeneratedCount = dataSource.acad_GetBatchStudentTranscriptData.Count
					};
				}
			}
			finally
			{
				if (report != null)
					report.Dispose();

				if (dataSource != null)
					dataSource.Dispose();
			}
		}
		catch (Exception ex)
		{
			return new ExportResult
			{
				Success = false,
				Message = "Error generating academic document PDF: " + ex.Message
			};
		}
	}

	private static List<string> NormalizeRegnos(IEnumerable<string> regnos)
	{
		List<string> list = new List<string>();
		if (regnos == null)
			return list;

		foreach (string regno in regnos)
		{
			string value = (regno ?? string.Empty).Trim();
			if (string.IsNullOrEmpty(value))
				continue;

			bool exists = false;
			for (int i = 0; i < list.Count; i++)
			{
				if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
				{
					exists = true;
					break;
				}
			}

			if (!exists)
				list.Add(value);
		}

		return list;
	}

	private static string NormalizeDocumentType(string documentType)
	{
		string value = (documentType ?? string.Empty).Trim().ToUpperInvariant();
		if (value == "TRANSCRIPT")
			return "Transcript";
		if (value == "TRANSCRIPTLIST" || value == "TRANSCRIPT_LIST" || value == "TRANSCRIPTLISTPDF")
			return "TranscriptList";
		if (value == "TRANSCRIPTCOMPACT" || value == "TRANSCRIPT_COMPACT" || value == "TRANSCRIPTSINGLEPAGE")
			return "TranscriptCompact";
		if (value == "CERTIFICATE")
			return "Certificate";
		return string.Empty;
	}

	private static string BuildFileName(string documentType, IList<string> regnos)
	{
		string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		if (regnos != null && regnos.Count == 1)
			return string.Format("{0}_{1}_{2}.pdf", documentType, MakeSafeFileName(regnos[0]), stamp);

		int count = regnos != null ? regnos.Count : 0;
		return string.Format("{0}_Batch_{1}_Students_{2}.pdf", documentType, count, stamp);
	}

	private static string MakeSafeFileName(string value)
	{
		string safe = value ?? string.Empty;
		foreach (char ch in Path.GetInvalidFileNameChars())
			safe = safe.Replace(ch, '_');
		return safe;
	}

	private static XtraReport CreateReport(string documentType, ResultsData dataSource)
	{
		if (documentType == "Transcript")
		{
			FinalTranscript report = new FinalTranscript();
			report.DataSource = dataSource;
			report.RequestParameters = false;
			return report;
		}

		if (documentType == "TranscriptList")
		{
			FinalTranscriptList report = new FinalTranscriptList();
			report.DataSource = dataSource;
			report.RequestParameters = false;
			return report;
		}

		if (documentType == "TranscriptCompact")
		{
			// Template 3 — Template 1's two-column grid, restyled to fit one page:
			// borderless, unpadded, small type, all capitals, no key-to-grades page.
			FinalTranscriptCompact report = new FinalTranscriptCompact();
			report.DataSource = dataSource;
			report.RequestParameters = false;
			return report;
		}

		if (documentType == "Certificate")
		{
			Certificate report = new Certificate();
			report.DataSource = dataSource;
			report.RequestParameters = false;
			return report;
		}

		return null;
	}

	/// <summary>
	/// Best-effort creation of the combined all-semesters transcript procedure used by
	/// Template 2 (list format). Uses CREATE (not DROP+CREATE): if the routine already
	/// exists the CREATE simply fails and is ignored, so a working procedure is never
	/// removed. If the database user lacks CREATE ROUTINE the exception is swallowed and
	/// the manually deployed procedure (sql/transcript/acad_GetSingleStudentTranscript_All.sql)
	/// is relied upon instead.
	/// </summary>
	private static void EnsureListTranscriptProc()
	{
		try
		{
			string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"] != null
				? ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString
				: string.Empty;

			if (string.IsNullOrEmpty(connStr))
				return;

			string createSql =
				"CREATE PROCEDURE acad_GetSingleStudentTranscript_All(reg CHAR(30))\n" +
				"BEGIN\n" +
				"  DECLARE prog,sys CHAR(45);\n" +
				"  SET prog=acad_GetProgCodeByRegNo(reg);\n" +
				"  SELECT acad_proper_case(study_system) INTO sys FROM acad_programme WHERE progcode=prog LIMIT 1;\n" +
				"  SELECT CONCAT(UPPER(acad_GetCourseNameByCode(courseid)),IF(COALESCE(r.is_retake,0)=1,' (RT)','')) AS coursename, sys,\n" +
				"  r.ID, r.regno, courseid, r.semester, acad_GetResultsAcademicYear(reg,studyyear, r.semester) acad, studyyear, score, grade, gradept,\n" +
				"  gpa, result_comment, CreditUnits, r.progid, r.study_system,\n" +
				"  CONCAT_WS('  ',CONCAT('GPA: ',gpa),CONCAT('      CGPA: ',acad_CGPAFinder_ByPeriod(r.regno,r.studyyear, r.semester))) AS SemesterScores\n" +
				"  FROM acad_transcript_results r JOIN acad_graduands g ON g.regno=r.regno\n" +
				// No trans_status filter: once a transcript was printed (status 'Printed') the
				// filter made every later copy blank. Who may print is GraduationPrintGate's job.
				"  WHERE g.regno=reg\n" +
				"  ORDER BY r.studyyear, r.semester, r.ID;\n" +
				"END";

			using (MySqlConnection conn = new MySqlConnection(connStr))
			{
				conn.Open();
				using (MySqlCommand create = new MySqlCommand(createSql, conn))
					create.ExecuteNonQuery();
			}
		}
		catch
		{
			// Non-fatal: routine already exists, or the user cannot create it and the
			// manually deployed copy is used. Any real "missing procedure" failure will
			// surface as a clear report-generation error to the caller.
		}
	}

	private static ResultsData BuildSelectedStudentsData(IEnumerable<string> regnos, out string diagnosticMessage)
	{
		diagnosticMessage = string.Empty;
		ResultsData master = new ResultsData();
		master.EnforceConstraints = false;

		acad_universityTableAdapter universityAdapter = new acad_universityTableAdapter();
		acad_GetBatchStudentTranscriptDataTableAdapter transcriptAdapter = new acad_GetBatchStudentTranscriptDataTableAdapter();

		universityAdapter.Fill(master.acad_university);

		List<string> diagnostics = new List<string>();

		foreach (string regno in regnos)
		{
			EnsureClassicGraduandLinkIfMissing(regno);

			ResultsData single = new ResultsData();
			single.EnforceConstraints = false;

			int rowsBeforeFallback = 0;
			transcriptAdapter.SingleTranscript(single.acad_GetBatchStudentTranscriptData, regno);
			rowsBeforeFallback = single.acad_GetBatchStudentTranscriptData != null ? single.acad_GetBatchStudentTranscriptData.Count : 0;
			CertificateDataHelper.EnsureSingleStudentData(single, regno);
			int rowsAfterFallback = single.acad_GetBatchStudentTranscriptData != null ? single.acad_GetBatchStudentTranscriptData.Count : 0;

			if (single.acad_GetBatchStudentTranscriptData != null && single.acad_GetBatchStudentTranscriptData.Count > 0)
			{
				foreach (ResultsData.acad_GetBatchStudentTranscriptDataRow row in single.acad_GetBatchStudentTranscriptData.Rows)
					master.acad_GetBatchStudentTranscriptData.ImportRow(row);
			}
			else
			{
				diagnostics.Add(BuildUnavailableReason(regno, rowsBeforeFallback, rowsAfterFallback));
			}

			single.Dispose();
		}

		CertificateDataHelper.RemapMastersClassifications(master);

		if (master.acad_GetBatchStudentTranscriptData == null || master.acad_GetBatchStudentTranscriptData.Count == 0)
		{
			diagnosticMessage = diagnostics.Count > 0
				? string.Join(" ", diagnostics.ToArray())
				: "No printable academic document data was found for the selected student(s).";
		}

		return master;
	}

	private static void EnsureClassicGraduandLinkIfMissing(string regno)
	{
		string safeRegno = (regno ?? string.Empty).Trim();
		if (string.IsNullOrEmpty(safeRegno))
			return;

		try
		{
			string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"] != null
				? ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString
				: string.Empty;

			if (string.IsNullOrEmpty(connStr))
				return;

			using (MySqlConnection conn = new MySqlConnection(connStr))
			{
				conn.Open();

				// 1. Auto-insert into acad_graduands if the student is missing
				using (MySqlCommand cmd = new MySqlCommand(@"
					INSERT INTO acad_graduands
					(
						regno, acadyear, cgpa, degclass, nationality, stud_name, progcode,
						trans_status, cert_status, trans_printer, cert_printer,
						gender, grad_date, comp_date, convocation
					)
					SELECT
						TRIM(s.regno) AS regno,
						IFNULL((SELECT ar.acad_year FROM acad_registration ar WHERE TRIM(ar.regno)=TRIM(s.regno) ORDER BY ar.ID DESC LIMIT 1), '') AS acadyear,
						IFNULL(acad_CGPAFinder(s.regno), 0) AS cgpa,
						IFNULL(acad_GetDegClass(
							acad_CGPAFinder(s.regno),
							IFNULL(s.gradSystemID, 1),
							CASE
								WHEN p.levelCode = '3' THEN 'Bachelors'
								WHEN p.levelCode = '2' THEN 'Diploma'
								WHEN p.levelCode = '1' THEN 'Certificate'
								WHEN p.levelCode = '4' THEN 'Postgraduate'
								ELSE 'Masters'
							END
						), 'Pass') AS degclass,
						IFNULL(NULLIF(TRIM(s.nationality), ''), 'UGANDAN') AS nationality,
						IFNULL(NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''), ' ', IFNULL(s.othername,''))), ''), TRIM(s.regno)) AS stud_name,
						IFNULL(s.progid, '') AS progcode,
						'Ready' AS trans_status,
						'Ready' AS cert_status,
						'-' AS trans_printer,
						'-' AS cert_printer,
						IFNULL(s.gender, '') AS gender,
						NULL AS grad_date,
						NULL AS comp_date,
						'-' AS convocation
					FROM acad_student s
					LEFT JOIN acad_programme p ON p.progcode = s.progid
					WHERE TRIM(s.regno) = @reg
					  AND NOT EXISTS (SELECT 1 FROM acad_graduands g WHERE TRIM(g.regno)=TRIM(s.regno))
					  AND EXISTS (SELECT 1 FROM acad_results r WHERE TRIM(r.regno)=TRIM(s.regno))
					LIMIT 1;", conn))
				{
					cmd.Parameters.AddWithValue("@reg", safeRegno);
					cmd.ExecuteNonQuery();
				}

				// 2. Always refresh acad_transcript_results from acad_results before generating the PDF.
				// The SP reads from acad_transcript_results (a materialized copy), not acad_results directly.
				// Always rebuilding ensures newly-entered grades are reflected immediately — without requiring
				// the registrar to manually run acad_CreateTranscript between result entry and PDF generation.
				using (MySqlCommand createCmd = new MySqlCommand(
					"CALL acad_CreateTranscript(@reg, 'Normal', 0)", conn))
				{
					createCmd.CommandTimeout = 120;
					createCmd.Parameters.AddWithValue("@reg", safeRegno);
					createCmd.ExecuteNonQuery();
				}

				// 3. Completion date. Priority:
				//    (a) An admin-entered acad_student.completion_date OVERRIDES everything (set on the
				//        NewStudentInfo quick-edit "Academic Information" panel), OR
				//    (b) the default = JUNE (end of June) of the FINAL semester's academic year — i.e. the
				//        academic year of the highest study-year/semester block. (Previously this defaulted
				//        to December; changed to June per the registrar.)
				//    Computed on every build so the completion date stays consistent with the transcript's
				//    own final academic year, unless an explicit override is stored.
				using (MySqlCommand compCmd = new MySqlCommand(@"
					UPDATE acad_graduands g
					JOIN acad_student s ON TRIM(s.regno) = TRIM(g.regno)
					LEFT JOIN (
						SELECT acad
						FROM acad_transcript_results
						WHERE regno = @reg AND IFNULL(acad,'') NOT IN ('', '-')
						ORDER BY studyyear DESC, semester DESC
						LIMIT 1
					) x ON 1=1
					SET g.comp_date = CASE
						WHEN s.completion_date IS NOT NULL AND s.completion_date <> '0000-00-00'
							THEN s.completion_date
						WHEN RIGHT(IFNULL(x.acad,''),4) REGEXP '^[0-9]{4}$'
							THEN STR_TO_DATE(CONCAT(RIGHT(x.acad,4), '-06-30'), '%Y-%m-%d')
						ELSE g.comp_date
					END
					WHERE TRIM(g.regno) = TRIM(@reg)", conn))
				{
					compCmd.CommandTimeout = 60;
					compCmd.Parameters.AddWithValue("@reg", safeRegno);
					compCmd.ExecuteNonQuery();
				}
			}
		}
		catch
		{
			// Never fail document generation because of repair attempt.
		}
	}

	private static string BuildUnavailableReason(string regno, int rowsBeforeFallback, int rowsAfterFallback)
	{
		string safeRegno = string.IsNullOrEmpty(regno) ? "(blank regno)" : regno;
		try
		{
			string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"] != null
				? ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString
				: string.Empty;

			if (string.IsNullOrEmpty(connStr))
			{
				return string.Format("{0}: classic document data was empty (SP rows: {1}, fallback rows: {2}) and the database connection for deeper diagnostics is unavailable.", safeRegno, rowsBeforeFallback, rowsAfterFallback);
			}

			using (MySqlConnection conn = new MySqlConnection(connStr))
			{
				conn.Open();

				bool studentExists = false;
				bool graduandExists = false;
				string studentName = string.Empty;
				string programmeName = string.Empty;

				using (MySqlCommand cmdStudent = new MySqlCommand(@"SELECT CONCAT(IFNULL(firstname,''), ' ', IFNULL(othername,'')) AS stud_name,
					(SELECT progname FROM acad_programme WHERE progcode = acad_student.progid LIMIT 1) AS prog_name
					FROM acad_student WHERE TRIM(regno)=@reg LIMIT 1", conn))
				{
					cmdStudent.Parameters.AddWithValue("@reg", safeRegno);
					using (MySqlDataReader reader = cmdStudent.ExecuteReader())
					{
						if (reader.Read())
						{
							studentExists = true;
							studentName = Convert.ToString(reader["stud_name"] ?? string.Empty).Trim();
							programmeName = Convert.ToString(reader["prog_name"] ?? string.Empty).Trim();
						}
					}
				}

				using (MySqlCommand cmdGrad = new MySqlCommand("SELECT COUNT(*) FROM acad_graduands WHERE TRIM(regno)=@reg", conn))
				{
					cmdGrad.Parameters.AddWithValue("@reg", safeRegno);
					graduandExists = Convert.ToInt32(cmdGrad.ExecuteScalar()) > 0;
				}

				if (!studentExists)
				{
					return string.Format("{0}: student record was not found in acad_student.", safeRegno);
				}

				if (!graduandExists)
				{
					return string.Format("{0}: no printable classic document data was found. The student{1}{2} is in acad_student but not in acad_graduands, and the classic SingleTranscript pipeline returned 0 rows. In the old system, certificates/transcripts are generated from the Transcript Document Centre graduand dataset, not from every student in New Student Info.",
						safeRegno,
						string.IsNullOrEmpty(studentName) ? string.Empty : " (" + studentName + ")",
						string.IsNullOrEmpty(programmeName) ? string.Empty : " for programme " + programmeName);
				}

				return string.Format("{0}: the student exists in the graduand dataset, but the classic SingleTranscript pipeline still returned no printable rows (SP rows: {1}, fallback rows: {2}). This usually means transcript preparation or transcript-format data is incomplete in the classic document setup.", safeRegno, rowsBeforeFallback, rowsAfterFallback);
			}
		}
		catch (Exception ex)
		{
			return string.Format("{0}: no printable academic document data was found, and diagnostic lookup failed: {1}", safeRegno, ex.Message);
		}
	}
}
