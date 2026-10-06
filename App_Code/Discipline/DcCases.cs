using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: lookups, opening cases, the case list
//  and the case file (plan 5.2, 5.3).
// =====================================================================
public static class DcLookups
{
    public static List<object> Campuses(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT ID, campus_name FROM acad_campuses WHERE ID>0 ORDER BY ID").Rows)
            l.Add(new { id = FaDb.I(r[0]), name = Title(FaDb.S(r[1])) });
        return l;
    }

    public static List<object> Faculties(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT faculty_code, faculty_name FROM acad_faculty WHERE IFNULL(faculty_name,'')<>'' ORDER BY faculty_name").Rows)
            l.Add(new { id = FaDb.S(r[0]).Trim(), name = Title(FaDb.S(r[1])) });
        return l;
    }

    public static List<object> Departments(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT ID, dept_name, faculty_code FROM hrm_departments WHERE IFNULL(dept_name,'')<>'' ORDER BY dept_name").Rows)
            l.Add(new { id = FaDb.I(r[0]), name = FaDb.S(r[1]), faculty = FaDb.S(r[2]).Trim() });
        return l;
    }

    public static List<object> Programmes(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null,
            "SELECT DISTINCT p.progcode, p.progname, p.faculty_code, p.department_id FROM acad_programme p JOIN dc_case x ON x.progcode=p.progcode ORDER BY p.progname").Rows)
            l.Add(new { id = FaDb.S(r[0]).Trim(), name = FaDb.S(r[1]), faculty = FaDb.S(r[2]).Trim(), dept = FaDb.I(r[3]) });
        return l;
    }

    public static List<object> CaseTypes(MySqlConnection c, bool activeOnly)
    {
        var l = new List<object>();
        DataTable defs = FaDb.Table(c, null, "SELECT case_type_id, sanction_type_id FROM dc_case_type_sanction WHERE is_active=1 ORDER BY sort_order");
        foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM dc_case_type" + (activeOnly ? " WHERE is_active=1" : "") + " ORDER BY sort_order, name").Rows)
        {
            int id = FaDb.I(r["id"]);
            var s = new List<int>();
            foreach (DataRow d in defs.Rows) if (FaDb.I(d[0]) == id) s.Add(FaDb.I(d[1]));
            l.Add(new
            {
                id = id, code = FaDb.S(r["code"]), name = FaDb.S(r["name"]), description = FaDb.S(r["description"]),
                severity = FaDb.S(r["severity"]), exam = FaDb.I(r["is_exam_related"]) == 1, restricted = FaDb.I(r["default_restricted"]) == 1,
                sort = FaDb.I(r["sort_order"]), active = FaDb.I(r["is_active"]) == 1, version = FaDb.I(r["row_version"]), sanctions = s
            });
        }
        return l;
    }

    public static List<object> SanctionTypes(MySqlConnection c, bool activeOnly)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM dc_sanction_type" + (activeOnly ? " WHERE is_active=1" : "") + " ORDER BY sort_order, name").Rows)
            l.Add(SanctionTypeJson(r));
        return l;
    }

    public static object SanctionTypeJson(DataRow r)
    {
        return new
        {
            id = FaDb.I(r["id"]), code = FaDb.S(r["code"]), name = FaDb.S(r["name"]), description = FaDb.S(r["description"]),
            outcome = FaDb.S(r["outcome"]), effects = FaDb.S(r["effects"]), effectsText = DcFmt.Effects(FaDb.S(r["effects"])),
            needsAmount = FaDb.I(r["needs_amount"]) == 1, needsDates = FaDb.S(r["needs_dates"]), needsCourse = FaDb.I(r["needs_course"]) == 1,
            needsSemester = FaDb.I(r["needs_semester"]) == 1, interim = FaDb.I(r["allowed_interim"]) == 1,
            sort = FaDb.I(r["sort_order"]), active = FaDb.I(r["is_active"]) == 1, version = FaDb.I(r["row_version"])
        };
    }

    public static List<string> AcadYears(MySqlConnection c)
    {
        var l = new List<string>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT DISTINCT acad_year FROM dc_case ORDER BY acad_year DESC").Rows) l.Add(FaDb.S(r[0]));
        string cur = DcSeq.AcadYear();
        if (!l.Contains(cur)) l.Insert(0, cur);
        return l;
    }

    public static string Title(string s)
    {
        s = (s ?? "").Trim();
        if (s == "" || s != s.ToUpperInvariant()) return s;
        string t = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        foreach (string w in new[] { "Of", "And", "The", "In", "For", "On", "At", "To", "With" })
            t = System.Text.RegularExpressions.Regex.Replace(t, @"(?<=\s)" + w + @"(?=\s)", w.ToLowerInvariant());
        return t;
    }

    // ── Searches used by the new-case form ─────────────────────────────

    private const string StudentSelect =
        "SELECT s.regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) AS name, s.progid, IFNULL(p.progname,'') AS progname, " +
        "IFNULL(p.faculty_code,'') AS faculty_code, p.department_id, s.studCampus, IFNULL(cp.campus_name,'') AS campus_name, s.entryyear, " +
        "IFNULL(s.stud_status,'') AS stud_status, IFNULL(s.new_status,'') AS new_status, IFNULL(s.email,'') AS email, IFNULL(s.studPhone,'') AS phone " +
        "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode=s.progid LEFT JOIN acad_campuses cp ON cp.ID=s.studCampus ";

    public static List<object> SearchStudents(MySqlConnection c, string q)
    {
        var l = new List<object>();
        q = (q ?? "").Trim();
        if (q.Length < 2) return l;
        DataTable t = FaDb.Table(c, null, StudentSelect +
            "WHERE s.regno LIKE @p OR s.entryno LIKE @p OR CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')) LIKE @a OR CONCAT(IFNULL(s.othername,''),' ',IFNULL(s.firstname,'')) LIKE @a " +
            "ORDER BY (s.regno=@q) DESC, s.entryyear DESC LIMIT 15", "@p", q + "%", "@a", "%" + q + "%", "@q", q);
        foreach (DataRow r in t.Rows) l.Add(StudentJson(c, r, true));
        return l;
    }

    public static DataRow Student(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        DataTable t = FaDb.Table(c, tx, StudentSelect + "WHERE s.regno=@r LIMIT 1", "@r", (regno ?? "").Trim());
        return t.Rows.Count == 0 ? null : t.Rows[0];
    }

    public static int? StudyYear(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        object o = FaDb.Scalar(c, tx, "SELECT studyyear FROM acad_registration WHERE regno=@r ORDER BY acad_year DESC, semester DESC LIMIT 1", "@r", regno);
        int y = FaDb.I(o);
        return y > 0 && y < 10 ? (int?)y : null;
    }

    /// <summary>A student for the form, with any case or restriction the user may see.</summary>
    public static object StudentJson(MySqlConnection c, DataRow r, bool withCases)
    {
        string regno = FaDb.S(r["regno"]).Trim();
        var cases = new List<object>();
        string restr = "";
        if (withCases)
        {
            DataTable t = FaDb.Table(c, null,
                "SELECT c.id, c.case_no, c.status, t.name FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id WHERE c.regno=@r" +
                DcAccess.CaseFilter("c") + " ORDER BY c.id DESC LIMIT 10", "@r", regno, DcAccess.MeParam, DcAccess.Username());
            foreach (DataRow x in t.Rows)
                cases.Add(new { id = FaDb.I(x[0]), caseNo = FaDb.S(x[1]), status = FaDb.S(x[2]), statusText = DcFmt.Status(FaDb.S(x[2])), type = FaDb.S(x[3]) });
            restr = FaDb.S(FaDb.Scalar(c, null, "SELECT dc_restriction_summary(@r)", "@r", regno));
        }
        return new
        {
            regno = regno, name = FaDb.S(r["name"]), progcode = FaDb.S(r["progid"]).Trim(), programme = FaDb.S(r["progname"]),
            faculty = FaDb.S(r["faculty_code"]).Trim(), campusId = FaDb.I(r["studCampus"]), campus = Title(FaDb.S(r["campus_name"])),
            entryYear = FaDb.I(r["entryyear"]), status = FirstNonEmpty(FaDb.S(r["new_status"]), FaDb.S(r["stud_status"])),
            cases = cases, restrictions = DcFmt.Restrictions(restr)
        };
    }

    private static string FirstNonEmpty(string a, string b) { return string.IsNullOrEmpty((a ?? "").Trim()) ? (b ?? "").Trim() : a.Trim(); }

    public static List<object> SearchStaff(MySqlConnection c, string q)
    {
        var l = new List<object>();
        q = (q ?? "").Trim();
        if (q.Length < 2) return l;
        DataTable t = FaDb.Table(c, null,
            "SELECT e.empID, e.emp_name, IFNULL(e.usernames,'') AS usernames, IFNULL(e.EMP_CODE,'') AS code, IFNULL(d.dept_name,'') AS dept " +
            "FROM hrm_employee e LEFT JOIN hrm_departments d ON d.ID=e.dept_id " +
            "WHERE (e.emp_name LIKE @a OR e.EMP_CODE LIKE @p OR e.usernames LIKE @p) AND IFNULL(e.employment_status,'') NOT IN ('TERMINATED','DISMISSED','RESIGNED','DECEASED') " +
            "ORDER BY e.emp_name LIMIT 15", "@a", "%" + q + "%", "@p", q + "%");
        foreach (DataRow r in t.Rows)
            l.Add(new { id = FaDb.I(r[0]), name = FaDb.S(r[1]), username = FaDb.S(r[2]).Trim(), code = FaDb.S(r[3]), department = FaDb.S(r[4]) });
        return l;
    }

    public static List<object> SearchTickets(MySqlConnection c, string q)
    {
        var l = new List<object>();
        q = (q ?? "").Trim();
        if (q.Length < 1) return l;
        int id; int.TryParse(q.TrimStart('#'), out id);
        DataTable t = FaDb.Table(c, null,
            "SELECT ticket_id, submitter_regno, submitter_name, issue_type, subject, status, created_at FROM campus_dynamics_portal.support_tickets " +
            "WHERE ticket_id=@id OR subject LIKE @a OR submitter_regno LIKE @p OR submitter_name LIKE @a ORDER BY ticket_id DESC LIMIT 15",
            "@id", id, "@a", "%" + q + "%", "@p", q + "%");
        foreach (DataRow r in t.Rows)
            l.Add(new
            {
                id = FaDb.I(r[0]), name = "#" + FaDb.I(r[0]) + " " + FaDb.S(r[4]), by = FaDb.S(r[2]) + (FaDb.S(r[1]) == "" ? "" : " (" + FaDb.S(r[1]) + ")"),
                type = FaDb.S(r[3]), status = FaDb.S(r[5]), date = DcFmt.Date(r[6])
            });
        return l;
    }

    public static List<object> SearchExamPapers(MySqlConnection c, string q)
    {
        var l = new List<object>();
        q = (q ?? "").Trim();
        if (q.Length < 2) return l;
        DataTable t = FaDb.Table(c, null,
            "SELECT x.ID, x.courseID, IFNULL(cb.courseName, '') AS course_name, x.acad_year, x.semester, x.ExamDate, x.StartTime, x.roomNo, x.staffCode, x.campusId, x.progcode " +
            "FROM acad_exam_timetable x LEFT JOIN acad_course cb ON cb.courseID=x.courseID " +
            "WHERE x.courseID LIKE @p OR cb.courseName LIKE @a ORDER BY x.ExamDate DESC LIMIT 20", "@p", q + "%", "@a", "%" + q + "%");
        foreach (DataRow r in t.Rows)
            l.Add(new
            {
                id = FaDb.I(r[0]), course = FaDb.S(r[1]).Trim(), name = FaDb.S(r[1]).Trim() + " " + FaDb.S(r[2]),
                acadYear = FaDb.S(r[3]).Trim(), semester = FaDb.I(r[4]), date = DcFmt.Date(r[5]), iso = DcFmt.Iso(r[5]), time = FaDb.S(r[6]),
                venue = FaDb.S(r[7]), invigilator = FaDb.S(r[8]).Trim(), campusId = FaDb.I(r[9]), progcode = FaDb.S(r[10]).Trim()
            });
        return l;
    }
}

