using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// ID Card module — business layer (identity, finance gate, windows, submit,
/// list/detail/stats, action dispatch, email). Every public method returns a JSON
/// string ready for a WebMethod / API endpoint, and is context-free: the caller
/// (eadmin / eportal / XAXU API) supplies the actor + channel. All state changes
/// go through IDCardService.Transition (the funnel in IDCardService.cs).
///
/// Decisions locked in COOPERP/IDCARD_WORKFLOW_DESIGN.md §14a.
/// </summary>
public static partial class IDCardService
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();
    private const int PASS_PCT = 10;   // student must have paid >= 10% of the semester fee

    // ==================================================================
    // IDENTITY  (Step 1 display)
    // ==================================================================
    public static string IdentityJson(string type, string regno, int empId)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                object idn = Identity(conn, type, regno, empId);
                if (idn == null) return J.Serialize(new { success = false, message = "Record not found." });
                object win = WindowState(conn, type);
                string active = ActiveRequestNo(conn, type, regno, empId);
                return J.Serialize(new { success = true, identity = idn, window = win, activeRequest = active });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    private static Dictionary<string, object> Identity(MySqlConnection conn, string type, string regno, int empId)
    {
        var d = new Dictionary<string, object>();
        if (Norm(type) == "STAFF")
        {
            using (var cmd = new MySqlCommand(
                "SELECT e.empID, TRIM(e.emp_name) nm, IFNULL(e.emp_email,'') email, IFNULL(e.photo_file,'') photo," +
                " IFNULL((SELECT dept_name FROM hrm_departments dd WHERE dd.dept_headID=e.empID LIMIT 1),'') dept" +
                " FROM hrm_employee e WHERE e.empID=@e LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@e", empId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    d["type"] = "STAFF"; d["number"] = S(r["empID"]); d["name"] = S(r["nm"]);
                    d["email"] = S(r["email"]); d["photo"] = S(r["photo"]); d["subtitle"] = S(r["dept"]);
                    d["hasPhoto"] = !string.IsNullOrEmpty(S(r["photo"])) && S(r["photo"]) != "-";
                }
            }
        }
        else
        {
            using (var cmd = new MySqlCommand(
                "SELECT s.regno, IFNULL(NULLIF(TRIM(s.entryno),''),s.regno) studno," +
                " TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) nm, IFNULL(s.email,'') email," +
                " IFNULL(s.photofile,'') photo, IFNULL(s.photo_status,'') photostatus, s.progid, IFNULL(p.progname,s.progid) pn," +
                " IFNULL(s.studsesion,'') sess, IFNULL(s.entryyear,'') entryyr, IFNULL(cp.campus_name,'') campus," +
                " IFNULL(s.gender,'') gender, IFNULL(s.studPhone,'') phone, IFNULL(s.intake,'') intake," +
                " IFNULL(s.nationality,'') nationality, IFNULL(s.stud_status,'') sstatus" +
                " FROM acad_student s LEFT JOIN acad_programme p ON p.progcode=s.progid" +
                " LEFT JOIN acad_campuses cp ON cp.ID=s.studCampus WHERE s.regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno ?? "");
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    d["type"] = "STUDENT"; d["number"] = S(r["studno"]); d["regno"] = S(r["regno"]);
                    d["name"] = S(r["nm"]); d["email"] = S(r["email"]); d["photo"] = S(r["photo"]);
                    d["photoStatus"] = S(r["photostatus"]);
                    d["programme"] = S(r["pn"]); d["session"] = S(r["sess"]);
                    d["entryYear"] = S(r["entryyr"]); d["campus"] = S(r["campus"]);
                    d["gender"] = S(r["gender"]); d["phone"] = S(r["phone"]); d["intake"] = S(r["intake"]);
                    d["nationality"] = S(r["nationality"]); d["studStatus"] = S(r["sstatus"]);
                    d["subtitle"] = S(r["pn"]) + (S(r["sess"]) != "" ? (" (" + S(r["sess"]) + ")") : "");
                    string ph = S(r["photo"]);
                    d["hasPhoto"] = !string.IsNullOrEmpty(ph) && ph != "-";
                }
            }
        }
        return d;
    }

    private static string RecipientEmail(MySqlConnection conn, string type, string regno, int empId)
    {
        string sql = Norm(type) == "STAFF"
            ? "SELECT emp_email FROM hrm_employee WHERE empID=@k LIMIT 1"
            : "SELECT email FROM acad_student WHERE regno=@k LIMIT 1";
        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@k", Norm(type) == "STAFF" ? (object)empId : (regno ?? ""));
            object v = cmd.ExecuteScalar();
            return (v == null || v == DBNull.Value) ? "" : v.ToString().Trim();
        }
    }

    // ==================================================================
    // WINDOWS
    // ==================================================================
    private static Dictionary<string, object> WindowState(MySqlConnection conn, string type)
    {
        var d = new Dictionary<string, object>();
        int total = 0;
        using (var c = new MySqlCommand("SELECT COUNT(*) FROM idcard_windows", conn)) total = Convert.ToInt32(c.ExecuteScalar());
        if (total == 0) { d["open"] = true; d["id"] = 0; d["message"] = "Requests are open."; return d; }  // bootstrap: no windows = open

        using (var cmd = new MySqlCommand(
            "SELECT id, title, closes_at FROM idcard_windows WHERE is_active=1 AND (requester_scope='BOTH' OR requester_scope=@t)" +
            " AND NOW() BETWEEN opens_at AND closes_at ORDER BY closes_at DESC LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@t", Norm(type));
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read()) { d["open"] = true; d["id"] = ToI(r["id"]); d["title"] = S(r["title"]); d["closesAt"] = S(r["closes_at"]); d["message"] = "Window open: " + S(r["title"]); return d; }
            }
        }
        // find the next upcoming window for a friendly message
        string next = "";
        using (var cmd = new MySqlCommand(
            "SELECT title, opens_at FROM idcard_windows WHERE is_active=1 AND (requester_scope='BOTH' OR requester_scope=@t)" +
            " AND opens_at>NOW() ORDER BY opens_at ASC LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@t", Norm(type));
            using (var r = cmd.ExecuteReader()) if (r.Read()) next = S(r["title"]) + " opens " + S(r["opens_at"]);
        }
        d["open"] = false; d["id"] = 0;
        d["message"] = "ID card requests are currently closed." + (next != "" ? (" Next: " + next) : "");
        return d;
    }

    public static string WindowsJson()
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                var list = new List<object>();
                using (var cmd = new MySqlCommand("SELECT id,title,requester_scope,opens_at,closes_at,is_active,IFNULL(notes,'') notes," +
                    " CASE WHEN is_active=1 AND NOW() BETWEEN opens_at AND closes_at THEN 1 ELSE 0 END is_open" +
                    " FROM idcard_windows ORDER BY opens_at DESC LIMIT 200", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(new { id = ToI(r["id"]), title = S(r["title"]), scope = S(r["requester_scope"]),
                        opensAt = S(r["opens_at"]), closesAt = S(r["closes_at"]), active = ToI(r["is_active"]) == 1, open = ToI(r["is_open"]) == 1, notes = S(r["notes"]) });
                return J.Serialize(new { success = true, windows = list });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    public static string CreateWindowJson(string title, string scope, string opensAt, string closesAt, string notes, string actor)
    {
        try
        {
            if (string.IsNullOrEmpty((title ?? "").Trim())) return Bad("A window title is required.");
            DateTime o, c;
            if (!DateTime.TryParse(opensAt, out o) || !DateTime.TryParse(closesAt, out c)) return Bad("Valid open and close dates are required.");
            if (c <= o) return Bad("The close date must be after the open date.");
            string sc = Norm(scope); if (sc != "STUDENT" && sc != "STAFF") sc = "BOTH";
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                using (var cmd = new MySqlCommand("INSERT INTO idcard_windows (title,requester_scope,opens_at,closes_at,is_active,notes,created_by,created_at)" +
                    " VALUES (@t,@s,@o,@c,1,@n,@by,NOW())", conn))
                {
                    cmd.Parameters.AddWithValue("@t", title.Trim());
                    cmd.Parameters.AddWithValue("@s", sc);
                    cmd.Parameters.AddWithValue("@o", o);
                    cmd.Parameters.AddWithValue("@c", c);
                    cmd.Parameters.AddWithValue("@n", (object)(notes ?? ""));
                    cmd.Parameters.AddWithValue("@by", actor ?? "");
                    cmd.ExecuteNonQuery();
                }
                return J.Serialize(new { success = true, message = "Window created." });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    public static string SetWindowActiveJson(int id, bool active)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("UPDATE idcard_windows SET is_active=@a WHERE id=@id", conn))
                { cmd.Parameters.AddWithValue("@a", active ? 1 : 0); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
                return J.Serialize(new { success = true, message = active ? "Window activated." : "Window closed." });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    // ==================================================================
    // PHOTO GATE (students only)
    //
    // An ID card is a photograph with a name on it, so the photograph has to
    // exist and have been approved before a card can be requested. Approving it
    // afterwards would mean cards queued for printing with nothing to print.
    //
    // The state lives on acad_student: photo_status (defaulting to APPROVED for
    // the long-standing photos that pre-date the approval workflow) plus
    // photo_banned for students barred from changing their picture at all.
    // A student can be APPROVED and still have no picture — 1,529 of the 3,286
    // registered students are in exactly that position — so having a file is
    // checked separately from the status, and is the more common obstacle.
    // ==================================================================
    private class PhotoGate
    {
        public bool Ok;
        public string State = "";       // OK | MISSING | PENDING | REJECTED | BANNED
        public string Message = "";
        public string Reason = "";      // reviewer's comment, when there is one
        public string Action = "";      // what the student should do next
    }

    private static PhotoGate RunPhoto(MySqlConnection conn, string regno)
    {
        var g = new PhotoGate();
        string status = "APPROVED", file = ""; bool banned = false; string banReason = "";

        using (var cmd = new MySqlCommand(
            "SELECT COALESCE(NULLIF(TRIM(photo_status),''),'APPROVED'), COALESCE(photofile,''), " +
            "       COALESCE(photo_banned,0), COALESCE(photo_ban_reason,'') " +
            "FROM acad_student WHERE regno=@r LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@r", regno ?? "");
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    status = S(r[0]).ToUpperInvariant();
                    file = S(r[1]);
                    banned = S(r[2]) == "1";
                    banReason = S(r[3]);
                }
        }

        bool hasFile = file != "" && file != "-" && !file.Equals("default.png", StringComparison.OrdinalIgnoreCase);

        if (banned)
        {
            g.State = "BANNED";
            g.Message = "Your photo has been locked by the administration, so an ID card cannot be requested.";
            g.Reason = banReason;
            g.Action = "Please visit the Academic Registrar's office.";
            return g;
        }
        if (status == "REJECTED")
        {
            g.State = "REJECTED";
            g.Message = "Your photo was not accepted, so it cannot be printed on a card.";
            using (var cmd = new MySqlCommand(
                "SELECT COALESCE(review_comment,'') FROM stud_photo_change WHERE regno=@r AND status='REJECTED' " +
                "ORDER BY id DESC LIMIT 1", conn))
            { cmd.Parameters.AddWithValue("@r", regno ?? ""); object v = cmd.ExecuteScalar(); g.Reason = v == null ? "" : S(v); }
            g.Action = "Upload a clear passport photo, wait for it to be approved, then come back.";
            return g;
        }
        if (status == "PENDING")
        {
            g.State = "PENDING";
            g.Message = "Your photo is waiting to be approved.";
            g.Action = "You can request your card as soon as it is approved — usually within a working day.";
            return g;
        }
        if (!hasFile)
        {
            g.State = "MISSING";
            g.Message = "There is no photo on your record, and a card cannot be printed without one.";
            g.Action = "Upload a clear passport photo. Once it is approved you can request your card.";
            return g;
        }

        g.Ok = true; g.State = "OK";
        g.Message = "Photo approved.";
        return g;
    }

    private static object PhotoObj(PhotoGate g)
    {
        return new { ok = g.Ok, state = g.State, message = g.Message, reason = g.Reason, action = g.Action };
    }

    // ==================================================================
    // FINANCE GATE (students only; 10% of current-semester fee)
    // ==================================================================
    private class Finance { public double Fee; public double Paid; public double Required; public bool Eligible; public bool Flagged;
        public string AcadYear = ""; public int Semester; public int StudyYear; public string Prog = "";
        public bool Enrolled = true;    // false when no enrolled registration backs the semester below
        public bool HasStudent = true;  // false when there is no student record to measure at all
        public string State = "OK"; }   // OK | BELOW | NO_FEE_STRUCTURE | NO_RECORD

    private static Finance RunFinance(MySqlConnection conn, string regno)
    {
        var f = new Finance();

        // The programme is the student's, not the registration's — it is needed in every
        // case below, including the one where there is no registration to join to.
        using (var cmd = new MySqlCommand("SELECT IFNULL(progid,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@r", regno ?? "");
            object v = cmd.ExecuteScalar();
            if (v == null) { f.HasStudent = false; f.Flagged = true; f.Eligible = true; f.State = "NO_RECORD"; return f; }
            f.Prog = Convert.ToString(v);
        }

        // WHICH SEMESTER IS THE FEE MEASURED AGAINST?
        //
        // An enrolled registration answers it outright, and is preferred. When there is
        // none the question is still answerable, and it must be answered rather than
        // refused: admission activates a student's account and leaves semester
        // registration for later, so a newly admitted student legitimately has NO
        // acad_registration row at all. 103 of the 2026 intake are in exactly that
        // position — the intake that most needs an ID card.
        //
        // That case used to return here with a fee of 0, a requirement of 0 and
        // Eligible=false, so the wizard told the student to pay 10% of 0 and reported a
        // shortfall of 0. It refused them for failing a test it had not managed to set.
        //
        // A non-enrolled row still says which semester the student belongs to, so it is
        // used when present; failing everything, the current academic year at year one,
        // semester one — which is what a fresh admission is about to be billed for.
        bool found = false;
        using (var cmd = new MySqlCommand(
            "SELECT r.acad_year, r.semester, r.studyyear," +
            "       (r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED')) AS enrolled" +
            "  FROM acad_registration r WHERE r.regno=@r" +
            " ORDER BY (r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED')) DESC," +
            "          r.acad_year DESC, r.semester DESC LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@r", regno ?? "");
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    found = true;
                    f.AcadYear = S(r["acad_year"]); f.Semester = ToI(r["semester"]);
                    f.StudyYear = ToI(r["studyyear"]); f.Enrolled = ToI(r["enrolled"]) == 1;
                }
        }
        if (!found)
        {
            f.Enrolled = false;
            f.StudyYear = 1; f.Semester = 1;
            using (var cmd = new MySqlCommand(
                "SELECT acadyear FROM acad_acadyears WHERE is_current_year='Yes' ORDER BY acadyear DESC LIMIT 1", conn))
            {
                object v = cmd.ExecuteScalar();
                f.AcadYear = v == null || v == DBNull.Value ? "" : Convert.ToString(v);
            }
        }
        // Not being registered is not a pass, but it is a fact the ID office should see.
        if (!f.Enrolled) f.Flagged = true;
        // semester fee via the accounts DB procedure
        double tuition = 0, functional = 0;
        try
        {
            using (var ac = new MySqlConnection(AccountsConnStr))
            {
                ac.Open();
                // Read the fee cells directly from fin_programme_fees (same cells fin_GetProgrammeFee
                // uses) — avoids stored-proc OUT-param handling. yN_sM columns; N/M are validated ints.
                if (f.StudyYear >= 1 && f.StudyYear <= 4 && f.Semester >= 1 && f.Semester <= 3)
                {
                    string yc = "y" + f.StudyYear + "_s" + f.Semester;
                    // Resolved by the student's own study session (fin_ProgFeeRow): a programme
                    // may hold both a MAIN and an INSERVICE structure, and "WHERE progcode=X
                    // LIMIT 1" would pick an arbitrary one — measuring an in-service student's
                    // 10% against the day rate, or the reverse.
                    using (var cmd = new MySqlCommand("SELECT IFNULL(" + yc + "_tuition,0), IFNULL(" + yc + "_functional,0)" +
                        " FROM fin_programme_fees" +
                        " WHERE ID = fin_ProgFeeRow(@p, (SELECT s.studsesion" +
                        "                                FROM campus_dynamics.acad_student s" +
                        "                                WHERE s.regno=@rg LIMIT 1))", ac))
                    {
                        cmd.Parameters.AddWithValue("@p", f.Prog);
                        cmd.Parameters.AddWithValue("@rg", regno);
                        using (var r = cmd.ExecuteReader())
                            if (r.Read()) { tuition = r[0] == DBNull.Value ? 0 : Convert.ToDouble(r[0]); functional = r[1] == DBNull.Value ? 0 : Convert.ToDouble(r[1]); }
                    }
                }
                // ── What the student has actually put toward this semester ──────────────
                //
                // This used to read ONLY fin_studentfeestracking rows tagged with the exact
                // acadyear + semester, and it under-reported badly:
                //
                //  • A payment that lands BEFORE the semester's bills are raised is written with
                //    semester=0 and a blank acadyear, because at capture time there is nothing to
                //    tag it to. 188 students in the current year hold such payments — 105.5m
                //    shillings that the check simply could not see. MRU2026004755 paid 647,000 at
                //    12:37 and was billed at 13:47 the same day, so the gate read "paid 0".
                //  • 95 more students have credit in the ledger with no tracking row at all.
                //
                // Both groups were told to pay a fee they had already paid. The gate is a 10%
                // threshold, not the fee-clearance rule, so it must not be stricter than reality.
                //
                // Two independent measures are taken and the HIGHER wins, which cannot
                // double-count and cannot under-report when one source is incomplete:
                //   A — tracking payments for this semester, PLUS untagged payments, which are
                //       unallocated money and therefore available to this semester;
                //   B — credits on the student's own ledger account since the academic year began.
                //
                // Bursaries and waivers count: a fully sponsored student owes nothing and must not
                // be blocked. Reversals and balance-fix adjustments are excluded — they are
                // corrections, not money the student put in.
                double paidTracking = 0, paidLedger = 0;

                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(SUM(amount),0) FROM fin_studentfeestracking" +
                    " WHERE regno=@r AND trans_type='Payment'" +
                    "   AND ( (acadyear=@ay AND semester=@sm)" +
                    "      OR semester=0 OR TRIM(IFNULL(acadyear,''))='' )", ac))
                {
                    cmd.Parameters.AddWithValue("@r", regno ?? "");
                    cmd.Parameters.AddWithValue("@ay", f.AcadYear);
                    cmd.Parameters.AddWithValue("@sm", f.Semester);
                    object v = cmd.ExecuteScalar(); paidTracking = v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v);
                }

                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(SUM(l.transaction_amount),0) FROM fin_ledger l" +
                    " WHERE l.accountcode=@r AND l.transactionType='CR'" +
                    "   AND l.transactionDate >= @start" +
                    "   AND l.particulars NOT LIKE 'Reversal%'" +
                    "   AND l.particulars NOT LIKE '%Balance Fix%'", ac))
                {
                    cmd.Parameters.AddWithValue("@r", regno ?? "");
                    cmd.Parameters.AddWithValue("@start", AcadYearStart(conn, f.AcadYear));
                    object v = cmd.ExecuteScalar(); paidLedger = v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v);
                }

                f.Paid = Math.Max(paidTracking, paidLedger);
            }
        }
        catch { f.Flagged = true; }

        f.Fee = tuition + functional;
        f.Required = Math.Round(f.Fee * PASS_PCT / 100.0, 0);
        // A threshold that cannot be computed cannot be failed. Where there is no fee
        // structure the student is let through and flagged for review, exactly as before —
        // the one thing never done again is refusing someone against a requirement of zero.
        if (f.Fee <= 0) { f.Eligible = true; f.Flagged = true; f.State = "NO_FEE_STRUCTURE"; }
        else { f.Eligible = f.Paid >= f.Required; f.State = f.Eligible ? "OK" : "BELOW"; }
        return f;
    }

    /// <summary>
    /// When the academic year began, from acad_acadyears (2026/2027 starts 2026-08-01). Falls
    /// back to 1 August of the leading year in the label, and to a wide-open date if even that
    /// cannot be read — this window must never be the reason a student who has paid is refused.
    /// </summary>
    private static DateTime AcadYearStart(MySqlConnection conn, string acadYear)
    {
        try
        {
            using (var cmd = new MySqlCommand("SELECT start_date FROM acad_acadyears WHERE acadyear=@a LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@a", acadYear ?? "");
                object v = cmd.ExecuteScalar();
                if (v != null && v != DBNull.Value) return Convert.ToDateTime(v);
            }
        }
        catch { }
        int y;
        if (!string.IsNullOrEmpty(acadYear) && acadYear.Length >= 4 && int.TryParse(acadYear.Substring(0, 4), out y))
            return new DateTime(y, 8, 1);
        return new DateTime(2000, 1, 1);
    }

    private static object FinanceObj(Finance f)
    {
        return new { fee = f.Fee, paid = f.Paid, requiredPct = PASS_PCT, required = f.Required, eligible = f.Eligible,
            flagged = f.Flagged, acadYear = f.AcadYear, semester = f.Semester, studyYear = f.StudyYear, prog = f.Prog,
            state = f.State, enrolled = f.Enrolled, hasStudent = f.HasStudent,
            shortfall = f.Eligible ? 0 : Math.Max(0, f.Required - f.Paid) };
    }

    /// <summary>The two student gates in one call — the wizard needs both to decide what to show,
    /// and asking for them separately would let the screen render half a verdict.</summary>
    public static string FinanceJson(string regno)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                return J.Serialize(new
                {
                    success = true,
                    finance = FinanceObj(RunFinance(conn, regno)),
                    photo = PhotoObj(RunPhoto(conn, regno))
                });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    // ==================================================================
    // CREATE  (Step 1 complete → REQUESTED)
    // ==================================================================
    public static string CreateJson(string type, string regno, int empId, string cardType,
        string photoRef, bool photoConfirmed, bool guidelinesAck, string actor)
    {
        try
        {
            if (!photoConfirmed) return Bad("Please confirm your photo before continuing.");
            if (!guidelinesAck) return Bad("Please confirm you have read the photo guidelines.");
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                var w = (Dictionary<string, object>)WindowState(conn, type);
                if (!(bool)w["open"]) return Bad(S(w["message"]));
            }
            int wid = 0;
            using (var conn2 = new MySqlConnection(ConnStr)) { conn2.Open(); var w2 = (Dictionary<string, object>)WindowState(conn2, type); wid = ToI(w2["id"]); }
            CreateResult cr = CreateRequest(type, regno, empId, cardType, photoRef, photoConfirmed, guidelinesAck, wid, actor);
            return J.Serialize(new { success = cr.Ok, requestNo = cr.RequestNo, message = cr.Message });
        }
        catch (Exception ex) { return Err(ex); }
    }

    // ==================================================================
    // SUBMIT  (Step 2: finance gate + replacement fee → SUBMITTED / BLOCKED)
    // ==================================================================
    public static string SubmitJson(string requestNo, string replRef, string replDate, string replMethod, string replNotes, string actor)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                string type, regno, cardType, status; int empId;
                if (!LoadBasics(conn, requestNo, out type, out regno, out empId, out cardType, out status))
                    return Bad("Request not found.");
                if (status != REQUESTED && status != BLOCKED && status != HALTED)
                    return Bad("This request can no longer be submitted (status " + status + ").");

                // replacement fee proof (both students and staff replacements)
                if (cardType == "REPLACEMENT")
                {
                    if (string.IsNullOrEmpty((replRef ?? "").Trim())) return Bad("Enter the replacement-fee payment reference (UGX 30,000 to XAXU).");
                    string m = Norm(replMethod); if (m != "DFCU" && m != "AIRTEL") return Bad("Select the payment method (DFCU Bank or Airtel Money).");
                    DateTime pd; if (!DateTime.TryParse(replDate, out pd)) return Bad("Enter a valid payment date.");
                    using (var up = new MySqlCommand("UPDATE idcard_requests SET replacement_fee_ref=@r, replacement_fee_date=@d," +
                        " replacement_fee_method=@m, replacement_fee_notes=@n, updated_at=NOW() WHERE request_no=@rn", conn))
                    {
                        up.Parameters.AddWithValue("@r", replRef.Trim());
                        up.Parameters.AddWithValue("@d", pd);
                        up.Parameters.AddWithValue("@m", m);
                        up.Parameters.AddWithValue("@n", (object)(replNotes ?? ""));
                        up.Parameters.AddWithValue("@rn", requestNo);
                        up.ExecuteNonQuery();
                    }
                }

                // HALTED resubmit -> straight back to SUBMITTED (fixes already made)
                if (status == HALTED)
                {
                    TransitionResult t0 = Transition(requestNo, SUBMITTED, actor, "requester", "eportal", "Resubmitted after halt", null);
                    return J.Serialize(new { success = t0.Ok, status = t0.Status, message = t0.Ok ? "Resubmitted." : t0.Message });
                }

                // STAFF: no finance gate
                if (Norm(type) == "STAFF")
                {
                    TransitionResult ts = Transition(requestNo, SUBMITTED, actor, "staff", "eportal", "Submitted", null);
                    return J.Serialize(new { success = ts.Ok, status = ts.Status, message = ts.Ok ? "Submitted for approval." : ts.Message });
                }

                // STUDENT: the photo must exist and be approved before anything else. Checked here
                // as well as in the wizard, because the wizard only decides what to show — this is
                // the only place that decides what is allowed.
                PhotoGate pg = RunPhoto(conn, regno);
                if (!pg.Ok)
                    return J.Serialize(new { success = false, photoBlocked = true, photo = PhotoObj(pg),
                        message = pg.Message + (pg.Action == "" ? "" : " " + pg.Action) });

                // STUDENT: run finance, persist snapshot, gate
                Finance f = RunFinance(conn, regno);
                using (var up = new MySqlCommand("UPDATE idcard_requests SET finance_ok=@ok, finance_snapshot_json=@snap, updated_at=NOW() WHERE request_no=@rn", conn))
                {
                    up.Parameters.AddWithValue("@ok", f.Eligible ? 1 : 0);
                    up.Parameters.AddWithValue("@snap", J.Serialize(FinanceObj(f)));
                    up.Parameters.AddWithValue("@rn", requestNo);
                    up.ExecuteNonQuery();
                }
                // move through FINANCE_CHECK, then SUBMITTED or BLOCKED
                Transition(requestNo, FINANCE_CHECK, actor, "student", "eportal", "Finance check", null);
                if (f.Eligible)
                {
                    TransitionResult ts = Transition(requestNo, SUBMITTED, actor, "student", "eportal",
                        f.Flagged ? "Submitted (fee structure unresolved — flagged)" : "Submitted (finance cleared)", null);
                    return J.Serialize(new { success = ts.Ok, status = ts.Status, finance = FinanceObj(f), message = ts.Ok ? "Submitted for approval." : ts.Message });
                }
                else
                {
                    // Same rule as the wizard shows: never quote a requirement of zero.
                    string why = f.Required > 0
                        ? "You must pay at least " + PASS_PCT + "% of your semester fee (" + f.Required.ToString("N0") + ") first. Paid so far: " + f.Paid.ToString("N0") + "."
                        : "Your fees for this semester could not be read, so this check could not be completed. Please contact the Bursar's office.";
                    Transition(requestNo, BLOCKED, actor, "student", "eportal",
                        f.Required > 0 ? "Below " + PASS_PCT + "% of semester fee" : "Semester fee could not be read", null);
                    return J.Serialize(new { success = false, blocked = true, status = BLOCKED, finance = FinanceObj(f),
                        message = why });
                }
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    // ==================================================================
    // ACTIONS  (eadmin / API) → the Transition funnel
    // ==================================================================
    public static string ActionJson(string requestNo, string action, string reason, string collectionPoint,
        string actor, string role, string channel)
    {
        try
        {
            string a = (action ?? "").Trim().ToUpperInvariant();
            string to;
            switch (a)
            {
                case "APPROVE":   to = APPROVED;  break;
                case "HALT":      to = HALTED;    break;
                case "PRINTED":   to = PRINTED;   break;
                case "READY":     to = READY;     break;
                case "COLLECTED": to = COLLECTED; break;
                case "CANCEL":    to = CANCELLED; break;
                default: return Bad("Unknown action.");
            }
            if (to == READY && !string.IsNullOrEmpty((collectionPoint ?? "").Trim()))
                using (var conn = new MySqlConnection(ConnStr)) { conn.Open();
                    using (var up = new MySqlCommand("UPDATE idcard_requests SET collection_point=@cp, updated_at=NOW() WHERE request_no=@rn", conn))
                    { up.Parameters.AddWithValue("@cp", collectionPoint.Trim()); up.Parameters.AddWithValue("@rn", requestNo); up.ExecuteNonQuery(); } }

            TransitionResult t = Transition(requestNo, to, actor, role, channel ?? "eadmin", null, reason);
            return J.Serialize(new { success = t.Ok, status = t.Status, message = t.Message });
        }
        catch (Exception ex) { return Err(ex); }
    }

    // ==================================================================
    // LIST / DETAIL / STATS / MY-REQUEST
    // ==================================================================
    // Back-compat thin wrapper (WebMethods / XAXU queue). Delegates to ListJsonEx.
    public static string ListJson(string status, string type, string cardType, string q, int page, int pageSize)
    {
        var f = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(status)) f["status"] = status;
        if (!string.IsNullOrEmpty(type)) f["type"] = type;
        if (!string.IsNullOrEmpty(cardType)) f["card_type"] = cardType;
        if (!string.IsNullOrEmpty(q)) f["q"] = q;
        return ListJsonEx(f, page, pageSize, "created_at", "desc");
    }

    /// <summary>
    /// Rich, paginated, sortable listing. Filter keys (all optional):
    /// status (single or CSV -> IN), type, card_type, q, date_from, date_to (on created_at),
    /// window_id, finance (ok|below|flagged), has_replacement_fee (0|1).
    /// sort: created_at|submitted_at|updated_at|status|request_no. order: asc|desc.
    /// </summary>
    public static string ListJsonEx(Dictionary<string, string> f, int page, int pageSize, string sort, string order)
    {
        try
        {
            if (f == null) f = new Dictionary<string, string>();
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            string sortCol;
            switch ((sort ?? "").Trim().ToLowerInvariant())
            {
                case "submitted_at": sortCol = "req.submitted_at"; break;
                case "updated_at":   sortCol = "req.updated_at";   break;
                case "status":       sortCol = "req.status";       break;
                case "request_no":   sortCol = "req.request_no";   break;
                case "created_at":   sortCol = "req.created_at";   break;
                default:             sortCol = "req.id";           break;
            }
            string ord = ((order ?? "").Trim().ToLowerInvariant() == "asc") ? "ASC" : "DESC";

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                var where = new StringBuilder(" WHERE 1=1");
                var ps = new List<MySqlParameter>();

                string status = Get(f, "status");
                if (!string.IsNullOrEmpty(status))
                {
                    string[] sts = status.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (sts.Length == 1) { where.Append(" AND req.status=@st"); ps.Add(new MySqlParameter("@st", sts[0].Trim().ToUpperInvariant())); }
                    else
                    {
                        var names = new List<string>();
                        for (int i = 0; i < sts.Length; i++) { string pn = "@st" + i; names.Add(pn); ps.Add(new MySqlParameter(pn, sts[i].Trim().ToUpperInvariant())); }
                        where.Append(" AND req.status IN (" + string.Join(",", names.ToArray()) + ")");
                    }
                }
                string type = Get(f, "type");
                if (!string.IsNullOrEmpty(type)) { where.Append(" AND req.requester_type=@ty"); ps.Add(new MySqlParameter("@ty", Norm(type))); }
                string cardType = Get(f, "card_type");
                if (!string.IsNullOrEmpty(cardType)) { where.Append(" AND req.card_type=@ct"); ps.Add(new MySqlParameter("@ct", Norm(cardType))); }
                string q = Get(f, "q");
                if (!string.IsNullOrEmpty(q)) { where.Append(" AND (req.request_no LIKE @q OR req.regno LIKE @q OR EXISTS(SELECT 1 FROM acad_student s2 WHERE s2.regno=req.regno AND (s2.entryno LIKE @q OR CONCAT(IFNULL(s2.firstname,''),' ',IFNULL(s2.othername,'')) LIKE @q)) OR EXISTS(SELECT 1 FROM hrm_employee e2 WHERE e2.empID=req.emp_id AND e2.emp_name LIKE @q))"); ps.Add(new MySqlParameter("@q", "%" + q + "%")); }
                string df = Get(f, "date_from");
                if (!string.IsNullOrEmpty(df)) { where.Append(" AND DATE(req.created_at) >= @df"); ps.Add(new MySqlParameter("@df", df)); }
                string dt = Get(f, "date_to");
                if (!string.IsNullOrEmpty(dt)) { where.Append(" AND DATE(req.created_at) <= @dt"); ps.Add(new MySqlParameter("@dt", dt)); }
                string wid = Get(f, "window_id");
                if (!string.IsNullOrEmpty(wid)) { int wi; if (int.TryParse(wid, out wi) && wi > 0) { where.Append(" AND req.window_id=@wid"); ps.Add(new MySqlParameter("@wid", wi)); } }
                string fin = Get(f, "finance").ToLowerInvariant();
                if (fin == "ok") where.Append(" AND req.finance_ok=1");
                else if (fin == "below") where.Append(" AND req.finance_ok=0");
                else if (fin == "flagged") where.Append(" AND req.finance_snapshot_json LIKE '%\"flagged\":true%'");
                // Year of entry. Students only — staff have no entry year, so naming one here
                // excludes them rather than silently returning staff rows the filter cannot
                // describe. Accepts a CSV so a bureau can print two intakes in one run.
                string ey = Get(f, "entry_year");
                if (!string.IsNullOrEmpty(ey))
                {
                    string[] yrs = ey.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var names = new List<string>();
                    for (int i = 0; i < yrs.Length; i++)
                    {
                        int yv;
                        if (!int.TryParse(yrs[i].Trim(), out yv) || yv < 1900 || yv > 2200) continue;
                        string pn = "@ey" + i; names.Add(pn); ps.Add(new MySqlParameter(pn, yv));
                    }
                    if (names.Count > 0)
                        where.Append(" AND EXISTS(SELECT 1 FROM acad_student sy WHERE sy.regno=req.regno" +
                                     " AND sy.entryyear IN (" + string.Join(",", names.ToArray()) + "))");
                }

                string hrf = Get(f, "has_replacement_fee");
                if (hrf == "1") where.Append(" AND req.replacement_fee_ref IS NOT NULL AND TRIM(req.replacement_fee_ref)<>''");
                else if (hrf == "0") where.Append(" AND (req.replacement_fee_ref IS NULL OR TRIM(req.replacement_fee_ref)='')");

                int total;
                using (var cnt = new MySqlCommand("SELECT COUNT(*) FROM idcard_requests req" + where, conn))
                { foreach (var p in ps) cnt.Parameters.AddWithValue(p.ParameterName, p.Value); total = Convert.ToInt32(cnt.ExecuteScalar()); }

                var rows = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT req.request_no, req.requester_type, req.card_type, req.status, req.created_at, req.submitted_at, req.updated_at," +
                    " COALESCE(NULLIF(TRIM(s.entryno),''), req.regno) studno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) sname," +
                    " IFNULL(s.photofile,'') sphoto, IFNULL(s.photo_status,'') sphotostatus, IFNULL(e.photo_file,'') ephoto," +
                    " e.empID, TRIM(e.emp_name) ename FROM idcard_requests req" +
                    " LEFT JOIN acad_student s ON s.regno=req.regno LEFT JOIN hrm_employee e ON e.empID=req.emp_id" +
                    where + " ORDER BY " + sortCol + " " + ord + ", req.id " + ord + " LIMIT @off,@ps", conn))
                {
                    foreach (var p in ps) cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                    cmd.Parameters.AddWithValue("@off", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@ps", pageSize);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            bool staff = S(r["requester_type"]) == "STAFF";
                            rows.Add(new { requestNo = S(r["request_no"]), type = S(r["requester_type"]), cardType = S(r["card_type"]),
                                status = S(r["status"]), createdAt = S(r["created_at"]), submittedAt = S(r["submitted_at"]), updatedAt = S(r["updated_at"]),
                                number = staff ? S(r["empID"]) : S(r["studno"]), name = staff ? S(r["ename"]) : S(r["sname"]),
                                photo = staff ? S(r["ephoto"]) : S(r["sphoto"]), photoStatus = staff ? "" : S(r["sphotostatus"]) });
                        }
                }
                int pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
                int fromN = total == 0 ? 0 : (page - 1) * pageSize + 1;
                int toN = (int)Math.Min((long)total, (long)page * pageSize);
                return J.Serialize(new { success = true, rows = rows, total = total, page = page, pages = pages,
                    page_size = pageSize, has_prev = page > 1, has_next = page < pages, from = fromN, to = toN });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    public static string DetailJson(string requestNo)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                Dictionary<string, object> req = null; string type = "", regno = null; int empId = 0;
                using (var cmd = new MySqlCommand("SELECT * FROM idcard_requests WHERE request_no=@rn LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@rn", requestNo ?? "");
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return Bad("Request not found.");
                        req = new Dictionary<string, object>();
                        for (int i = 0; i < r.FieldCount; i++) req[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i).ToString();
                        type = S(req.ContainsKey("requester_type") ? req["requester_type"] : ""); regno = req.ContainsKey("regno") ? (string)req["regno"] : null;
                        int.TryParse(S(req.ContainsKey("emp_id") ? req["emp_id"] : ""), out empId);
                    }
                }
                object identity = Identity(conn, type, regno, empId);
                object finance = null;
                if (req.ContainsKey("finance_snapshot_json") && req["finance_snapshot_json"] != null)
                    try { finance = J.DeserializeObject((string)req["finance_snapshot_json"]); } catch { }
                var events = new List<object>();
                using (var cmd = new MySqlCommand("SELECT from_status,to_status,IFNULL(actor,'') actor,IFNULL(actor_role,'') role,IFNULL(channel,'') channel,IFNULL(note,'') note,created_at FROM idcard_request_events WHERE request_id=@id ORDER BY id ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@id", req["id"]);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) events.Add(new { from = S(r["from_status"]), to = S(r["to_status"]), actor = S(r["actor"]), role = S(r["role"]), channel = S(r["channel"]), note = S(r["note"]), at = S(r["created_at"]) });
                }
                return J.Serialize(new { success = true, request = req, identity = identity, finance = finance, timeline = events });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    public static string MyRequestJson(string type, string regno, int empId)
    {
        try
        {
            string active2;
            Eligibility el;
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                active2 = ActiveRequestNo(conn, type, regno, empId);
                el = CheckEligibility(conn, type, regno, empId);
            }

            /* The eligibility answer travels WITH the request, because the tracker is
               where a stuck requester actually looks. Someone sitting at PRINTED — the
               2,486 carried over from OmniPass, who never placed the request in the
               first place — used to be shown a finished progress bar and nothing else.
               They can ask for a replacement, and the tracker has to be able to say so. */
            if (active2 == null)
                return J.Serialize(new
                {
                    success = true,
                    hasRequest = false,
                    canRequest = el.CanRequest,
                    hasCard = el.HasCard,
                    suggestedCardType = el.SuggestedCardType,
                    eligibilityMessage = el.Message
                });

            string detail = DetailJson(active2);
            var d = (Dictionary<string, object>)J.DeserializeObject(detail);
            d["hasRequest"] = true;
            d["canRequest"] = el.CanRequest;
            d["canEdit"] = el.CanEditBlocking;
            d["hasCard"] = el.HasCard;
            d["suggestedCardType"] = el.SuggestedCardType;
            d["eligibilityMessage"] = el.Message;
            return J.Serialize(d);
        }
        catch (Exception ex) { return Err(ex); }
    }

    public static string StatsJson() { return StatsJson(null, null, null); }
    public static string StatsJson(string dateFrom, string dateTo) { return StatsJson(dateFrom, dateTo, null); }

    /// <summary>
    /// Funnel counts (optionally within a created_at date range and one or more years of entry)
    /// plus type, card and year-of-entry breakdowns.
    ///
    /// The year-of-entry breakdown carries its own funnel — a bureau planning a print run needs
    /// to know which intake the outstanding work belongs to, and a bare total per year does not
    /// answer that. Staff requests have no entry year and are reported as their own row rather
    /// than dropped, because they still have to be printed.
    /// </summary>
    public static string StatsJson(string dateFrom, string dateTo, string entryYear)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                var where = new StringBuilder(" WHERE 1=1"); var ps = new List<MySqlParameter>();
                if (!string.IsNullOrEmpty(dateFrom)) { where.Append(" AND DATE(req.created_at) >= @df"); ps.Add(new MySqlParameter("@df", dateFrom)); }
                if (!string.IsNullOrEmpty(dateTo)) { where.Append(" AND DATE(req.created_at) <= @dt"); ps.Add(new MySqlParameter("@dt", dateTo)); }
                if (!string.IsNullOrEmpty(entryYear))
                {
                    string[] yrs = entryYear.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var names = new List<string>();
                    for (int i = 0; i < yrs.Length; i++)
                    {
                        int yv;
                        if (!int.TryParse(yrs[i].Trim(), out yv) || yv < 1900 || yv > 2200) continue;
                        string pn = "@sy" + i; names.Add(pn); ps.Add(new MySqlParameter(pn, yv));
                    }
                    if (names.Count > 0)
                        where.Append(" AND EXISTS(SELECT 1 FROM acad_student sy2 WHERE sy2.regno=req.regno" +
                                     " AND sy2.entryyear IN (" + string.Join(",", names.ToArray()) + "))");
                }

                var counts = new Dictionary<string, int>();
                using (var cmd = new MySqlCommand("SELECT req.status, COUNT(*) c FROM idcard_requests req" + where + " GROUP BY req.status", conn))
                { foreach (var p in ps) cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                  using (var r = cmd.ExecuteReader()) while (r.Read()) counts[S(r["status"])] = ToI(r["c"]); }
                int total = 0; foreach (var v in counts.Values) total += v;

                int stu = 0, stf = 0, cnew = 0, crep = 0;
                using (var cmd = new MySqlCommand("SELECT req.requester_type, COUNT(*) c FROM idcard_requests req" + where + " GROUP BY req.requester_type", conn))
                { foreach (var p in ps) cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                  using (var r = cmd.ExecuteReader()) while (r.Read()) { if (S(r["requester_type"]) == "STAFF") stf = ToI(r["c"]); else stu = ToI(r["c"]); } }
                using (var cmd = new MySqlCommand("SELECT req.card_type, COUNT(*) c FROM idcard_requests req" + where + " GROUP BY req.card_type", conn))
                { foreach (var p in ps) cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                  using (var r = cmd.ExecuteReader()) while (r.Read()) { if (S(r["card_type"]) == "REPLACEMENT") crep = ToI(r["c"]); else cnew = ToI(r["c"]); } }

                // Year of entry, with the funnel positions a print bureau actually plans around:
                // waiting (approved, not yet printed), printed, ready for collection, collected.
                var byYear = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT COALESCE(sy.entryyear, 0) AS ey, COUNT(*) AS total," +
                    " SUM(req.status='APPROVED') AS waiting," +
                    " SUM(req.status='PRINTED')  AS printed," +
                    " SUM(req.status='READY')    AS ready," +
                    " SUM(req.status='COLLECTED') AS collected," +
                    " SUM(req.requester_type='STAFF') AS staff" +
                    " FROM idcard_requests req" +
                    " LEFT JOIN acad_student sy ON sy.regno = req.regno" +
                    where +
                    " GROUP BY COALESCE(sy.entryyear, 0) ORDER BY ey DESC", conn))
                {
                    foreach (var p in ps) cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            int y = ToI(r["ey"]);
                            byYear.Add(new
                            {
                                year = y,
                                label = y > 0 ? y.ToString() : "No entry year on record",
                                total = ToI(r["total"]),
                                waiting = ToI(r["waiting"]),
                                printed = ToI(r["printed"]),
                                ready = ToI(r["ready"]),
                                collected = ToI(r["collected"]),
                                staff = ToI(r["staff"])
                            });
                        }
                }

                return J.Serialize(new { success = true, total = total,
                    requested = G(counts, REQUESTED), finance_check = G(counts, FINANCE_CHECK), blocked = G(counts, BLOCKED), submitted = G(counts, SUBMITTED),
                    approved = G(counts, APPROVED), halted = G(counts, HALTED), printed = G(counts, PRINTED),
                    ready = G(counts, READY), collected = G(counts, COLLECTED), cancelled = G(counts, CANCELLED),
                    byType = new { student = stu, staff = stf }, byCard = new { newCard = cnew, replacement = crep },
                    byEntryYear = byYear });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>
    /// Apply one action to many requests (CSV request_nos). Each runs through the same
    /// Transition funnel as ActionJson; incompatible states fail per-item. Cap defaults 500.
    /// </summary>
    public static string BatchActionJson(string requestNos, string action, string reason, string collectionPoint,
        string actor, string role, string channel, int cap)
    {
        try
        {
            if (cap < 1) cap = 500;
            string[] raws = (requestNos ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in raws) { string rn = raw.Trim(); if (rn.Length > 0 && seen.Add(rn)) list.Add(rn); }
            if (list.Count == 0) return Bad("No request numbers supplied.");
            if (list.Count > cap) return J.Serialize(new { success = false, error_code = "BATCH_TOO_LARGE", message = "Too many requests in one batch (max " + cap + ")." });

            int ok = 0, fail = 0;
            var results = new List<object>();
            foreach (string rn in list)
            {
                string res = ActionJson(rn, action, reason, collectionPoint, actor, role, channel);
                bool success = false; string status = ""; string msg = "";
                try
                {
                    var o = J.Deserialize<Dictionary<string, object>>(res);
                    if (o != null)
                    {
                        if (o.ContainsKey("success") && o["success"] != null) success = Convert.ToBoolean(o["success"]);
                        if (o.ContainsKey("status") && o["status"] != null) status = o["status"].ToString();
                        if (o.ContainsKey("message") && o["message"] != null) msg = o["message"].ToString();
                    }
                }
                catch { }
                if (success) ok++; else fail++;
                results.Add(new { request_no = rn, ok = success, status = status, message = msg });
            }
            return J.Serialize(new { success = true, ok = ok, fail = fail, total = ok + fail, results = results });
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>Static metadata for building generic UIs/clients: statuses, legal transitions, action map, filter enums.</summary>
    public static string MetaJson()
    {
        try
        {
            // The years of entry that actually have requests — offering every year in
            // acad_student would list two decades the bureau will never print.
            var entryYears = new List<object>();
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(
                        "SELECT sy.entryyear AS ey, COUNT(*) AS n FROM idcard_requests req" +
                        " JOIN acad_student sy ON sy.regno = req.regno" +
                        " WHERE IFNULL(sy.entryyear,0) > 0" +
                        " GROUP BY sy.entryyear ORDER BY sy.entryyear DESC", conn))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) entryYears.Add(new { year = ToI(r["ey"]), count = ToI(r["n"]) });
                }
            }
            catch { /* the dropdown simply falls back to "all years" */ }

            string[] statuses = new string[] { REQUESTED, FINANCE_CHECK, BLOCKED, SUBMITTED, APPROVED, HALTED, PRINTED, READY, COLLECTED, CANCELLED };
            var transitions = new Dictionary<string, object>();
            foreach (var kv in Allowed) { var arr = new List<string>(); foreach (var to in kv.Value) arr.Add(to); transitions[kv.Key] = arr; }
            var terminal = new List<string>(); foreach (var s in statuses) if (IsTerminal(s)) terminal.Add(s);
            var actions = new Dictionary<string, string>();
            actions["approve"] = APPROVED; actions["halt"] = HALTED; actions["printed"] = PRINTED;
            actions["ready"] = READY; actions["collected"] = COLLECTED; actions["cancel"] = CANCELLED;
            return J.Serialize(new { success = true, statuses = statuses, terminal = terminal, transitions = transitions, actions = actions,
                entryYears = entryYears,
                filters = new {
                    type = new string[] { "STUDENT", "STAFF" },
                    card_type = new string[] { "NEW", "REPLACEMENT" },
                    finance = new string[] { "ok", "below", "flagged" },
                    sort = new string[] { "created_at", "submitted_at", "updated_at", "status", "request_no" },
                    order = new string[] { "asc", "desc" } } });
        }
        catch (Exception ex) { return Err(ex); }
    }

    // ==================================================================
    // OMNIPASS SYNC — auto-create PRINTED requests for students whose card
    // OmniPass reports as printed but who have no request yet. Direct inserts
    // (no funnel, so no emails); mirrors sql/idcard_backfill_omnipass_printed.sql.
    // Called after each OmniPass BatchSync. Idempotent + capped per run.
    // ==================================================================
    public static int SyncPrintedRequests(string actor, int limit)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr)) { conn.Open(); return SyncPrintedRequests(conn, actor, limit); }
        }
        catch { return 0; }
    }

    public static int SyncPrintedRequests(MySqlConnection conn, string actor, int limit)
    {
        int created = 0;
        try
        {
            if (limit <= 0) limit = 500;
            if (string.IsNullOrEmpty(actor)) actor = "OMNIPASS-SYNC";
            EnsureSchema(conn);

            int year = DateTime.Now.Year;
            string prefix = "IDR-" + year + "-";
            long maxSeq = 0;
            using (var c = new MySqlCommand("SELECT IFNULL(MAX(CAST(SUBSTRING(request_no,10) AS UNSIGNED)),0) FROM idcard_requests WHERE request_no LIKE @p", conn))
            { c.Parameters.AddWithValue("@p", prefix + "%"); object v = c.ExecuteScalar(); maxSeq = (v == null || v == DBNull.Value) ? 0 : Convert.ToInt64(v); }

            var cand = new List<object[]>();
            using (var c = new MySqlCommand(
                "SELECT a.regno, IFNULL(a.photofile,'') photo, IFNULL(a.id_card_checked_at, NOW()) c " +
                "FROM acad_student a WHERE a.id_card_status='PRINTED' " +
                "AND NOT EXISTS (SELECT 1 FROM idcard_requests r WHERE TRIM(r.regno)=TRIM(a.regno)) " +
                "ORDER BY a.id_card_checked_at LIMIT " + limit, conn))
            using (var r = c.ExecuteReader())
                while (r.Read()) cand.Add(new object[] { S(r["regno"]), S(r["photo"]), r["c"] });

            foreach (var row in cand)
            {
                maxSeq++;
                string rn = prefix + maxSeq.ToString("D6");
                long rid;
                using (var ins = new MySqlCommand(
                    "INSERT INTO idcard_requests (request_no, requester_type, regno, card_type, status, photo_ref, photo_confirmed, guidelines_ack," +
                    " submitted_at, approved_at, printed_at, approved_by, printed_by, notes, created_by, created_at, updated_at)" +
                    " VALUES (@rn,'STUDENT',@r,'NEW','PRINTED',@ph,1,1,@c,@c,@c,@ac,@ac,@note,@cb,NOW(),NOW())", conn))
                {
                    ins.Parameters.AddWithValue("@rn", rn);
                    ins.Parameters.AddWithValue("@r", (string)row[0]);
                    ins.Parameters.AddWithValue("@ph", (string)row[1]);
                    ins.Parameters.AddWithValue("@c", row[2]);
                    ins.Parameters.AddWithValue("@ac", "OMNIPASS-SYNC");
                    ins.Parameters.AddWithValue("@note", "Auto-created from OmniPass PRINTED status");
                    ins.Parameters.AddWithValue("@cb", "OMNIPASS-SYNC");
                    try { ins.ExecuteNonQuery(); }
                    catch (MySqlException mex) { if (mex.Number == 1062) continue; throw; }
                    rid = ins.LastInsertedId;
                }
                using (var ev = new MySqlCommand(
                    "INSERT INTO idcard_request_events (request_id, from_status, to_status, actor, actor_role, channel, note, created_at) VALUES " +
                    "(@id,NULL,'REQUESTED',@a,'system','omnipass',@n,@c)," +
                    "(@id,'REQUESTED','FINANCE_CHECK',@a,'system','omnipass',@n,@c)," +
                    "(@id,'FINANCE_CHECK','SUBMITTED',@a,'system','omnipass',@n,@c)," +
                    "(@id,'SUBMITTED','APPROVED',@a,'system','omnipass',@n,@c)," +
                    "(@id,'APPROVED','PRINTED',@a,'system','omnipass',@n,@c)", conn))
                {
                    ev.Parameters.AddWithValue("@id", rid);
                    ev.Parameters.AddWithValue("@a", actor);
                    ev.Parameters.AddWithValue("@n", "Auto-created from OmniPass PRINTED status");
                    ev.Parameters.AddWithValue("@c", row[2]);
                    ev.ExecuteNonQuery();
                }
                created++;
            }
        }
        catch { /* never break the OmniPass sync */ }
        return created;
    }

    // ==================================================================
    // EMAIL (best-effort; called from Transition)
    // ==================================================================
    private static void TryNotify(MySqlConnection conn, int requestId, string toStatus)
    {
        try
        {
            string subject, body; if (!Template(toStatus, out subject, out body)) return;   // silent stages
            string type = "", regno = null, requestNo = ""; int empId = 0; string collect = "", halt = "";
            using (var cmd = new MySqlCommand("SELECT request_no, requester_type, regno, IFNULL(emp_id,0) emp_id, IFNULL(collection_point,'') cp, IFNULL(halt_reason,'') hr FROM idcard_requests WHERE id=@id LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", requestId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return;
                    requestNo = S(r["request_no"]); type = S(r["requester_type"]); regno = r["regno"] == DBNull.Value ? null : S(r["regno"]);
                    empId = ToI(r["emp_id"]); collect = S(r["cp"]); halt = S(r["hr"]);
                }
            }
            string email = RecipientEmail(conn, type, regno, empId);
            if (string.IsNullOrEmpty(email) || email.IndexOf('@') < 0) return;

            body = body.Replace("{REQ}", requestNo).Replace("{COLLECT}", collect == "" ? "the ID Card office" : collect).Replace("{REASON}", halt);
            string html = Wrap(subject, body, requestNo);
            bool ok = false;
            try { if (Mailer != null) { Mailer(email, subject, html); ok = true; } } catch { ok = false; }
            using (var up = new MySqlCommand("UPDATE idcard_request_events SET email_sent=@e WHERE request_id=@id AND to_status=@t ORDER BY id DESC LIMIT 1", conn))
            { up.Parameters.AddWithValue("@e", ok ? 1 : 0); up.Parameters.AddWithValue("@id", requestId); up.Parameters.AddWithValue("@t", toStatus); up.ExecuteNonQuery(); }
        }
        catch { /* email must never break a transition */ }
    }

    private static bool Template(string status, out string subject, out string body)
    {
        subject = ""; body = "";
        switch ((status ?? "").ToUpperInvariant())
        {
            case SUBMITTED: subject = "ID Card request received"; body = "Your ID card request <b>{REQ}</b> has been received and submitted for approval. We will notify you at each step."; return true;
            case APPROVED:  subject = "ID Card request approved"; body = "Good news — your ID card request <b>{REQ}</b> has been <b>approved</b> and is queued for printing."; return true;
            case HALTED:    subject = "ID Card request halted"; body = "Your ID card request <b>{REQ}</b> has been <b>halted</b>.<br/>Reason: <b>{REASON}</b><br/>Please address this and resubmit from the portal."; return true;
            case PRINTED:   subject = "ID Card printed"; body = "Your ID card for request <b>{REQ}</b> has been <b>printed</b>. You will be notified when it is ready for collection."; return true;
            case READY:     subject = "ID Card ready for collection"; body = "Your ID card (<b>{REQ}</b>) is <b>ready for collection</b> at <b>{COLLECT}</b>.<br/>Please bring your Request ID and a valid identification document."; return true;
            case COLLECTED: subject = "ID Card collected"; body = "This confirms your ID card for request <b>{REQ}</b> has been <b>collected</b>. Thank you."; return true;
            default: return false;   // REQUESTED / FINANCE_CHECK / BLOCKED / CANCELLED = no email
        }
    }

    private static string Wrap(string subject, string body, string reqNo)
    {
        return "<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e0e5ed;'>" +
            "<div style='background:#05275C;color:#fff;padding:16px 20px;font-size:16px;font-weight:700;'>Muteesa I Royal University</div>" +
            "<div style='padding:20px;color:#1a1a2e;font-size:14px;line-height:1.6;'>" +
            "<div style='font-size:15px;font-weight:700;margin-bottom:10px;'>" + subject + "</div>" + body +
            "<div style='margin-top:18px;font-size:12px;color:#5a6472;'>Request reference: <b>" + reqNo + "</b></div></div>" +
            "<div style='background:#f5f7fa;padding:12px 20px;font-size:11px;color:#8a94a6;'>Automated message from the Campus Dynamics ID Card system. Do not reply.</div></div>";
    }

    // ==================================================================
    // eligibility / editing an unfinished request / administrative override
    // ==================================================================

    /// <summary>
    /// May this person start a request right now, and would it be a replacement?
    /// The portal asks this before offering the button, so the answer and the reason
    /// are the same ones CreateRequest will enforce.
    /// </summary>
    public static string EligibilityJson(string type, string regno, int empId)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                Eligibility el = CheckEligibility(conn, type, regno, empId);
                return J.Serialize(new
                {
                    success = true,
                    canRequest = el.CanRequest,
                    blockingRequestNo = el.BlockingRequestNo,
                    blockingStatus = el.BlockingStatus,
                    canEditBlocking = el.CanEditBlocking,
                    hasCard = el.HasCard,
                    suggestedCardType = el.SuggestedCardType,
                    message = el.Message
                });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>
    /// Change an unfinished request — currently the card type, which is the field that
    /// decides whether a UGX 30,000 replacement fee is owed.
    ///
    /// This exists because SubmitJson reads card_type from the row and never wrote it:
    /// a requester who started as NEW, came back and picked REPLACEMENT would have
    /// submitted as NEW, skipping the fee capture entirely, with the screen showing
    /// them the choice they thought they had made. Changing your mind before you
    /// submit is normal, so it is now a real operation rather than a display trick.
    ///
    /// Only ever touches a request that has not yet been handed to the print bureau.
    /// </summary>
    public static string UpdateRequestJson(string requestNo, string cardType, string actor, string channel)
    {
        try
        {
            string ct = (cardType ?? "").Trim().ToUpperInvariant();
            if (ct != "NEW" && ct != "REPLACEMENT") return Bad("Choose New or Replacement.");

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                string type, regno, oldType, status; int empId;
                if (!LoadBasics(conn, requestNo, out type, out regno, out empId, out oldType, out status))
                    return Bad("Request not found.");

                status = (status ?? "").ToUpperInvariant();
                if (status != REQUESTED && status != FINANCE_CHECK && status != BLOCKED && status != HALTED)
                    return Bad("This request can no longer be changed (status " + status + "). Cancel it and start a new one if you need something different.");

                if (string.Equals(oldType, ct, StringComparison.OrdinalIgnoreCase))
                    return J.Serialize(new { success = true, cardType = ct, message = "No change." });

                // Switching away from REPLACEMENT clears the fee proof. Leaving it behind
                // would keep a payment reference attached to a card that carries no fee,
                // which reads to the bureau as a payment that was made.
                string sql = ct == "NEW"
                    ? "UPDATE idcard_requests SET card_type=@ct, replacement_fee_ref=NULL, replacement_fee_date=NULL," +
                      " replacement_fee_method=NULL, replacement_fee_notes=NULL, updated_at=NOW() WHERE request_no=@rn"
                    : "UPDATE idcard_requests SET card_type=@ct, updated_at=NOW() WHERE request_no=@rn";
                using (var up = new MySqlCommand(sql, conn))
                { up.Parameters.AddWithValue("@ct", ct); up.Parameters.AddWithValue("@rn", requestNo); up.ExecuteNonQuery(); }

                // Recorded in the timeline. A card type that changed silently would be
                // impossible to reconcile against a replacement fee later.
                LogNote(conn, requestNo, status, actor, "requester", channel ?? "eportal",
                        "Card type changed from " + oldType + " to " + ct);

                return J.Serialize(new { success = true, cardType = ct, message = "Request updated." });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>
    /// Administrative status change. Legal moves go through the ordinary funnel, so
    /// they behave exactly like the buttons. A move the state machine does not allow
    /// is an OVERRIDE: permitted, because real life produces states the diagram did
    /// not (a card printed then spoiled, a request advanced by mistake), but it demands
    /// a reason, it is written into the timeline as an override, and it is never
    /// silent. Terminal-to-anything is included deliberately — undoing a wrong
    /// COLLECTED is precisely the case an operator cannot fix any other way.
    /// </summary>
    public static string SetStatusJson(string requestNo, string toStatus, string reason,
        string actor, string role, string channel, bool allowOverride)
    {
        try
        {
            string to = (toStatus ?? "").Trim().ToUpperInvariant();
            if (Array.IndexOf(AllStatuses, to) < 0) return Bad("Unknown status '" + toStatus + "'.");

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open(); EnsureSchema(conn);
                string type, regno, cardType, from; int empId;
                if (!LoadBasics(conn, requestNo, out type, out regno, out empId, out cardType, out from))
                    return Bad("Request not found.");
                from = (from ?? "").ToUpperInvariant();

                if (from == to)
                    return J.Serialize(new { success = true, status = to, overridden = false, message = "Already " + to + "." });

                if (IsLegalMove(from, to))
                {
                    TransitionResult t = Transition(requestNo, to, actor, role, channel ?? "eadmin",
                                                    to == HALTED ? (reason ?? "Set by administrator") : null, reason);
                    return J.Serialize(new { success = t.Ok, status = t.Status, overridden = false, message = t.Message });
                }

                if (!allowOverride)
                    return Bad("Moving from " + from + " to " + to + " is not a normal step. Tick the override box and give a reason if you are sure.");
                if (string.IsNullOrEmpty((reason ?? "").Trim()))
                    return Bad("An override needs a reason. It is written into the request's history.");

                // Same column bookkeeping the funnel does, so an overridden request is not
                // left with a status its timestamps disagree with.
                string tc = TimeCol(to), ac = ActorCol(to);
                var sb = new System.Text.StringBuilder("UPDATE idcard_requests SET status=@to, updated_at=NOW()");
                if (tc != null) sb.Append(", " + tc + "=NOW()");
                if (ac != null) sb.Append(", " + ac + "=@actor");
                if (to == HALTED) sb.Append(", halt_reason=@hr");
                sb.Append(" WHERE request_no=@rn AND status=@from");   // optimistic: refuse if it moved under us
                int rows;
                using (var up = new MySqlCommand(sb.ToString(), conn))
                {
                    up.Parameters.AddWithValue("@to", to);
                    if (ac != null) up.Parameters.AddWithValue("@actor", actor ?? "");
                    if (to == HALTED) up.Parameters.AddWithValue("@hr", reason.Trim());
                    up.Parameters.AddWithValue("@rn", requestNo);
                    up.Parameters.AddWithValue("@from", from);
                    rows = up.ExecuteNonQuery();
                }
                if (rows == 0) return Bad("The request changed while you were working on it. Reload and try again.");

                LogNote(conn, requestNo, to, actor, role, channel ?? "eadmin",
                        "OVERRIDE " + from + " -> " + to + ": " + reason.Trim(), from);

                return J.Serialize(new
                {
                    success = true,
                    status = to,
                    overridden = true,
                    message = "Status set to " + to + " by override. The reason has been recorded in the history."
                });
            }
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>Writes a timeline entry without changing status (or recording an override).</summary>
    private static void LogNote(MySqlConnection conn, string requestNo, string toStatus, string actor,
        string role, string channel, string note)
    { LogNote(conn, requestNo, toStatus, actor, role, channel, note, toStatus); }

    private static void LogNote(MySqlConnection conn, string requestNo, string toStatus, string actor,
        string role, string channel, string note, string fromStatus)
    {
        try
        {
            int id;
            using (var cmd = new MySqlCommand("SELECT id FROM idcard_requests WHERE request_no=@rn LIMIT 1", conn))
            { cmd.Parameters.AddWithValue("@rn", requestNo); object v = cmd.ExecuteScalar(); if (v == null || v == DBNull.Value) return; id = Convert.ToInt32(v); }
            using (var cmd = new MySqlCommand(
                "INSERT INTO idcard_request_events (request_id, from_status, to_status, actor, actor_role, channel, note, created_at)" +
                " VALUES (@i,@f,@t,@a,@r,@c,@n,NOW())", conn))
            {
                cmd.Parameters.AddWithValue("@i", id);
                cmd.Parameters.AddWithValue("@f", (object)(fromStatus ?? ""));
                cmd.Parameters.AddWithValue("@t", toStatus ?? "");
                cmd.Parameters.AddWithValue("@a", (object)(actor ?? ""));
                cmd.Parameters.AddWithValue("@r", (object)(role ?? ""));
                // channel is VARCHAR(12) — an overlong value throws 1406 and would lose the note.
                cmd.Parameters.AddWithValue("@c", (object)((channel ?? "").Length > 12 ? channel.Substring(0, 12) : channel ?? ""));
                cmd.Parameters.AddWithValue("@n", (object)((note ?? "").Length > 500 ? note.Substring(0, 500) : note ?? ""));
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* the state change already happened; never fail on the audit write */ }
    }

    // ==================================================================
    // small helpers
    // ==================================================================
    private static bool LoadBasics(MySqlConnection conn, string requestNo, out string type, out string regno, out int empId, out string cardType, out string status)
    {
        type = ""; regno = null; empId = 0; cardType = ""; status = "";
        using (var cmd = new MySqlCommand("SELECT requester_type, regno, IFNULL(emp_id,0), card_type, status FROM idcard_requests WHERE request_no=@rn LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@rn", requestNo ?? "");
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return false;
                type = r.GetString(0); regno = r.IsDBNull(1) ? null : r.GetString(1); empId = r.GetInt32(2);
                cardType = r.GetString(3); status = r.GetString(4); return true;
            }
        }
    }

    private static int G(Dictionary<string, int> d, string k) { return d.ContainsKey(k) ? d[k] : 0; }
    private static string Get(Dictionary<string, string> d, string k) { return d != null && d.ContainsKey(k) && d[k] != null ? d[k] : ""; }
    private static int ToI(object v) { return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v); }
    private static string S(object v) { return v == null || v == DBNull.Value ? "" : v.ToString(); }
    private static string Bad(string m) { return J.Serialize(new { success = false, message = m }); }
    private static string Err(Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
}
