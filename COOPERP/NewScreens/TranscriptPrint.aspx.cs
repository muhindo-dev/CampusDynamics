using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Print-optimized HTML academic transcript with an adaptive layout engine
/// (dynamic per-course font scaling, intelligent wrapping, multi-stage page-fit
/// compression). Reads the same data the official transcript uses, straight from
/// acad_results, and renders a faithful, registrar-style document for print / save-as-PDF.
/// URL: TranscriptPrint.aspx?reg=REGNO[&autoprint=1]
/// </summary>
public partial class COOPERP_NewScreens_TranscriptPrint : System.Web.UI.Page
{
    private string Conn { get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; } }

    protected string Body = "";
    protected bool AutoPrint = false;

    private sealed class Course
    {
        public string Code, Name, Grade;
        public double Cu, Gp;
    }
    private sealed class Sem
    {
        public int Year, Semester;
        public string Acad = "";
        public List<Course> Courses = new List<Course>();
        public double SemGpa, Cgpa; // cgpa = cumulative up to and including this semester
    }

    /// <summary>
    /// MRU study systems are only ever "Semester" or "Session" — in-service students study in
    /// SESSIONS, everyone else by SEMESTER. Any legacy programme value (Full Time, Part Time,
    /// Blended, In-Service, etc.) is normalised to one of these two so the transcript never shows
    /// anything else.
    /// </summary>
    private static string NormalizeStudySystem(string raw)
    {
        string v = (raw ?? "").Trim().ToUpperInvariant();
        if (v == "SESSION" || v == "PART TIME" || v == "PART-TIME" || v == "BLENDED"
            || v.Contains("SESSION") || v.Contains("SERVICE") || v.Contains("EVENING") || v.Contains("WEEKEND"))
            return "Session";
        return "Semester";   // Semester, Full Time, Day, blank, or anything else
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        AutoPrint = (Request["autoprint"] ?? "") == "1";
        string reg = (Request["reg"] ?? Request["regno"] ?? "").Trim();
        if (string.IsNullOrEmpty(reg)) { Body = Err("No student registration number supplied. Use ?reg=REGNO."); return; }

        // This page is reachable by its own URL, not only through the students list, so the
        // graduation check has to stand here too. Checking in one place and redirecting to the
        // other would leave the door that was typed straight into the address bar open.
        GraduationPrintGate.Outcome gate =
            GraduationPrintGate.Check(new string[] { reg }, "Transcript");
        if (gate.AnyBlocked)
        {
            GraduationPrintGate.Record(gate, "TranscriptHTML", null, Actor(), RoleAccessService.GetRoleName());
            AutoPrint = false;
            Body = Err(Server.HtmlEncode(GraduationPrintGate.BlockedSummary(gate))
                       .Replace("\n", "<br>"));
            return;
        }

        try
        {
            Body = Build(reg);
            // Recorded after the transcript exists, and this route is a transcript by
            // definition, so it also moves the graduation list's transcript state forward.
            GraduationPrintGate.Record(gate, "TranscriptHTML", "TranscriptPrint.aspx",
                                       Actor(), RoleAccessService.GetRoleName());
        }
        catch (Exception ex) { Body = Err("Could not build transcript: " + Server.HtmlEncode(ex.Message)); }
    }

    /// <summary>Who is printing, for the document record.</summary>
    private string Actor()
    {
        try
        {
            if (Session != null)
            {
                string s = Convert.ToString(Session["username"] ?? Session["usernm"] ?? "").Trim();
                if (s != "") return s;
            }
        }
        catch { }
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                return (User.Identity.Name ?? "").Trim();
        }
        catch { }
        return "unknown";
    }

    private string Build(string reg)
    {
        // ---- Bio ----
        string studnm = "", regno = reg, entryno = "", gender = "", nationality = "", dob = "",
               progname = "", faculty = "", studySystem = "Semester", levelCode = "3", photofile = "",
               compDateRaw = "";
        double minLoad = 0;
        using (var conn = new MySqlConnection(Conn))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(@"
                SELECT TRIM(CONCAT_WS(' ', NULLIF(s.firstname,''), NULLIF(s.othername,''))) studnm,
                       s.regno, IFNULL(s.entryno,'') entryno, IFNULL(s.gender,'') gender,
                       IFNULL(s.nationality,'') nationality,
                       CASE WHEN s.dob IS NULL OR s.dob='0000-00-00' THEN '' ELSE DATE_FORMAT(s.dob,'%d %M, %Y') END dob,
                       CASE WHEN s.completion_date IS NULL OR s.completion_date='0000-00-00' THEN '' ELSE DATE_FORMAT(s.completion_date,'%d %M, %Y') END compdate,
                       IFNULL(p.progname,'') progname, IFNULL(p.mincredit,0) minLoad,
                       IFNULL(p.study_system,'Semester') study_system, IFNULL(f.faculty_name,'') faculty,
                       IFNULL(p.levelCode,'3') levelCode, IFNULL(s.photofile,'') photofile
                FROM acad_student s
                LEFT JOIN acad_programme p ON p.progcode = s.progid
                LEFT JOIN acad_faculty f ON f.faculty_code = p.faculty_code
                WHERE s.regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", reg);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return Err("Student " + Server.HtmlEncode(reg) + " was not found.");
                    studnm = S(r["studnm"]); regno = S(r["regno"]); entryno = S(r["entryno"]);
                    gender = S(r["gender"]); nationality = S(r["nationality"]); dob = S(r["dob"]);
                    compDateRaw = S(r["compdate"]);
                    progname = S(r["progname"]); faculty = S(r["faculty"]);
                    studySystem = NormalizeStudySystem(S(r["study_system"])); minLoad = D(r["minLoad"]);
                    levelCode = S(r["levelCode"]); if (levelCode == "") levelCode = "3";
                    photofile = S(r["photofile"]);
                }
            }

            // ---- Results (source of truth: acad_results) ----
            var sems = new List<Sem>();
            Sem cur = null;
            using (var cmd = new MySqlCommand(@"
                SELECT COALESCE(r.studyyear,1) studyyear, r.semester,
                       -- Block academic year = the year the student was REGISTERED for that
                       -- study-year+semester (authoritative), falling back to the result's own
                       -- year. Keeps the per-semester header consistent with the PDF transcript.
                       COALESCE((SELECT MIN(rg.acad_year) FROM acad_registration rg
                                 WHERE rg.regno=r.regno AND rg.studyyear=COALESCE(r.studyyear,1)
                                   AND rg.semester=r.semester AND IFNULL(rg.acad_year,'') NOT IN ('','-')),
                                IFNULL(r.acad,'')) acad,
                       TRIM(r.courseid) code,
                       CONCAT(UPPER(COALESCE(NULLIF(TRIM(c.courseName),''), acad_GetCourseNameByCode(TRIM(r.courseid)), TRIM(r.courseid))),
                              IF(COALESCE(r.is_retake,0)=1,' (RT)','')) coursename,
                       COALESCE(r.CreditUnits, c.CreditUnit, 3) cu, IFNULL(r.grade,'') grade,
                       COALESCE(r.gradept,0) gradept
                FROM acad_results r
                LEFT JOIN acad_course c ON TRIM(c.courseID)=TRIM(r.courseid)
                WHERE r.regno=@r
                ORDER BY COALESCE(r.studyyear,1), r.semester, code", conn))
            {
                cmd.Parameters.AddWithValue("@r", reg);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int yr = I(r["studyyear"]), sm = I(r["semester"]);
                        if (cur == null || cur.Year != yr || cur.Semester != sm)
                        {
                            cur = new Sem { Year = yr, Semester = sm, Acad = S(r["acad"]) };
                            sems.Add(cur);
                        }
                        cur.Courses.Add(new Course
                        {
                            Code = S(r["code"]),
                            Name = S(r["coursename"]),
                            Cu = D(r["cu"]),
                            Grade = S(r["grade"]),
                            Gp = D(r["gradept"])
                        });
                    }
                }
            }
            if (sems.Count == 0) return Err("No results found for " + Server.HtmlEncode(studnm == "" ? reg : studnm) + ".");

            // ---- GPA / CGPA (cumulative) + totals ----
            double cumGp = 0, cumCu = 0, totalCu = 0;
            foreach (var s in sems)
            {
                double sGp = 0, sCu = 0;
                foreach (var c in s.Courses) { sGp += c.Gp * c.Cu; sCu += c.Cu; }
                s.SemGpa = sCu > 0 ? sGp / sCu : 0;
                cumGp += sGp; cumCu += sCu; totalCu += sCu;
                s.Cgpa = cumCu > 0 ? cumGp / cumCu : 0;
            }
            double cgpa = cumCu > 0 ? cumGp / cumCu : 0;
            string award = Classify(cgpa, levelCode);

            // ---- Completion date ----
            // Priority: an admin-entered acad_student.completion_date OVERRIDES; otherwise default to
            // the END OF JUNE of the FINAL semester's academic year (e.g. "2024/2025" -> 30 June, 2025).
            string completion = compDateRaw;
            if (string.IsNullOrEmpty(completion) && sems.Count > 0)
            {
                string acad = sems[sems.Count - 1].Acad ?? "";
                string digits = "";
                foreach (char ch in acad) if (char.IsDigit(ch)) digits += ch;
                if (digits.Length >= 4)
                {
                    int yr;
                    if (int.TryParse(digits.Substring(digits.Length - 4), out yr) && yr > 1900 && yr < 3000)
                        completion = new DateTime(yr, 6, 30).ToString("dd MMMM, yyyy", CultureInfo.InvariantCulture);
                }
            }

            // ---- Grades key ----
            var keys = new List<string[]>();
            try
            {
                using (var cmd = new MySqlCommand("CALL acad_GetStudentGradingSystemDetails(@r)", conn))
                {
                    cmd.Parameters.AddWithValue("@r", reg);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string g = S(r["grade"]);
                            if (g == "" || g == "-") continue;
                            keys.Add(new[] { g, S(r["Marks"]), S(r["gradep"]) });
                        }
                }
            }
            catch { }

            string photoUrl = (!string.IsNullOrEmpty(photofile) && photofile.Trim() != "-")
                ? ResolveUrl("~/COOPERP/StudentInfo/photos/" + photofile.Trim())
                : ResolveUrl("~/COOPERP/StudentInfo/photos/default.png");

            return Render(studnm, regno, entryno, gender, nationality, dob, progname, faculty,
                          award, cgpa, totalCu, minLoad, sems, keys, levelCode, photoUrl, completion,
                          studySystem);
        }
    }

    // ===================================================================
    // RENDER
    // ===================================================================
    private string Render(string studnm, string regno, string entryno, string gender, string nationality,
        string dob, string progname, string faculty, string award, double cgpa, double totalCu, double minLoad,
        List<Sem> sems, List<string[]> keys, string levelCode, string photoUrl, string completion,
        string studySystem)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='tx' id='tx'>");

        // Student photo — shown in full (object-fit: contain), never cropped, top-right.
        sb.Append("<div class='tx-photo'><img src='" + HttpUtility.HtmlAttributeEncode(photoUrl) +
                  "' alt='Student photo' onerror=\"this.onerror=null;this.src='" +
                  HttpUtility.HtmlAttributeEncode(ResolveUrl("~/COOPERP/StudentInfo/photos/default.png")) + "';\" /></div>");

        // The whole document flows inside this table so its <thead> (the slim identity strip)
        // repeats at the top of EVERY printed page — guaranteeing the student's name/reg-no
        // appear even when results run onto page 2, 3, ...
        sb.Append("<table class='tx-page'><thead><tr><td>");
        sb.Append("<div class='tx-runhead'>");
        sb.Append("<div class='tx-runhead__l'>Muteesa I Royal University &middot; Academic Transcript</div>");
        sb.Append("<div class='tx-runhead__r'><b>" + H(studnm == "" ? regno : studnm) + "</b>" +
                  "<span class='sep'>&middot;</span>" + H(regno) +
                  (progname != "" ? "<span class='sep'>&middot;</span>" + H(progname) : "") + "</div>");
        sb.Append("</div>");
        sb.Append("</td></tr></thead><tbody><tr><td>");

        // Letterhead
        sb.Append("<div class='tx-head'>");
        sb.Append("<div class='tx-head__uni'>MUTEESA I ROYAL UNIVERSITY</div>");
        sb.Append("<div class='tx-head__office'>OFFICE OF THE ACADEMIC REGISTRAR</div>");
        sb.Append("<div class='tx-head__addr'>P.O. Box 14002 Mengo-Kampala, Uganda &nbsp;&bull;&nbsp; P.O. Box 322 Masaka &nbsp;&bull;&nbsp; Email: registrar@mru.ac.ug</div>");
        sb.Append("<div class='tx-head__title'>ACADEMIC TRANSCRIPT</div>");
        sb.Append("</div>");

        // Bio + summary
        sb.Append("<div class='tx-bio'>");
        sb.Append(BioCell("NAME", studnm));
        sb.Append(BioCell("REG. NO.", regno + (entryno != "" && entryno != regno ? " &nbsp;(" + H(entryno) + ")" : "")));
        sb.Append(BioCell("PROGRAMME", progname));
        sb.Append(BioCell("FACULTY", faculty));
        sb.Append(BioCell("SEX", gender));
        sb.Append(BioCell("NATIONALITY", nationality));
        sb.Append(BioCell("DATE OF BIRTH", dob));
        sb.Append(BioCell("CLASS OF AWARD", award));
        sb.Append(BioCell("DATE OF COMPLETION", completion));
        sb.Append("</div>");

        // Award strip
        sb.Append("<div class='tx-strip'>");
        sb.Append(Strip("AWARD", progname));
        sb.Append(Strip("CGPA", cgpa.ToString("0.00")));
        sb.Append(Strip("TOTAL CREDITS", ((int)Math.Round(totalCu)).ToString()));
        sb.Append(Strip("MIN. GRAD. LOAD", minLoad > 0 ? ((int)Math.Round(minLoad)).ToString() : "&mdash;"));
        sb.Append("</div>");

        // Semesters
        sb.Append("<div class='tx-sems'>");
        foreach (var s in sems)
        {
            sb.Append("<div class='sem'>");
            sb.AppendFormat("<div class='sem-head'><span>Year {0} &mdash; {5} {1}{2}</span><span class='sem-gpa'>GPA: {3} &nbsp;&nbsp; CGPA: {4}</span></div>",
                s.Year, s.Semester, s.Acad != "" ? " &nbsp;|&nbsp; " + H(s.Acad) : "",
                s.SemGpa.ToString("0.00"), s.Cgpa.ToString("0.00"), H(studySystem));
            sb.Append("<table class='ct'><thead><tr><th class='c-code'>CODE</th><th class='c-course'>COURSE</th><th class='c-cu'>CU</th><th class='c-grade'>GRADE</th></tr></thead><tbody>");
            foreach (var c in s.Courses)
            {
                // Deterministic font scaling by course-name length (longer name => smaller font),
                // 5 levels with a pronounced 14pt -> 9pt spread so the difference is obvious.
                int nlen = (c.Name ?? "").Length;
                string lvl = nlen <= 22 ? "c-len1"
                           : nlen <= 34 ? "c-len2"
                           : nlen <= 46 ? "c-len3"
                           : nlen <= 58 ? "c-len4" : "c-len5";
                sb.AppendFormat("<tr><td class='c-code'>{0}</td><td class='c-course'><span class='c-fit {4}'>{1}</span></td><td class='c-cu'>{2}</td><td class='c-grade'>{3}</td></tr>",
                    H(c.Code), H(c.Name), ((int)Math.Round(c.Cu)), H(c.Grade), lvl);
            }
            sb.Append("</tbody></table></div>");
        }
        sb.Append("</div>");

        // Key + classification
        sb.Append("<div class='tx-foot-grid'>");
        sb.Append("<div class='tx-key'><div class='tx-key__t'>KEY TO GRADES</div><table class='kt'>");
        sb.Append("<tr><th>Grade</th><th>Marks</th><th>Point</th></tr>");
        if (keys.Count == 0)
            sb.Append("<tr><td colspan='3' style='color:#888'>&mdash;</td></tr>");
        else
            foreach (var k in keys)
                sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>", H(k[0]), H(k[1]), H(k[2]));
        sb.Append("</table></div>");

        sb.Append("<div class='tx-class'><div class='tx-key__t'>CLASSIFICATION (CGPA)</div><table class='kt'>");
        foreach (var row in ClassBands(levelCode))
            sb.AppendFormat("<tr><td>{0}</td><td>{1}</td></tr>", row[0], row[1]);
        sb.Append("</table></div>");
        sb.Append("</div>");

        // Verification / signature
        sb.Append("<div class='tx-verify'>");
        sb.Append("<div class='tx-nb'><strong>NB:</strong> This transcript is not valid without the official seal of the University. Any alteration renders it void.</div>");
        sb.Append("<div class='tx-sign'><div class='tx-sign__line'></div><div class='tx-sign__name'>Dr. Musisi Fred Kamoga</div><div class='tx-sign__title'>ACADEMIC REGISTRAR</div></div>");
        sb.Append("</div>");

        sb.Append("</td></tr></tbody></table>"); // close .tx-page (repeating-header wrapper)
        sb.Append("</div>"); // .tx
        return sb.ToString();
    }

    private static string BioCell(string label, string val)
    {
        return "<div class='tx-bio__cell'><span class='tx-bio__l'>" + label + "</span><span class='tx-bio__v'>" +
               (string.IsNullOrEmpty(val) ? "&mdash;" : H(val)) + "</span></div>";
    }
    private static string Strip(string label, string val)
    {
        return "<div class='tx-strip__c'><div class='tx-strip__v'>" + val + "</div><div class='tx-strip__l'>" + label + "</div></div>";
    }

    // Level-aware classification, matching acad_gs_award (gsID=1).
    // levelCode: 3=Bachelors, 2=Diploma, 1=Certificate, 4=Postgraduate, else Masters.
    private static bool IsDipCert(string levelCode) { return levelCode == "2" || levelCode == "1"; }

    private static string Classify(double cgpa, string levelCode)
    {
        if (IsDipCert(levelCode))
        {
            if (cgpa >= 4.40) return "CLASS I (DISTINCTION)";
            if (cgpa >= 2.80) return "CLASS II (CREDIT)";
            if (cgpa >= 2.00) return "CLASS III (PASS)";
            return "&mdash;";
        }
        if (cgpa >= 4.40) return "FIRST CLASS (HONOURS)";
        if (cgpa >= 3.60) return "SECOND CLASS (UPPER)";
        if (cgpa >= 2.80) return "SECOND CLASS (LOWER)";
        if (cgpa >= 2.00) return "THIRD CLASS (PASS)";
        return "&mdash;";
    }

    private static List<string[]> ClassBands(string levelCode)
    {
        var b = new List<string[]>();
        if (IsDipCert(levelCode))
        {
            b.Add(new[] { "Class I (Distinction)", "4.40 &ndash; 5.00" });
            b.Add(new[] { "Class II (Credit)", "2.80 &ndash; 4.39" });
            b.Add(new[] { "Class III (Pass)", "2.00 &ndash; 2.79" });
        }
        else
        {
            b.Add(new[] { "First Class (Honours)", "4.40 &ndash; 5.00" });
            b.Add(new[] { "Second Class (Upper)", "3.60 &ndash; 4.39" });
            b.Add(new[] { "Second Class (Lower)", "2.80 &ndash; 3.59" });
            b.Add(new[] { "Third Class (Pass)", "2.00 &ndash; 2.79" });
        }
        return b;
    }

    private string Err(string msg)
    {
        return "<div class='tx-err'>" + msg + "</div>";
    }

    private static string H(object v) { return HttpUtility.HtmlEncode(v == null ? "" : v.ToString()); }
    private static string S(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static int I(object v) { int r; return v == null || v == DBNull.Value ? 0 : (int.TryParse(v.ToString(), out r) ? r : 0); }
    private static double D(object v) { double r; return v == null || v == DBNull.Value ? 0 : (double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out r) ? r : 0); }
}