public static class DcCases
{
    // ── Opening an incident with one case per student ────────────────

    /// <summary>
    /// json: { students:[regno], caseTypeId, severity, occurredAt, place, campusId, description, reporterName, reporterEmpId,
    ///         reporterUser, witnesses, ticketId, examTimetableId, courseCode, examAcadYear, examSemester, invigilator,
    ///         restricted, measures:[{code, reason, to}], officer, opId }
    /// </summary>
    public static object Create(Dictionary<string, object> d)
    {
        var students = new List<string>();
        object so; if (d.TryGetValue("students", out so) && so is System.Collections.IEnumerable && !(so is string))
            foreach (object x in (System.Collections.IEnumerable)so) { string s = Convert.ToString(x, CultureInfo.InvariantCulture).Trim(); if (s != "" && !students.Contains(s)) students.Add(s); }
        if (students.Count == 0) throw new DcRefusal("Add at least one student.");
        if (students.Count > 30) throw new DcRefusal("Add at most 30 students to one incident.");
        int typeId = FaJson.Int(d, "caseTypeId");
        string desc = FaJson.Str(d, "description");
        if (desc.Length < 15) throw new DcRefusal("Describe what happened in at least 15 characters.");
        DateTime occurred;
        string occ = FaJson.Str(d, "occurredAt");
        if (!DateTime.TryParse(occ, CultureInfo.InvariantCulture, DateTimeStyles.None, out occurred)) throw new DcRefusal("Give the date and time of the incident.");
        if (occurred > DateTime.Now.AddMinutes(10)) throw new DcRefusal("The incident date cannot be in the future.");
        if (occurred < DateTime.Today.AddYears(-10)) throw new DcRefusal("The incident date is too far in the past.");
        string opId = FaJson.Str(d, "opId");

        using (var c = FaDb.Open())
        {
            if (opId != "")
            {
                object prior = FaDb.Scalar(c, null, "SELECT case_id FROM dc_entry WHERE client_op_id=@o", "@o", DcAudit.Cut(opId + ":1", 40));
                if (prior != null)
                {
                    var ids = new List<int>();
                    foreach (DataRow r in FaDb.Table(c, null, "SELECT id FROM dc_case WHERE incident_id=(SELECT incident_id FROM dc_case WHERE id=@c) ORDER BY id", "@c", FaDb.I(prior)).Rows) ids.Add(FaDb.I(r[0]));
                    return new { already = true, caseIds = ids };
                }
            }
            DataTable tt = FaDb.Table(c, null, "SELECT * FROM dc_case_type WHERE id=@id AND is_active=1", "@id", typeId);
            if (tt.Rows.Count == 0) throw new DcRefusal("Choose a case type.");
            DataRow type = tt.Rows[0];
            string severity = FaJson.Str(d, "severity").ToUpperInvariant();
            if (severity != "MINOR" && severity != "SERIOUS" && severity != "GROSS") severity = FaDb.S(type["severity"]);
            bool restricted = FaJson.Bool(d, "restricted") || FaDb.I(type["default_restricted"]) == 1;
            if (FaDb.I(type["default_restricted"]) == 1 && !FaJson.Bool(d, "restricted") && !DcAccess.Current().SeeRestricted)
                restricted = true;

            var created = new List<object>();
            var caseIds = new List<int>();
            var notes = new List<long>();
            using (var tx = c.BeginTransaction())
            {
                string year = DcSeq.AcadYear();
                string me = DcAccess.Username();
                string reporterUser = FaJson.Str(d, "reporterUser"); if (reporterUser == "") reporterUser = me;
                string reporterName = FaJson.Str(d, "reporterName");
                if (reporterName == "") reporterName = FaDb.S(FaDb.Scalar(c, tx, "SELECT emp_name FROM hrm_employee WHERE usernames=@u LIMIT 1", "@u", me));
                if (reporterName == "") reporterName = me;
                int? examId = FaJson.IntN(d, "examTimetableId");
                string course = FaJson.Str(d, "courseCode");
                string examYear = FaJson.Str(d, "examAcadYear");
                int? examSem = FaJson.IntN(d, "examSemester");
                int? invEmp = null;
                if (examId.HasValue && examId.Value > 0)
                {
                    DataTable ex = FaDb.Table(c, tx, "SELECT courseID, acad_year, semester, staffCode FROM acad_exam_timetable WHERE ID=@i", "@i", examId.Value);
                    if (ex.Rows.Count == 0) throw new DcRefusal("That examination paper was not found.");
                    if (course == "") course = FaDb.S(ex.Rows[0][0]).Trim();
                    if (examYear == "") examYear = FaDb.S(ex.Rows[0][1]).Trim();
                    if (!examSem.HasValue) examSem = FaDb.I(ex.Rows[0][2]);
                    string staff = FaDb.S(ex.Rows[0][3]).Trim();
                    if (staff != "") { int iv = FaDb.I(FaDb.Scalar(c, tx, "SELECT empID FROM hrm_employee WHERE EMP_CODE=@s LIMIT 1", "@s", staff)); if (iv > 0) invEmp = iv; }
                }
                int? invPicked = FaJson.IntN(d, "invigilatorEmpId"); if (invPicked.HasValue && invPicked.Value > 0) invEmp = invPicked;
                int? ticket = FaJson.IntN(d, "ticketId");
                if (ticket.HasValue && ticket.Value > 0 && FaDb.Scalar(c, tx, "SELECT 1 FROM campus_dynamics_portal.support_tickets WHERE ticket_id=@t", "@t", ticket.Value) == null)
                    throw new DcRefusal("That complaint ticket was not found.");

                string incNo = DcSeq.IncidentNo(c, tx, year);
                long incId = FaDb.Insert(c, tx,
                    "INSERT INTO dc_incident (incident_no, case_type_id, occurred_at, place, campus_id, description, reported_by, reported_by_emp_id, reporter_name, witnesses, " +
                    "complaint_ticket_id, exam_timetable_id, course_code, exam_acad_year, exam_semester, invigilator_emp_id, created_by, created_at) " +
                    "VALUES (@no,@t,@at,@pl,@cp,@ds,@rb,@re,@rn,@w,@tk,@ex,@cc,@ey,@es,@iv,@me,NOW())",
                    "@no", incNo, "@t", typeId, "@at", occurred, "@pl", FaDb.NullIfEmpty(DcAudit.Cut(FaJson.Str(d, "place"), 200)),
                    "@cp", FaJson.IntN(d, "campusId").HasValue && FaJson.Int(d, "campusId") > 0 ? (object)FaJson.Int(d, "campusId") : DBNull.Value,
                    "@ds", desc, "@rb", DcAudit.Cut(reporterUser, 100), "@re", FaJson.IntN(d, "reporterEmpId").HasValue && FaJson.Int(d, "reporterEmpId") > 0 ? (object)FaJson.Int(d, "reporterEmpId") : DBNull.Value,
                    "@rn", DcAudit.Cut(reporterName, 150), "@w", FaDb.NullIfEmpty(FaJson.Str(d, "witnesses")),
                    "@tk", ticket.HasValue && ticket.Value > 0 ? (object)ticket.Value : DBNull.Value,
                    "@ex", examId.HasValue && examId.Value > 0 ? (object)examId.Value : DBNull.Value, "@cc", FaDb.NullIfEmpty(DcAudit.Cut(course, 25)),
                    "@ey", FaDb.NullIfEmpty(DcAudit.Cut(examYear, 9)), "@es", examSem.HasValue && examSem.Value > 0 ? (object)examSem.Value : DBNull.Value,
                    "@iv", invEmp.HasValue ? (object)invEmp.Value : DBNull.Value, "@me", me);
                DcAudit.Write(c, tx, "INCIDENT", incId, null, "CREATE", null, new { incidentNo = incNo, type = FaDb.S(type["code"]), students = students }, null,
                              "Disciplinary incident " + incNo + " reported (" + students.Count + " student" + (students.Count == 1 ? "" : "s") + ")");

                string officer = FaJson.Str(d, "officer");
                int n = 0;
                foreach (string regno in students)
                {
                    n++;
                    DataRow st = DcLookups.Student(c, tx, regno);
                    if (st == null) throw new DcRefusal("Student " + regno + " was not found.");
                    string caseNo = DcSeq.CaseNo(c, tx, year);
                    int caseId = (int)FaDb.Insert(c, tx,
                        "INSERT INTO dc_case (case_no, incident_id, regno, student_name, progcode, faculty_code, department_id, campus_id, study_year, acad_year, case_type_id, " +
                        "severity, status, is_restricted, reported_by, case_officer, last_entry_at, created_by, created_at) " +
                        "VALUES (@no,@inc,@r,@nm,@pc,@fc,@dp,@cp,@sy,@y,@t,@sv,'REPORTED',@rs,@rb,@of,NOW(),@me,NOW())",
                        "@no", caseNo, "@inc", incId, "@r", FaDb.S(st["regno"]).Trim(), "@nm", DcAudit.Cut(FaDb.S(st["name"]), 200),
                        "@pc", FaDb.NullIfEmpty(FaDb.S(st["progid"])), "@fc", FaDb.NullIfEmpty(FaDb.S(st["faculty_code"])),
                        "@dp", FaDb.I(st["department_id"]) > 0 ? (object)FaDb.I(st["department_id"]) : DBNull.Value,
                        "@cp", FaDb.I(st["studCampus"]) > 0 ? (object)FaDb.I(st["studCampus"]) : DBNull.Value,
                        "@sy", DcLookups.StudyYear(c, tx, regno).HasValue ? (object)DcLookups.StudyYear(c, tx, regno).Value : DBNull.Value,
                        "@y", year, "@t", typeId, "@sv", severity, "@rs", restricted ? 1 : 0, "@rb", DcAudit.Cut(reporterUser, 100),
                        "@of", FaDb.NullIfEmpty(DcAudit.Cut(officer, 100)), "@me", me);
                    DcAudit.Write(c, tx, "CASE", caseId, caseId, "CREATE", null,
                        new { caseNo = caseNo, regno = regno, type = FaDb.S(type["code"]), severity = severity, restricted = restricted }, null,
                        "Disciplinary case " + caseNo + " opened for " + regno);

                    string studentBody = restricted
                        ? "A matter of " + DcFmt.Lc(FaDb.S(type["name"])) + " has been reported. The " + DcSettings.ContactOffice + " will contact you."
                        : "Alleged: " + FaDb.S(type["name"]) + ", on " + occurred.ToString("d MMM yyyy", CultureInfo.InvariantCulture) +
                          (FaJson.Str(d, "place") == "" ? "" : " at " + FaJson.Str(d, "place")) + ".\n\n" + desc;
                    DcEntries.Add(c, tx, caseId, "CASE_OPENED", "Case opened: " + FaDb.S(type["name"]), studentBody, true, null, "REPORTED",
                                  null, null, new { incidentNo = incNo, severity = severity, restricted = restricted, reporter = reporterName }, opId == "" ? null : opId + ":" + n, occurred);

                    // Interim measures requested on the form.
                    object mo;
                    if (d.TryGetValue("measures", out mo) && mo is System.Collections.IEnumerable && !(mo is string))
                    {
                        DataRow cs = FaDb.Table(c, tx, "SELECT c.*, t.name AS type_name, t.code AS type_code, t.is_exam_related FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id WHERE c.id=@id", "@id", caseId).Rows[0];
                        foreach (object m in (System.Collections.IEnumerable)mo)
                        {
                            var md = m as Dictionary<string, object>;
                            if (md == null) continue;
                            DcWorkflow.InterimOnCreate(c, tx, cs, FaJson.Str(md, "code"), FaJson.Str(md, "reason"), FaJson.Date(md, "to"));
                        }
                    }

                    notes.Add(DcNotify.Queue(c, tx, caseId, regno, "CASE_OPENED",
                        "A disciplinary case has been opened",
                        restricted ? "A disciplinary matter (case " + caseNo + ") has been recorded. The " + DcSettings.ContactOffice + " will contact you."
                                   : "Case " + caseNo + " has been opened about " + DcFmt.Lc(FaDb.S(type["name"])) + " on " + occurred.ToString("d MMM yyyy", CultureInfo.InvariantCulture) +
                                     ". You will be told of any hearing. You can follow the case under My Disciplinary Cases on the student portal.", null));
                    caseIds.Add(caseId);
                    created.Add(new { id = caseId, caseNo = caseNo, regno = regno, name = FaDb.S(st["name"]) });
                }
                tx.Commit();
            }
            DcNotify.SendAsync(notes);
            return new { incident = true, caseIds = caseIds, cases = created };
        }
    }

    // ── The list ────────────────────────────────────────────────────

    private static string Where(Dictionary<string, object> f, List<object> p)
    {
        var sb = new StringBuilder(" WHERE 1=1");
        sb.Append(DcAccess.CaseFilter("c"));
        p.Add(DcAccess.MeParam); p.Add(DcAccess.Username());
        Action<string, string, object> add = (sql, name, val) => { sb.Append(sql); p.Add(name); p.Add(val); };
        string v;
        if ((v = FaJson.Str(f, "campus")) != "") add(" AND c.campus_id=@campus", "@campus", FaJson.Int(f, "campus"));
        if ((v = FaJson.Str(f, "faculty")) != "") add(" AND c.faculty_code=@fac", "@fac", v);
        if ((v = FaJson.Str(f, "dept")) != "") add(" AND c.department_id=@dept", "@dept", FaJson.Int(f, "dept"));
        if ((v = FaJson.Str(f, "prog")) != "") add(" AND c.progcode=@prog", "@prog", v);
        if ((v = FaJson.Str(f, "type")) != "") add(" AND c.case_type_id=@type", "@type", FaJson.Int(f, "type"));
        if ((v = FaJson.Str(f, "severity")) != "") add(" AND c.severity=@sev", "@sev", v);
        if ((v = FaJson.Str(f, "year")) != "") add(" AND c.acad_year=@yr", "@yr", v);
        if ((v = FaJson.Str(f, "reporter")) != "") add(" AND c.reported_by=@rep", "@rep", v);
        if ((v = FaJson.Str(f, "regno")) != "") add(" AND c.regno=@regno", "@regno", v);
        if ((v = FaJson.Str(f, "incident")) != "") add(" AND c.incident_id=@inc", "@inc", FaJson.Int(f, "incident"));
        DateTime? from = FaJson.Date(f, "from"), to = FaJson.Date(f, "to");
        if (from.HasValue) add(" AND c.created_at>=@from", "@from", from.Value);
        if (to.HasValue) add(" AND c.created_at<@to", "@to", to.Value.AddDays(1));
        v = FaJson.Str(f, "status");
        if (v == "open") sb.Append(" AND c.status NOT IN ('CLOSED','WITHDRAWN')");
        else if (v == "awaiting_hearing") sb.Append(" AND c.status IN ('REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED')");
        else if (v == "awaiting_decision") sb.Append(" AND c.status='HEARD'");
        else if (v != "") add(" AND c.status=@st", "@st", v);
        switch (FaJson.Str(f, "flag"))
        {
            case "overdue":
                add(" AND c.status NOT IN ('CLOSED','WITHDRAWN') AND c.last_entry_at < DATE_SUB(NOW(), INTERVAL @od DAY)", "@od", DcSettings.Int("overdue_no_update_days", 14));
                break;
            case "hearing_passed":
                sb.Append(" AND c.status IN ('HEARING_SCHEDULED','SUMMONED') AND c.hearing_at < NOW()");
                break;
            case "appeal_closing":
                add(" AND c.status='DECIDED' AND c.appeal_deadline BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL @ac DAY)", "@ac", DcSettings.Int("appeal_closing_days", 3));
                break;
            case "sanction":
                sb.Append(" AND EXISTS (SELECT 1 FROM dc_sanction s WHERE s.case_id=c.id AND s.status='ACTIVE' AND s.starts_on<=CURDATE() AND (s.ends_on IS NULL OR s.ends_on>=CURDATE()))");
                break;
            case "follow_marks":
                sb.Append(" AND EXISTS (SELECT 1 FROM dc_sanction s WHERE s.case_id=c.id AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED') AND (FIND_IN_SET('CANCEL_PAPER',s.effects)>0 OR FIND_IN_SET('CANCEL_SEMESTER',s.effects)>0))");
                break;
            case "follow_fees":
                sb.Append(" AND EXISTS (SELECT 1 FROM dc_sanction s WHERE s.case_id=c.id AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED') AND (FIND_IN_SET('FINE',s.effects)>0 OR FIND_IN_SET('RESTITUTION',s.effects)>0))");
                break;
            case "follow":
                sb.Append(" AND EXISTS (SELECT 1 FROM dc_sanction s WHERE s.case_id=c.id AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED'))");
                break;
            case "restricted":
                sb.Append(" AND c.is_restricted=1");
                break;
            case "unread":
                sb.Append(" AND EXISTS (SELECT 1 FROM dc_notification n WHERE n.case_id=c.id AND n.portal_read_at IS NULL AND n.created_at < DATE_SUB(NOW(), INTERVAL 7 DAY))");
                break;
            case "email_failed":
                sb.Append(" AND EXISTS (SELECT 1 FROM dc_notification n WHERE n.case_id=c.id AND n.email_status IN ('FAILED','NO_ADDRESS'))");
                break;
        }
        string sanction = FaJson.Str(f, "effect");
        if (sanction != "")
            add(" AND EXISTS (SELECT 1 FROM dc_sanction s WHERE s.case_id=c.id AND s.status='ACTIVE' AND FIND_IN_SET(@eff,s.effects)>0 AND s.starts_on<=CURDATE() AND (s.ends_on IS NULL OR s.ends_on>=CURDATE()))", "@eff", sanction);
        string q = FaJson.Str(f, "q");
        if (q != "")
        {
            sb.Append(" AND (c.case_no LIKE @qp OR c.regno LIKE @qp OR c.student_name LIKE @qa)");
            p.Add("@qp"); p.Add(q + "%"); p.Add("@qa"); p.Add("%" + q + "%");
        }
        return sb.ToString();
    }

    public const string ListSelect =
        "SELECT c.id, c.case_no, c.regno, c.student_name, c.progcode, IFNULL(p.progname,'') AS progname, c.faculty_code, c.campus_id, IFNULL(cp.campus_name,'') AS campus_name, " +
        "c.acad_year, c.study_year, t.name AS type_name, t.is_exam_related, c.severity, c.status, c.is_restricted, c.hearing_at, c.hearing_venue, c.last_entry_at, c.created_at, " +
        "c.decided_at, c.appeal_deadline, c.reported_by, c.case_officer, c.decision_summary, i.incident_no, i.occurred_at, i.place, " +
        "(SELECT GROUP_CONCAT(st.name ORDER BY st.sort_order SEPARATOR ', ') FROM dc_sanction s JOIN dc_sanction_type st ON st.id=s.sanction_type_id " +
        "  WHERE s.case_id=c.id AND s.status='ACTIVE' AND s.starts_on<=CURDATE() AND (s.ends_on IS NULL OR s.ends_on>=CURDATE())) AS in_force, " +
        "(SELECT GROUP_CONCAT(st.name ORDER BY s.id SEPARATOR ', ') FROM dc_sanction s JOIN dc_sanction_type st ON st.id=s.sanction_type_id " +
        "  WHERE s.case_id=c.id AND s.source<>'INTERIM' AND s.status NOT IN ('VARIED','SET_ASIDE')) AS decided_sanctions, " +
        "(SELECT COUNT(*) FROM dc_sanction s WHERE s.case_id=c.id AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED')) AS follow_ups " +
        "FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id JOIN dc_incident i ON i.id=c.incident_id " +
        "LEFT JOIN acad_programme p ON p.progcode=c.progcode LEFT JOIN acad_campuses cp ON cp.ID=c.campus_id ";

    public static DataTable Query(MySqlConnection c, Dictionary<string, object> f, int page, int size, out int total)
    {
        var p = new List<object>();
        string where = Where(f, p);
        string order;
        switch (FaJson.Str(f, "sort"))
        {
            case "oldest": order = " ORDER BY c.id"; break;
            case "hearing": order = " ORDER BY c.hearing_at IS NULL, c.hearing_at, c.id"; break;
            case "updated": order = " ORDER BY c.last_entry_at DESC, c.id DESC"; break;
            case "student": order = " ORDER BY c.student_name, c.id"; break;
            default: order = " ORDER BY c.id DESC"; break;
        }
        string limit = size > 0 ? " LIMIT " + Math.Max(0, (page - 1) * size) + "," + size : "";
        DataTable t = FaDb.Table(c, null, "SELECT SQL_CALC_FOUND_ROWS " + ListSelect.Substring(7) + where + order + limit, p.ToArray());
        total = FaDb.I(FaDb.Scalar(c, null, "SELECT FOUND_ROWS()"));
        return t;
    }

    public static int Count(MySqlConnection c, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string where = Where(f, p);
        return FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*) FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id " + where, p.ToArray()));
    }

    public static object RowJson(DataRow r)
    {
        return new
        {
            id = FaDb.I(r["id"]), caseNo = FaDb.S(r["case_no"]), regno = FaDb.S(r["regno"]), name = FaDb.S(r["student_name"]),
            progcode = FaDb.S(r["progcode"]), programme = FaDb.S(r["progname"]), campus = DcLookups.Title(FaDb.S(r["campus_name"])),
            year = FaDb.S(r["acad_year"]), type = FaDb.S(r["type_name"]), exam = FaDb.I(r["is_exam_related"]) == 1,
            severity = FaDb.S(r["severity"]), severityText = DcFmt.Severity(FaDb.S(r["severity"])),
            status = FaDb.S(r["status"]), statusText = DcFmt.Status(FaDb.S(r["status"])), restricted = FaDb.I(r["is_restricted"]) == 1,
            hearing = DcFmt.When(r["hearing_at"]), hearingIso = DcFmt.IsoDT(r["hearing_at"]), venue = FaDb.S(r["hearing_venue"]),
            updated = DcFmt.Date(r["last_entry_at"]), opened = DcFmt.Date(r["created_at"]), occurred = DcFmt.Date(r["occurred_at"]),
            appealDeadline = DcFmt.Date(r["appeal_deadline"]), officer = FaDb.S(r["case_officer"]), reporter = FaDb.S(r["reported_by"]),
            inForce = FaDb.S(r["in_force"]), followUps = FaDb.I(r["follow_ups"]), incidentNo = FaDb.S(r["incident_no"])
        };
    }

    // ── The case file ───────────────────────────────────────────────

    public static object Detail(int caseId)
    {
        using (var c = FaDb.Open())
        {
            DataRow cs = DcAccess.VisibleCase(c, null, caseId, false);
            if (cs == null) throw new DcRefusal("That case was not found.");
            DcAccess.LogRestrictedAccess(c, cs, "VIEW");
            DataRow inc = FaDb.Table(c, null,
                "SELECT i.*, IFNULL(cp.campus_name,'') AS campus_name, IFNULL(e.emp_name,'') AS invigilator, " +
                "t.subject AS ticket_subject, t.status AS ticket_status " +
                "FROM dc_incident i LEFT JOIN acad_campuses cp ON cp.ID=i.campus_id LEFT JOIN hrm_employee e ON e.empID=i.invigilator_emp_id " +
                "LEFT JOIN campus_dynamics_portal.support_tickets t ON t.ticket_id=i.complaint_ticket_id WHERE i.id=@i", "@i", FaDb.I(cs["incident_id"])).Rows[0];
            DataRow st = DcLookups.Student(c, null, FaDb.S(cs["regno"]));

            var entries = new List<object>();
            DataTable atts = FaDb.Table(c, null, "SELECT * FROM dc_attachment WHERE case_id=@c ORDER BY id", "@c", caseId);
            foreach (DataRow e in FaDb.Table(c, null, "SELECT * FROM dc_entry WHERE case_id=@c ORDER BY entry_at, id", "@c", caseId).Rows)
            {
                int eid = FaDb.I(e["id"]);
                var ea = new List<object>();
                foreach (DataRow a in atts.Rows) if (FaDb.I(a["entry_id"]) == eid && FaDb.I(a["is_active"]) == 1) ea.Add(AttJson(a));
                entries.Add(new
                {
                    id = eid, type = FaDb.S(e["entry_type"]), typeText = DcFmt.EntryType(FaDb.S(e["entry_type"])), at = DcFmt.When(e["entry_at"]),
                    title = FaDb.S(e["title"]), body = FaDb.S(e["body"]), visible = FaDb.I(e["student_visible"]) == 1,
                    statusBefore = DcFmt.Status(FaDb.S(e["status_before"])), statusAfter = DcFmt.Status(FaDb.S(e["status_after"])),
                    corrects = FaDb.I(e["corrects_entry_id"]), sanctionId = FaDb.I(e["sanction_id"]),
                    by = FaDb.S(e["recorded_by"]), role = FaDb.S(e["recorded_role"]), recorded = DcFmt.When(e["recorded_at"]),
                    via = FaDb.S(e["interface"]), ip = FaDb.S(e["ip_address"]), attachments = ea
                });
            }
            var sanctions = new List<object>();
            foreach (DataRow s in FaDb.Table(c, null,
                "SELECT s.*, t.name, t.code FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.case_id=@c ORDER BY s.id", "@c", caseId).Rows)
                sanctions.Add(SanctionJson(s));
            var hearings = new List<object>();
            foreach (DataRow h in FaDb.Table(c, null, "SELECT * FROM dc_hearing WHERE case_id=@c ORDER BY scheduled_at, id", "@c", caseId).Rows)
                hearings.Add(new
                {
                    id = FaDb.I(h["id"]), at = DcFmt.When(h["scheduled_at"]), iso = DcFmt.IsoDT(h["scheduled_at"]), venue = FaDb.S(h["venue"]), panel = FaDb.S(h["panel"]),
                    status = FaDb.S(h["status"]), attended = h["student_attended"] == DBNull.Value ? "" : (FaDb.I(h["student_attended"]) == 1 ? "Present" : "Absent")
                });
            var letters = new List<object>();
            foreach (DataRow l in FaDb.Table(c, null, "SELECT * FROM dc_letter WHERE case_id=@c ORDER BY id", "@c", caseId).Rows)
                letters.Add(new { id = FaDb.I(l["id"]), no = FaDb.S(l["letter_no"]), template = FaDb.S(l["template_code"]), subject = FaDb.S(l["subject"]),
                                  issued = DcFmt.When(l["issued_at"]), by = FaDb.S(l["issued_by"]), visible = FaDb.I(l["student_visible"]) == 1 });
            var appeals = new List<object>();
            foreach (DataRow a in FaDb.Table(c, null, "SELECT * FROM dc_appeal WHERE case_id=@c ORDER BY id", "@c", caseId).Rows)
                appeals.Add(new { id = FaDb.I(a["id"]), lodged = DcFmt.When(a["lodged_at"]), via = FaDb.S(a["lodged_via"]), by = FaDb.S(a["lodged_by"]),
                                  grounds = FaDb.S(a["grounds"]), withinWindow = FaDb.I(a["within_window"]) == 1, lateReason = FaDb.S(a["late_reason"]),
                                  status = FaDb.S(a["status"]), decided = DcFmt.Date(a["decided_at"]), decision = FaDb.S(a["decision_text"]) });
            var attachments = new List<object>();
            foreach (DataRow a in atts.Rows) attachments.Add(AttJson(a));
            var audit = new List<object>();
            foreach (DataRow a in FaDb.Table(c, null, "SELECT * FROM dc_audit WHERE case_id=@c ORDER BY id DESC LIMIT 500", "@c", caseId).Rows)
                audit.Add(new { at = DcFmt.When(a["created_at"]), entity = FaDb.S(a["entity"]), action = FaDb.S(a["action"]), before = FaDb.S(a["before_json"]),
                                after = FaDb.S(a["after_json"]), reason = FaDb.S(a["reason"]), actor = FaDb.S(a["actor"]), role = FaDb.S(a["actor_role"]),
                                via = FaDb.S(a["interface"]), ip = FaDb.S(a["ip_address"]) });
            var notices = new List<object>();
            foreach (DataRow n in FaDb.Table(c, null, "SELECT * FROM dc_notification WHERE case_id=@c ORDER BY id DESC", "@c", caseId).Rows)
                notices.Add(new { id = FaDb.I(n["id"]), title = FaDb.S(n["title"]), at = DcFmt.When(n["created_at"]), read = DcFmt.When(n["portal_read_at"]),
                                  email = FaDb.S(n["email_status"]), emailTo = FaDb.S(n["email_to"]), emailError = FaDb.S(n["email_error"]) });
            var linked = new List<object>();
            foreach (DataRow l in FaDb.Table(c, null, "SELECT c.id, c.case_no, c.regno, c.student_name, c.status FROM dc_case c WHERE c.incident_id=@i AND c.id<>@id" +
                                                DcAccess.CaseFilter("c") + " ORDER BY c.id", "@i", FaDb.I(cs["incident_id"]), "@id", caseId, DcAccess.MeParam, DcAccess.Username()).Rows)
                linked.Add(new { id = FaDb.I(l[0]), caseNo = FaDb.S(l[1]), regno = FaDb.S(l[2]), name = FaDb.S(l[3]), statusText = DcFmt.Status(FaDb.S(l[4])) });
            var history = new List<object>();
            foreach (DataRow h in FaDb.Table(c, null, "SELECT c.id, c.case_no, c.status, t.name FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id WHERE c.regno=@r AND c.id<>@id" +
                                                DcAccess.CaseFilter("c") + " ORDER BY c.id DESC", "@r", FaDb.S(cs["regno"]), "@id", caseId, DcAccess.MeParam, DcAccess.Username()).Rows)
                history.Add(new { id = FaDb.I(h[0]), caseNo = FaDb.S(h[1]), statusText = DcFmt.Status(FaDb.S(h[2])), type = FaDb.S(h[3]) });

            return new
            {
                @case = new
                {
                    id = caseId, caseNo = FaDb.S(cs["case_no"]), regno = FaDb.S(cs["regno"]), name = FaDb.S(cs["student_name"]),
                    progcode = FaDb.S(cs["progcode"]), programme = st == null ? "" : FaDb.S(st["progname"]), campus = st == null ? "" : DcLookups.Title(FaDb.S(st["campus_name"])),
                    email = st == null ? "" : FaDb.S(st["email"]), phone = st == null ? "" : FaDb.S(st["phone"]),
                    studentStatus = st == null ? "" : FaDb.S(st["new_status"]) != "" ? FaDb.S(st["new_status"]) : FaDb.S(st["stud_status"]),
                    year = FaDb.S(cs["acad_year"]), studyYear = FaDb.I(cs["study_year"]), typeId = FaDb.I(cs["case_type_id"]), type = FaDb.S(cs["type_name"]),
                    exam = FaDb.I(cs["is_exam_related"]) == 1, severity = FaDb.S(cs["severity"]), severityText = DcFmt.Severity(FaDb.S(cs["severity"])),
                    status = FaDb.S(cs["status"]), statusText = DcFmt.Status(FaDb.S(cs["status"])), restricted = FaDb.I(cs["is_restricted"]) == 1,
                    reporter = FaDb.S(cs["reported_by"]), officer = FaDb.S(cs["case_officer"]), hearing = DcFmt.When(cs["hearing_at"]),
                    hearingIso = DcFmt.IsoDT(cs["hearing_at"]), venue = FaDb.S(cs["hearing_venue"]), decided = DcFmt.Date(cs["decided_at"]),
                    decision = FaDb.S(cs["decision_summary"]), notified = DcFmt.When(cs["notified_at"]), appealDeadline = DcFmt.Date(cs["appeal_deadline"]),
                    appealOpen = FaDb.S(cs["status"]) == "DECIDED" && FaDb.D(cs["appeal_deadline"]).HasValue && FaDb.D(cs["appeal_deadline"]).Value >= DateTime.Today,
                    closed = DcFmt.When(cs["closed_at"]), opened = DcFmt.When(cs["created_at"]), version = FaDb.I(cs["row_version"]),
                    restrictions = DcFmt.Restrictions(FaDb.S(FaDb.Scalar(c, null, "SELECT dc_restriction_summary(@r)", "@r", FaDb.S(cs["regno"]))))
                },
                incident = new
                {
                    no = FaDb.S(inc["incident_no"]), occurred = DcFmt.When(inc["occurred_at"]), place = FaDb.S(inc["place"]), campus = DcLookups.Title(FaDb.S(inc["campus_name"])),
                    description = FaDb.S(inc["description"]), reporter = FaDb.S(inc["reporter_name"]), witnesses = FaDb.S(inc["witnesses"]),
                    ticketId = FaDb.I(inc["complaint_ticket_id"]), ticket = FaDb.S(inc["ticket_subject"]), ticketStatus = FaDb.S(inc["ticket_status"]),
                    course = FaDb.S(inc["course_code"]), examYear = FaDb.S(inc["exam_acad_year"]), examSemester = FaDb.I(inc["exam_semester"]),
                    invigilator = FaDb.S(inc["invigilator"])
                },
                entries = entries, sanctions = sanctions, hearings = hearings, letters = letters, appeals = appeals, attachments = attachments,
                audit = audit, notices = notices, linked = linked, history = history,
                actions = DcWorkflow.Actions(cs),
                sanctionTypes = DcWorkflow.SanctionChoices(c, FaDb.I(cs["case_type_id"])),
                templates = DcLetters.TemplateChoices(c)
            };
        }
    }

    public static object AttJson(DataRow a)
    {
        return new
        {
            id = FaDb.I(a["id"]), entryId = FaDb.I(a["entry_id"]), kind = FaDb.S(a["kind"]), description = FaDb.S(a["description"]), name = FaDb.S(a["original_name"]),
            size = FaDb.I(a["size_bytes"]), visible = FaDb.I(a["student_visible"]) == 1, by = FaDb.S(a["uploaded_by"]), via = FaDb.S(a["uploaded_via"]),
            at = DcFmt.When(a["uploaded_at"]), active = FaDb.I(a["is_active"]) == 1, removedReason = FaDb.S(a["removed_reason"])
        };
    }

    public static object SanctionJson(DataRow s)
    {
        DateTime? st = FaDb.D(s["starts_on"]), en = FaDb.D(s["ends_on"]);
        bool inForce = FaDb.S(s["status"]) == "ACTIVE" && st.HasValue && st.Value <= DateTime.Today && (!en.HasValue || en.Value >= DateTime.Today);
        return new
        {
            id = FaDb.I(s["id"]), name = FaDb.S(s["name"]), code = FaDb.S(s["code"]), effects = FaDb.S(s["effects"]), effectsText = DcFmt.Effects(FaDb.S(s["effects"])),
            source = FaDb.S(s["source"]), sourceText = DcFmt.Source(FaDb.S(s["source"])), amount = s["amount"] == DBNull.Value ? "" : DcFmt.Money(s["amount"]),
            from = DcFmt.Date(s["starts_on"]), to = DcFmt.Date(s["ends_on"]), fromIso = DcFmt.Iso(s["starts_on"]), toIso = DcFmt.Iso(s["ends_on"]),
            course = FaDb.S(s["course_code"]), year = FaDb.S(s["acad_year"]), semester = FaDb.I(s["semester"]), terms = FaDb.S(s["terms"]),
            status = FaDb.S(s["status"]), statusText = DcFmt.SanctionStatus(FaDb.S(s["status"])), inForce = inForce,
            pending = st.HasValue && st.Value > DateTime.Today && FaDb.S(s["status"]) == "ACTIVE",
            followUp = FaDb.S(s["follow_up"]), followRef = FaDb.S(s["follow_up_ref"]),
            ended = DcFmt.When(s["ended_at"]), endedBy = FaDb.S(s["ended_by"]), endedReason = FaDb.S(s["ended_reason"]), replaces = FaDb.I(s["replaces_id"])
        };
    }

    // ── Record Updates feed (plan 5.4) ──────────────────────────────

    public static DataTable Feed(MySqlConnection c, Dictionary<string, object> f, int page, int size, out int total)
    {
        var p = new List<object>();
        var sb = new StringBuilder(
            "SELECT SQL_CALC_FOUND_ROWS e.id, e.case_id, e.entry_type, e.entry_at, e.title, e.body, e.student_visible, e.recorded_by, e.recorded_role, e.recorded_at, e.interface, " +
            "c.case_no, c.regno, c.student_name, c.status, t.name AS type_name, c.faculty_code " +
            "FROM dc_entry e JOIN dc_case c ON c.id=e.case_id JOIN dc_case_type t ON t.id=c.case_type_id WHERE 1=1");
        sb.Append(DcAccess.CaseFilter("c")); p.Add(DcAccess.MeParam); p.Add(DcAccess.Username());
        DateTime? from = FaJson.Date(f, "from"), to = FaJson.Date(f, "to");
        if (from.HasValue) { sb.Append(" AND e.recorded_at>=@from"); p.Add("@from"); p.Add(from.Value); }
        if (to.HasValue) { sb.Append(" AND e.recorded_at<@to"); p.Add("@to"); p.Add(to.Value.AddDays(1)); }
        string v;
        if ((v = FaJson.Str(f, "entryType")) != "") { sb.Append(" AND e.entry_type=@et"); p.Add("@et"); p.Add(v); }
        if ((v = FaJson.Str(f, "type")) != "") { sb.Append(" AND c.case_type_id=@ct"); p.Add("@ct"); p.Add(FaJson.Int(f, "type")); }
        if ((v = FaJson.Str(f, "faculty")) != "") { sb.Append(" AND c.faculty_code=@fac"); p.Add("@fac"); p.Add(v); }
        if ((v = FaJson.Str(f, "officer")) != "") { sb.Append(" AND e.recorded_by=@ob"); p.Add("@ob"); p.Add(v); }
        if ((v = FaJson.Str(f, "q")) != "") { sb.Append(" AND (c.case_no LIKE @qp OR c.regno LIKE @qp OR c.student_name LIKE @qa OR e.title LIKE @qa)"); p.Add("@qp"); p.Add(v + "%"); p.Add("@qa"); p.Add("%" + v + "%"); }
        if (!FaJson.Bool(f, "system")) sb.Append(" AND e.entry_type<>'STUDENT_NOTIFIED'");
        sb.Append(" ORDER BY e.recorded_at DESC, e.id DESC");
        if (size > 0) sb.Append(" LIMIT " + Math.Max(0, (page - 1) * size) + "," + size);
        DataTable t = FaDb.Table(c, null, sb.ToString(), p.ToArray());
        total = FaDb.I(FaDb.Scalar(c, null, "SELECT FOUND_ROWS()"));
        return t;
    }
}
