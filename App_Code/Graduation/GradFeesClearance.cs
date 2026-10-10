using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre, Fees Clearance.
//
//  A name on the graduation list (acad_graduands) has been approved by the
//  Academic Registry. Finance clearance is the Bursar's separate decision
//  that the student owes the University nothing. This class is the whole
//  of that decision: who may take it, what it is checked against, and the
//  money tools the Bursar needs to settle an account on the spot.
//
//  THE RULES
//   • Clearance needs the balance settled: canonical billed - paid <= 0
//     (fin_GetCanonicalStudentBalance, the one figure the rest of the system
//     uses), and, for gated years, a Graduation Fee bill for the list year.
//   • Anything outstanding can only be cleared as an OVERRIDE: a named person
//     with the override right, and a written reason.
//   • Every decision is appended to acad_grad_finance; the previous one is
//     superseded, never edited (enforced by triggers). acad_grad_finance_state
//     carries the current decision so lists and exports read one row.
//   • A clearance is re-tested live every time it is read. If the student
//     owes again (a later bill), the clearance stands but is flagged, so the
//     Bursar can see it and revoke it.
//   • Bills and payments posted here go through the same double-entry the
//     rest of the system uses (tracking row + both GL legs, in one
//     transaction), and are logged in acad_grad_finance_posting.
//
//  RIGHTS (sys_role_permissions on academics.graduation.fees_clearance)
//    can_view   see the queue and a student's finances
//    can_edit   bill, record a payment, clear a settled account, hold
//    can_delete override (clear with money owed) and revoke a clearance
//  An administrator holds all three.
// =====================================================================
public static class GradFeesClearance
{
    public const string SLUG = "academics.graduation.fees_clearance";
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    private static string MainConn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    private static string AcctConn()
    {
        var cs = ConfigurationManager.ConnectionStrings["accountsConnectionString"];
        return cs != null ? cs.ConnectionString : MainConn();
    }

    // =================================================================
    //  Rights
    // =================================================================
    public class Rights
    {
        public bool View, Edit, Override, IsAdmin;
        public string User = "", Role = "", RoleName = "";
        public string Label
        {
            get
            {
                if (IsAdmin) return "Administrator";
                if (Override) return (RoleName == "" ? "Finance" : RoleName) + " · may override";
                if (Edit) return (RoleName == "" ? "Finance" : RoleName) + " · may clear";
                if (View) return (RoleName == "" ? "Viewer" : RoleName) + " · view only";
                return "No access";
            }
        }
    }

    public static Rights GetRights()
    {
        var r = new Rights();
        try
        {
            HttpContext ctx = HttpContext.Current;
            if (ctx == null || ctx.Session == null) return r;
            r.User = Convert.ToString(ctx.Session["username"] ?? "").Trim();
            if (r.User == "") return r;

            if (string.IsNullOrEmpty(ctx.Session[RoleAccessService.SESSION_SLUGS] as string))
                RoleAccessService.LoadUserAccess(r.User);
            else
                RoleAccessService.MaybeRefresh(r.User);

            r.Role = RoleAccessService.GetRoleCode();
            r.RoleName = RoleAccessService.GetRoleName();
            if (RoleAccessService.IsAdmin())
            {
                r.IsAdmin = r.View = r.Edit = r.Override = true;
                return r;
            }

            using (var c = new MySqlConnection(MainConn()))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(MAX(rp.can_view),0), IFNULL(MAX(rp.can_edit),0), IFNULL(MAX(rp.can_delete),0) " +
                    "FROM sys_role_permissions rp JOIN sys_user_roles ur ON ur.role_id=rp.role_id " +
                    "JOIN sys_roles ro ON ro.id=ur.role_id " +
                    "WHERE ur.username=@u AND ur.is_active=1 AND ro.is_active=1 " +
                    "  AND (ur.expires_at IS NULL OR ur.expires_at>NOW()) AND rp.menu_slug=@s", c))
                {
                    cmd.Parameters.AddWithValue("@u", r.User);
                    cmd.Parameters.AddWithValue("@s", SLUG);
                    using (var rd = cmd.ExecuteReader())
                        if (rd.Read())
                        {
                            r.View = Convert.ToInt32(rd[0]) == 1;
                            r.Edit = r.View && Convert.ToInt32(rd[1]) == 1;
                            r.Override = r.Edit && Convert.ToInt32(rd[2]) == 1;
                        }
                }
            }
        }
        catch { r.View = r.Edit = r.Override = false; }
        return r;
    }

    public static string Denied(string why)
    {
        return J.Serialize(new { success = false, hasAccess = false,
            message = why ?? "You do not have access to Fees Clearance. Ask the system administrator for the Fees Clearance permission." });
    }

    private static string ClientIp()
    {
        try
        {
            var h = HttpContext.Current;
            if (h == null) return null;
            string f = h.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            return !string.IsNullOrEmpty(f) ? f.Split(',')[0].Trim() : h.Request.UserHostAddress;
        }
        catch { return null; }
    }

    // =================================================================
    //  Settings
    // =================================================================
    public class Settings
    {
        public bool RequireGradFee = true;
        public int GradFeeItem = 60;
        public decimal GradFeeAmount = 505000m;
        public int GradFeeSemester = 2;
        public string DocumentGate = "certificate";
        public int FirstGatedYear = 2026;

        public bool IsGated(string acadYear) { return YearOf(acadYear) >= FirstGatedYear; }
    }

    public static Settings GetSettings()
    {
        var s = new Settings();
        try
        {
            using (var c = new MySqlConnection(MainConn()))
            {
                c.Open();
                using (var cmd = new MySqlCommand("SELECT k, v FROM acad_grad_finance_setting", c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        string k = rd.GetString(0), v = rd.GetString(1).Trim();
                        int n; decimal d;
                        switch (k)
                        {
                            case "require_grad_fee": s.RequireGradFee = v == "1"; break;
                            case "grad_fee_item": if (int.TryParse(v, out n) && n > 0) s.GradFeeItem = n; break;
                            case "grad_fee_amount": if (decimal.TryParse(v, NumberStyles.Number, IC, out d) && d > 0) s.GradFeeAmount = d; break;
                            case "grad_fee_semester": if (int.TryParse(v, out n) && n >= 1 && n <= 3) s.GradFeeSemester = n; break;
                            case "document_gate": s.DocumentGate = v.ToLowerInvariant(); break;
                            case "first_gated_year": if (int.TryParse(v, out n) && n > 2000) s.FirstGatedYear = n; break;
                        }
                    }
            }
        }
        catch { }
        return s;
    }

    public static int YearOf(string acadYear)
    {
        string s = (acadYear ?? "").Trim();
        int n;
        return s.Length >= 4 && int.TryParse(s.Substring(0, 4), out n) ? n : 0;
    }

    // =================================================================
    //  The list
    // =================================================================
    public class Filter
    {
        public string acadYear = "", faculty = "", department = "", programme = "", search = "", status = "";
        public int page = 1, size = 100;
    }

    public class Finding
    {
        public string level;   // BLOCK | WARN | INFO
        public string code;
        public string text;
        public Finding(string l, string c, string t) { level = l; code = c; text = t; }
    }

    public class Row
    {
        public string regno = "", name = "", progcode = "", progname = "", faculty = "", acadyear = "", degclass = "";
        public decimal balance;            // live, billed - paid (> 0 owes)
        public bool gradFeeBilled;
        public bool gradFeeRequired;
        public int pendingReceipts;
        public string status = "PENDING"; // PENDING | CLEARED | HELD
        public string basis = "", reason = "", actor = "", decidedAt = "";
        public decimal decidedBalance;
        public bool owesSinceClearance;    // CLEARED but now owes
        public bool eligible;              // could be cleared now without override
        public List<Finding> findings = new List<Finding>();
        public int seq;
    }

    /// <summary>Every graduand for the filter with live figures. Lists are a few hundred names.</summary>
    public static List<Row> Fetch(Filter f, Settings st)
    {
        var rows = new List<Row>();
        using (var c = new MySqlConnection(MainConn()))
        {
            c.Open();
            string w = " WHERE 1=1 ";
            if (f.acadYear != "") w += " AND g.acadyear=@ay ";
            if (f.faculty != "") w += " AND TRIM(p.faculty_code)=@fac ";
            int dep;
            bool hasDep = f.department != "" && int.TryParse(f.department, out dep);
            if (hasDep) w += " AND p.department_id=@dep ";
            if (f.programme != "") w += " AND g.progcode=@prog ";
            if (f.search != "") w += " AND (g.regno LIKE @q OR g.stud_name LIKE @q) ";

            string sql =
                "SELECT g.regno, IFNULL(g.stud_name,''), g.progcode, IFNULL(p.progname,''), IFNULL(TRIM(p.faculty_code),''), " +
                "       IFNULL(g.acadyear,''), IFNULL(g.degclass,''), " +
                "       campus_dynamics_accounts.fin_GetCanonicalStudentBalance(g.regno) AS bal, " +
                "       EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking t " +
                "              WHERE t.regno=g.regno AND t.item_code=@gfi AND t.trans_type='Bill' AND t.post_status='Posted' " +
                "                AND (t.acadyear=g.acadyear OR t.trans_date >= CONCAT(LEFT(g.acadyear,4),'-08-01'))) AS gf, " +
                "       (SELECT COUNT(*) FROM campus_dynamics_accounts.fin_schoolpaydata sp " +
                "         WHERE sp.regno=g.regno AND sp.captureStatus='Pending') AS pend, " +
                "       IFNULL(s.status,''), IFNULL(s.basis,''), IFNULL(s.reason,''), IFNULL(s.actor,''), " +
                "       IFNULL(DATE_FORMAT(s.decided_at,'%e %b %Y %H:%i'),''), IFNULL(s.balance,0) " +
                "FROM acad_graduands g " +
                "LEFT JOIN acad_programme p ON p.progcode=g.progcode " +
                "LEFT JOIN acad_grad_finance_state s ON s.regno=g.regno AND s.acadyear=g.acadyear " +
                w + " ORDER BY p.progname, g.stud_name";

            using (var cmd = new MySqlCommand(sql, c))
            {
                cmd.CommandTimeout = 120;
                cmd.Parameters.AddWithValue("@gfi", st.GradFeeItem);
                if (f.acadYear != "") cmd.Parameters.AddWithValue("@ay", f.acadYear);
                if (f.faculty != "") cmd.Parameters.AddWithValue("@fac", f.faculty);
                if (hasDep) cmd.Parameters.AddWithValue("@dep", int.Parse(f.department));
                if (f.programme != "") cmd.Parameters.AddWithValue("@prog", f.programme);
                if (f.search != "") cmd.Parameters.AddWithValue("@q", "%" + f.search + "%");
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        var x = new Row();
                        x.regno = rd.GetString(0).Trim(); x.name = rd.GetString(1); x.progcode = rd.GetString(2);
                        x.progname = rd.GetString(3); x.faculty = rd.GetString(4); x.acadyear = rd.GetString(5);
                        x.degclass = rd.GetString(6);
                        x.balance = rd.IsDBNull(7) ? 0 : Convert.ToDecimal(rd[7]);
                        x.gradFeeBilled = Convert.ToInt32(rd[8]) == 1;
                        x.pendingReceipts = Convert.ToInt32(rd[9]);
                        string sts = rd.GetString(10);
                        x.status = sts == "" ? "PENDING" : sts;
                        x.basis = rd.GetString(11); x.reason = rd.GetString(12); x.actor = rd.GetString(13);
                        x.decidedAt = rd.GetString(14);
                        x.decidedBalance = Convert.ToDecimal(rd[15]);
                        Assess(x, st);
                        rows.Add(x);
                    }
            }
        }
        return rows;
    }

    /// <summary>The cheap checks that every row carries. The student panel adds the deep ones.</summary>
    private static void Assess(Row x, Settings st)
    {
        x.findings.Clear();
        x.gradFeeRequired = st.RequireGradFee && st.IsGated(x.acadyear);
        if (x.balance > 0)
            x.findings.Add(new Finding("BLOCK", "OWES", "Owes UGX " + Money(x.balance) + "."));
        if (x.gradFeeRequired && !x.gradFeeBilled)
            x.findings.Add(new Finding("BLOCK", "NO_GRAD_FEE",
                "No Graduation Fee has been billed for the " + x.acadyear + " graduation."));
        if (x.pendingReceipts > 0)
            x.findings.Add(new Finding("WARN", "PENDING_RECEIPTS",
                x.pendingReceipts + " SchoolPay receipt" + (x.pendingReceipts == 1 ? " is" : "s are") +
                " not yet posted to the ledger."));
        if (x.balance < 0)
            x.findings.Add(new Finding("INFO", "IN_CREDIT", "In credit by UGX " + Money(-x.balance) + "."));
        x.owesSinceClearance = x.status == "CLEARED" && x.balance > 0;
        if (x.owesSinceClearance)
            x.findings.Insert(0, new Finding("WARN", "OWES_SINCE",
                "Cleared on " + x.decidedAt + " but now owes UGX " + Money(x.balance) + "."));
        bool blocked = false;
        foreach (Finding fd in x.findings) if (fd.level == "BLOCK") { blocked = true; break; }
        x.eligible = !blocked && x.status != "CLEARED";
    }

    public static List<Row> Narrow(List<Row> all, string status)
    {
        if (string.IsNullOrEmpty(status) || status == "all") return all;
        var o = new List<Row>();
        foreach (Row r in all)
        {
            bool keep;
            switch (status)
            {
                case "pending": keep = r.status == "PENDING"; break;
                case "ready": keep = r.status == "PENDING" && r.eligible; break;
                case "blocked": keep = r.status != "CLEARED" && !r.eligible; break;
                case "cleared": keep = r.status == "CLEARED"; break;
                case "held": keep = r.status == "HELD"; break;
                case "owing": keep = r.balance > 0; break;
                case "drift": keep = r.owesSinceClearance; break;
                case "nofee": keep = r.gradFeeRequired && !r.gradFeeBilled; break;
                default: keep = true; break;
            }
            if (keep) o.Add(r);
        }
        return o;
    }

    public static object Kpis(List<Row> all)
    {
        int cleared = 0, pending = 0, held = 0, ready = 0, owing = 0, nofee = 0, drift = 0;
        decimal owed = 0, credit = 0;
        foreach (Row r in all)
        {
            if (r.status == "CLEARED") cleared++; else if (r.status == "HELD") held++; else pending++;
            if (r.status == "PENDING" && r.eligible) ready++;
            if (r.balance > 0) { owing++; owed += r.balance; }
            if (r.balance < 0) credit += -r.balance;
            if (r.gradFeeRequired && !r.gradFeeBilled) nofee++;
            if (r.owesSinceClearance) drift++;
        }
        return new { total = all.Count, cleared, pending, held, ready, owing, nofee, drift,
                     owed = owed, credit = credit };
    }

    // =================================================================
    //  Bootstrap (filters), institution-wide: Finance is not faculty-scoped
    // =================================================================
    public static string Bootstrap(Rights rt)
    {
        var years = new List<string>();
        var faculties = new List<object>();
        var departments = new List<object>();
        var programmes = new List<object>();
        string currentYear = "";
        using (var c = new MySqlConnection(MainConn()))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT DISTINCT acadyear FROM acad_graduands WHERE acadyear REGEXP '^[0-9]{4}/[0-9]{4}$' ORDER BY acadyear DESC", c))
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) years.Add(rd.GetString(0));
            try { currentYear = AcademicYearHelper.GetCurrentAcademicYear(); } catch { }
            if (!years.Contains(currentYear)) currentYear = years.Count > 0 ? years[0] : "";

            using (var cmd = new MySqlCommand(
                "SELECT TRIM(f.faculty_code), f.faculty_name FROM acad_faculty f " +
                "WHERE EXISTS (SELECT 1 FROM acad_programme p WHERE TRIM(p.faculty_code)=TRIM(f.faculty_code)) ORDER BY f.faculty_name", c))
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) faculties.Add(new { v = rd[0].ToString(), t = rd[1].ToString() });

            using (var cmd = new MySqlCommand(
                "SELECT d.ID, d.dept_name, TRIM(IFNULL(d.faculty_code,'')) FROM hrm_departments d " +
                "WHERE EXISTS (SELECT 1 FROM acad_programme p WHERE p.department_id=d.ID) ORDER BY d.dept_name", c))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read()) departments.Add(new { v = rd[0].ToString(), t = rd[1].ToString(), fac = rd[2].ToString() });

            using (var cmd = new MySqlCommand(
                "SELECT TRIM(p.progcode), COALESCE(p.progname,p.progcode), TRIM(IFNULL(p.faculty_code,'')), IFNULL(p.department_id,0) " +
                "FROM acad_programme p WHERE TRIM(IFNULL(p.progcode,''))<>'' AND p.progcode<>'-' ORDER BY 2", c))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read()) programmes.Add(new { v = rd[0].ToString(), t = rd[1].ToString(), fac = rd[2].ToString(), dep = rd[3].ToString() });
        }
        Settings st = GetSettings();
        return J.Serialize(new
        {
            success = true, hasAccess = true,
            roleNote = rt.Label, scopeLabel = "All faculties",
            canEdit = rt.Edit, canOverride = rt.Override,
            years, currentYear, faculties, departments, programmes,
            settings = new { requireGradFee = st.RequireGradFee, gradFeeAmount = st.GradFeeAmount,
                             gradFeeItem = st.GradFeeItem, firstGatedYear = st.FirstGatedYear,
                             documentGate = st.DocumentGate }
        });
    }

    // =================================================================
    //  One student, in depth
    // =================================================================
    public static string Detail(Rights rt, string regno)
    {
        regno = (regno ?? "").Trim();
        if (regno == "") return Err("No student number.");
        Settings st = GetSettings();
        var f = new Filter { search = regno };
        Row row = null;
        foreach (Row r in Fetch(f, st)) if (string.Equals(r.regno, regno, StringComparison.OrdinalIgnoreCase)) { row = r; break; }
        if (row == null) return Err(regno + " is not on any graduation list. Fees clearance applies only to students the Academic Registry has approved.");

        var ident = new Dictionary<string, object>();
        var semesters = new List<object>();
        var history = new List<object>();
        var postings = new List<object>();
        using (var c = new MySqlConnection(MainConn()))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT IFNULL(s.entryno,''), IFNULL(s.studsesion,''), IFNULL(s.entryyear,''), IFNULL(s.studPhone,''), " +
                "       IFNULL(s.email,''), IFNULL(f.faculty_name,''), " +
                "       IFNULL((SELECT v.actor FROM acad_grad_review v WHERE v.regno=g.regno AND v.verdict='CLEARED' ORDER BY v.id DESC LIMIT 1),''), " +
                "       IFNULL((SELECT DATE_FORMAT(v.created_at,'%e %b %Y') FROM acad_grad_review v WHERE v.regno=g.regno AND v.verdict='CLEARED' ORDER BY v.id DESC LIMIT 1),'') " +
                "FROM acad_graduands g LEFT JOIN acad_student s ON s.regno=g.regno " +
                "LEFT JOIN acad_programme p ON p.progcode=g.progcode LEFT JOIN acad_faculty f ON TRIM(f.faculty_code)=TRIM(p.faculty_code) " +
                "WHERE g.regno=@r LIMIT 1", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                    if (rd.Read())
                    {
                        ident["entryno"] = rd.GetString(0); ident["session"] = rd.GetString(1);
                        ident["entryyear"] = rd[2].ToString(); ident["phone"] = rd.GetString(3);
                        ident["email"] = rd.GetString(4); ident["facultyName"] = rd.GetString(5);
                        ident["approvedBy"] = rd.GetString(6); ident["approvedOn"] = rd.GetString(7);
                    }
            }

            // Registered semesters against what was billed for them (tuition + functional), net of
            // GL reversals. A registered semester that nets to nothing is a missing charge.
            var regs = new List<string[]>();
            using (var cmd = new MySqlCommand(
                "SELECT acad_year, studyyear, semester, regstatus FROM acad_registration WHERE regno=@r " +
                "ORDER BY acad_year, semester", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        regs.Add(new[] { rd[0].ToString(), rd[1].ToString(), rd[2].ToString(), rd[3].ToString() });
            }
            using (var a = new MySqlConnection(AcctConn()))
            {
                a.Open();
                foreach (string[] g in regs)
                {
                    decimal billed = 0, reversed = 0;
                    using (var cmd = new MySqlCommand(
                        "SELECT IFNULL(SUM(t.amount),0), " +
                        " IFNULL((SELECT SUM(l.transaction_amount) FROM fin_ledger l WHERE l.accountcode=@r AND l.transactionType='CR' " +
                        "   AND l.particulars LIKE 'Reversal of%' AND l.folio IN (SELECT CONCAT('BillNo:',t2.TID) FROM fin_studentfeestracking t2 " +
                        "       WHERE t2.regno=@r AND t2.acadyear=@y AND t2.semester=@s AND t2.item_code IN (1,52) AND t2.trans_type='Bill')),0) " +
                        "FROM fin_studentfeestracking t WHERE t.regno=@r AND t.acadyear=@y AND t.semester=@s " +
                        " AND t.item_code IN (1,52) AND t.trans_type='Bill' AND t.post_status='Posted'", a))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@y", g[0]);
                        cmd.Parameters.AddWithValue("@s", g[2]);
                        using (var rd = cmd.ExecuteReader())
                            if (rd.Read()) { billed = Convert.ToDecimal(rd[0]); reversed = Convert.ToDecimal(rd[1]); }
                    }
                    string rs = (g[3] ?? "").Trim().ToUpperInvariant();
                    bool registered = rs == "REGISTERED" || rs == "LATE REGISTERED" || rs == "CLEARED";
                    decimal net = billed - reversed;
                    semesters.Add(new { acadYear = g[0], studyYear = g[1], semester = g[2], regStatus = g[3],
                                        billed, reversed, net, missing = registered && net <= 0,
                                        fullyReversed = billed > 0 && net <= 0 });
                    if (registered && net <= 0)
                        row.findings.Add(new Finding("WARN", billed > 0 ? "REVERSED" : "UNBILLED",
                            billed > 0
                              ? g[0] + " Semester " + g[2] + " is registered but its fees were reversed and never raised again (UGX " + Money(reversed) + " reversed)."
                              : g[0] + " Semester " + g[2] + " is registered but no tuition or functional fee was billed for it."));
                }

                // Payments counted twice: the same SchoolPay transaction number on two tracking rows.
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM (SELECT TRIM(SUBSTRING_INDEX(detail,'TNo:',-1)) tno FROM fin_studentfeestracking " +
                    " WHERE regno=@r AND trans_type='Payment' AND post_status='Posted' AND detail LIKE '%TNo:%' " +
                    " GROUP BY tno HAVING COUNT(*)>1) z", a))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    int dup = Convert.ToInt32(cmd.ExecuteScalar());
                    if (dup > 0) row.findings.Add(new Finding("WARN", "DUP_PAYMENT",
                        dup + " SchoolPay payment" + (dup == 1 ? " appears" : "s appear") + " twice in the fees records. The balance may be understated."));
                }
            }

            using (var cmd = new MySqlCommand(
                "SELECT id, acadyear, verdict, basis, reason, balance, actor, actor_role, " +
                " DATE_FORMAT(created_at,'%e %b %Y %H:%i'), superseded_at IS NULL, IFNULL(batch_id,'') " +
                "FROM acad_grad_finance WHERE regno=@r ORDER BY id DESC LIMIT 50", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        history.Add(new { id = rd.GetInt32(0), year = rd.GetString(1), verdict = rd.GetString(2),
                                          basis = rd.GetString(3), reason = rd.GetString(4), balance = rd.GetDecimal(5),
                                          actor = rd.GetString(6), role = rd.GetString(7), at = rd.GetString(8),
                                          current = Convert.ToInt32(rd[9]) == 1, bulk = rd.GetString(10) != "" });
            }
            using (var cmd = new MySqlCommand(
                "SELECT kind, amount, detail, reference, bank_code, IFNULL(tracking_tid,0), IFNULL(voucher_no,0), actor, " +
                " DATE_FORMAT(created_at,'%e %b %Y %H:%i') FROM acad_grad_finance_posting WHERE regno=@r ORDER BY id DESC LIMIT 50", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        postings.Add(new { kind = rd.GetString(0), amount = rd.GetDecimal(1), detail = rd.GetString(2),
                                           reference = rd.GetString(3), bank = rd.GetString(4), tid = Convert.ToInt64(rd[5]),
                                           voucher = Convert.ToInt64(rd[6]), actor = rd.GetString(7), at = rd.GetString(8) });
            }
        }

        // The ledger, exactly as FinanceEngine defines it, with a running balance.
        var ledger = new List<object>();
        decimal dr = 0, cr = 0, run = 0;
        int payments = 0; string lastPay = ""; decimal schoolPay = 0, bursary = 0;
        DataTable dt = FinanceEngine.GetDualLedger(regno);
        foreach (DataRow lr in dt.Rows)
        {
            decimal d = lr["debit"] == DBNull.Value ? 0 : Convert.ToDecimal(lr["debit"]);
            decimal k = lr["credit"] == DBNull.Value ? 0 : Convert.ToDecimal(lr["credit"]);
            dr += d; cr += k; run += d - k;
            string det = Convert.ToString(lr["detail"]);
            string when = lr["trans_date"] == DBNull.Value ? "" : Convert.ToDateTime(lr["trans_date"]).ToString("dd MMM yyyy", IC);
            if (k > 0)
            {
                if (det.StartsWith("Bursary", StringComparison.OrdinalIgnoreCase)) bursary += k;
                else if (det.IndexOf("Waiver", StringComparison.OrdinalIgnoreCase) < 0 &&
                         det.IndexOf("Reversal", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    payments++; lastPay = when;
                    if (det.IndexOf("TNo:", StringComparison.OrdinalIgnoreCase) >= 0) schoolPay += k;
                }
            }
            ledger.Add(new { date = when, detail = det, debit = d, credit = k, balance = run,
                             src = Convert.ToString(lr["entry_source"]), year = Convert.ToString(lr["acad_year"]) });
        }
        decimal waivers = 0;
        try { waivers = FinanceEngine.GetWaiverTotal(regno); } catch { }
        if (dr - cr != row.balance)
            row.findings.Add(new Finding("WARN", "MISMATCH",
                "The ledger total (UGX " + Money(dr - cr) + ") and the balance used for clearance (UGX " + Money(row.balance) +
                ") disagree. Check the account in Student Ledgers before clearing."));

        // Re-decide eligibility with the deep checks included (deep findings are WARN, never BLOCK).
        bool blocked = false;
        foreach (Finding fd in row.findings) if (fd.level == "BLOCK") { blocked = true; break; }
        row.eligible = !blocked && row.status != "CLEARED";

        return J.Serialize(new
        {
            success = true,
            student = row,
            ident,
            totals = new { billed = dr, paid = cr, balance = row.balance, payments, lastPayment = lastPay,
                           schoolPay, bursary, waivers },
            ledger, semesters, history, postings,
            rights = new { edit = rt.Edit, @override = rt.Override, admin = rt.IsAdmin },
            options = Options(st, row)
        });
    }

    private static object Options(Settings st, Row row)
    {
        var items = new List<object>();
        var banks = new List<object>();
        using (var a = new MySqlConnection(AcctConn()))
        {
            a.Open();
            using (var cmd = new MySqlCommand(
                "SELECT ItemCode, ItemName FROM academicbillingitems WHERE ItemCode NOT IN (75) ORDER BY ItemName", a))
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) items.Add(new { v = rd[0].ToString(), t = rd[1].ToString() });
            using (var cmd = new MySqlCommand(
                "SELECT AccountCode, AccountName FROM fin_subaccounts WHERE MainAccountCode='AC1300' " +
                " AND AccountName NOT LIKE '%Loan%' AND AccountName NOT LIKE '%CONTROL%' ORDER BY AccountCode", a))
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) banks.Add(new { v = rd[0].ToString().Trim(), t = rd[1].ToString() });
        }
        return new { items, banks, gradFeeItem = st.GradFeeItem, gradFeeAmount = st.GradFeeAmount,
                     gradFeeSemester = st.GradFeeSemester, year = row.acadyear };
    }

    // =================================================================
    //  Decisions
    // =================================================================
    public class Outcome { public bool ok; public string message = ""; public string regno = ""; }

    /// <summary>Clear, hold or revoke one student. Everything is re-checked here, live.</summary>
    public static Outcome Decide(Rights rt, string regno, string verdict, string reason, bool asOverride, string batchId)
    {
        var o = new Outcome { regno = (regno ?? "").Trim() };
        verdict = (verdict ?? "").Trim().ToUpperInvariant();
        reason = (reason ?? "").Trim();
        if (o.regno == "") { o.message = "No student number."; return o; }
        if (!rt.Edit) { o.message = "You may view Fees Clearance but not decide."; return o; }
        if (verdict != "CLEARED" && verdict != "HELD" && verdict != "REVOKED") { o.message = "Unknown decision."; return o; }
        if (verdict == "REVOKED" && !rt.Override) { o.message = "Revoking a clearance needs the override right (Bursar)."; return o; }
        if ((verdict == "HELD" || verdict == "REVOKED") && reason.Length < 10)
        { o.message = "Give a reason of at least 10 characters. The student and the next reviewer only have this sentence."; return o; }
        if (reason.Length > 600) reason = reason.Substring(0, 600);

        Settings st = GetSettings();
        Row row = null;
        foreach (Row r in Fetch(new Filter { search = o.regno }, st))
            if (string.Equals(r.regno, o.regno, StringComparison.OrdinalIgnoreCase)) { row = r; break; }
        if (row == null) { o.message = o.regno + " is not on a graduation list."; return o; }

        string basis = "";
        if (verdict == "CLEARED")
        {
            if (row.status == "CLEARED") { o.message = row.name + " is already cleared by Finance."; return o; }
            var blocks = new List<string>();
            foreach (Finding fd in row.findings) if (fd.level == "BLOCK") blocks.Add(fd.text);
            if (blocks.Count > 0)
            {
                if (!asOverride)
                { o.message = "Cannot clear: " + string.Join(" ", blocks.ToArray()); return o; }
                if (!rt.Override)
                { o.message = "Clearing with money outstanding needs the override right (Bursar). " + string.Join(" ", blocks.ToArray()); return o; }
                if (reason.Length < 15)
                { o.message = "An override needs a written justification of at least 15 characters (for example the sponsor's undertaking or the payment plan)."; return o; }
                basis = "OVERRIDE";
            }
            else basis = row.balance < 0 ? "IN_CREDIT" : "NO_BALANCE";
        }
        else if (verdict == "REVOKED" && row.status != "CLEARED")
        { o.message = row.name + " is not cleared, so there is nothing to revoke."; return o; }
        else if (verdict == "HELD" && row.status == "HELD" && reason == row.reason)
        { o.message = "That is the reason already recorded."; return o; }

        decimal billed = 0, paid = 0;
        try
        {
            FinancialSummary fs = FinanceEngine.ComputePeriodBalance(o.regno);
            billed = fs.TotalCharges; paid = fs.TotalPayments;
        }
        catch { }

        string checks = J.Serialize(row.findings);
        using (var c = new MySqlConnection(MainConn()))
        {
            c.Open();
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    // Serialise decisions on this student.
                    using (var cmd = new MySqlCommand("SELECT regno FROM acad_graduands WHERE regno=@r FOR UPDATE", c, tx))
                    { cmd.Parameters.AddWithValue("@r", o.regno); if (cmd.ExecuteScalar() == null) throw new Exception("not on a graduation list"); }

                    long id;
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO acad_grad_finance (regno, acadyear, verdict, basis, reason, billed, paid, balance, checks_json, " +
                        " batch_id, actor, actor_role, ip, created_at) VALUES (@r,@y,@v,@b,@why,@bi,@pa,@ba,@ck,@bt,@a,@ar,@ip,NOW())", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", o.regno);
                        cmd.Parameters.AddWithValue("@y", row.acadyear);
                        cmd.Parameters.AddWithValue("@v", verdict);
                        cmd.Parameters.AddWithValue("@b", basis);
                        cmd.Parameters.AddWithValue("@why", reason);
                        cmd.Parameters.AddWithValue("@bi", billed);
                        cmd.Parameters.AddWithValue("@pa", paid);
                        cmd.Parameters.AddWithValue("@ba", row.balance);
                        cmd.Parameters.AddWithValue("@ck", checks);
                        cmd.Parameters.AddWithValue("@bt", (object)batchId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@a", rt.User);
                        cmd.Parameters.AddWithValue("@ar", rt.Role);
                        cmd.Parameters.AddWithValue("@ip", (object)ClientIp() ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                        id = cmd.LastInsertedId;
                    }
                    using (var cmd = new MySqlCommand(
                        "UPDATE acad_grad_finance SET superseded_at=NOW(), superseded_by=@id " +
                        "WHERE regno=@r AND superseded_at IS NULL AND id<>@id", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@r", o.regno);
                        cmd.ExecuteNonQuery();
                    }
                    if (verdict == "REVOKED")
                    {
                        using (var cmd = new MySqlCommand("DELETE FROM acad_grad_finance_state WHERE regno=@r", c, tx))
                        { cmd.Parameters.AddWithValue("@r", o.regno); cmd.ExecuteNonQuery(); }
                    }
                    else
                    {
                        using (var cmd = new MySqlCommand(
                            "REPLACE INTO acad_grad_finance_state (regno, acadyear, status, decision_id, basis, reason, balance, actor, decided_at) " +
                            "VALUES (@r,@y,@v,@id,@b,@why,@ba,@a,NOW())", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@r", o.regno);
                            cmd.Parameters.AddWithValue("@y", row.acadyear);
                            cmd.Parameters.AddWithValue("@v", verdict);
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.Parameters.AddWithValue("@b", basis);
                            cmd.Parameters.AddWithValue("@why", reason);
                            cmd.Parameters.AddWithValue("@ba", row.balance);
                            cmd.Parameters.AddWithValue("@a", rt.User);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    Log(c, tx, rt.User, "Fees Clearance " + verdict,
                        o.regno + " " + row.acadyear,
                        (basis != "" ? basis + "; " : "") + "balance " + Money(row.balance) + (reason != "" ? "; " + reason : ""));
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { }
                    o.message = "Nothing was saved: " + ex.Message;
                    return o;
                }
            }
        }
        o.ok = true;
        o.message = verdict == "CLEARED"
            ? row.name + " is cleared by Finance" + (basis == "OVERRIDE" ? " (override recorded against your name)." : ".")
            : verdict == "HELD" ? row.name + " is on finance hold." : "The finance clearance for " + row.name + " has been revoked.";
        return o;
    }

    // =================================================================
    //  Money: bill an item, record a payment
    // =================================================================

    /// <summary>
    /// Bills one item to one student: tracking row + DR student + CR the item's revenue account,
    /// in one transaction, refusing a second bill for the same item and term (the
    /// fin_bill_uniqueness trigger enforces it too).
    /// </summary>
    public static Outcome Bill(Rights rt, string regno, int itemCode, decimal amount, string acadYear,
                               int semester, string detail, string batchId)
    {
        var o = new Outcome { regno = (regno ?? "").Trim() };
        if (!rt.Edit) { o.message = "You may view Fees Clearance but not post bills."; return o; }
        if (o.regno == "") { o.message = "No student number."; return o; }
        if (amount <= 0 || amount > 50000000m || amount != Math.Round(amount)) { o.message = "Enter a whole amount between 1 and 50,000,000."; return o; }
        if (semester < 1 || semester > 3) { o.message = "Choose semester 1, 2 or 3."; return o; }
        acadYear = (acadYear ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(acadYear, "^[0-9]{4}/[0-9]{4}$")) { o.message = "Choose the academic year."; return o; }
        if (!OnList(o.regno)) { o.message = o.regno + " is not on a graduation list."; return o; }

        string itemName = "", acct = "";
        using (var a = new MySqlConnection(AcctConn()))
        {
            a.Open();
            using (var cmd = new MySqlCommand("SELECT ItemName, IFNULL(AccountCode,'') FROM academicbillingitems WHERE ItemCode=@i", a))
            {
                cmd.Parameters.AddWithValue("@i", itemCode);
                using (var rd = cmd.ExecuteReader()) if (rd.Read()) { itemName = rd.GetString(0); acct = rd.GetString(1).Trim(); }
            }
            if (itemName == "") { o.message = "That billing item does not exist."; return o; }
            if (acct == "") { o.message = itemName + " has no revenue account, so it cannot be posted. Set one in Fee Structure."; return o; }

            string part = (detail ?? "").Trim();
            if (part == "") part = itemName + " for Semester:" + semester + ", " + acadYear;
            if (part.Length > 300) part = part.Substring(0, 300);

            using (var tx = a.BeginTransaction())
            {
                try
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT TID FROM fin_studentfeestracking WHERE regno=@r AND acadyear=@y AND semester=@s " +
                        " AND item_code=@i AND trans_type='Bill' LIMIT 1 FOR UPDATE", a, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", o.regno); cmd.Parameters.AddWithValue("@y", acadYear);
                        cmd.Parameters.AddWithValue("@s", semester); cmd.Parameters.AddWithValue("@i", itemCode);
                        if (cmd.ExecuteScalar() != null)
                        { tx.Rollback(); o.message = itemName + " is already billed to " + o.regno + " for Semester " + semester + ", " + acadYear + "."; return o; }
                    }
                    string today = DateTime.Now.ToString("yyyy-MM-dd", IC);
                    long tid;
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO fin_studentfeestracking (regno, semester, acadyear, amount, item_code, trans_type, detail, trans_date, post_status) " +
                        "VALUES (@r,@s,@y,@m,@i,'Bill',@d,@dt,'Posted')", a, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", o.regno); cmd.Parameters.AddWithValue("@s", semester);
                        cmd.Parameters.AddWithValue("@y", acadYear); cmd.Parameters.AddWithValue("@m", amount);
                        cmd.Parameters.AddWithValue("@i", itemCode); cmd.Parameters.AddWithValue("@d", part);
                        cmd.Parameters.AddWithValue("@dt", today);
                        cmd.ExecuteNonQuery();
                        tid = cmd.LastInsertedId;
                    }
                    string folio = "BillNo:" + tid.ToString(IC);
                    foreach (bool student in new[] { true, false })
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO fin_ledger (accountcode, account_type, transactionType, transaction_amount, particulars, voucherNo, " +
                            " transactionDate, teller, timeLog, folio, journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount) " +
                            "VALUES (@ac,@at,@tt,@m,@p,@v,@td,@u,NOW(),@fo,'-','UGX',@m,0,1,@m)", a, tx))
                        {
                            cmd.Parameters.AddWithValue("@ac", student ? o.regno : acct);
                            cmd.Parameters.AddWithValue("@at", student ? "Student" : "Chart Account");
                            cmd.Parameters.AddWithValue("@tt", student ? "DR" : "CR");
                            cmd.Parameters.AddWithValue("@m", amount);
                            cmd.Parameters.AddWithValue("@p", student ? part : part + " (" + o.regno + ")");
                            cmd.Parameters.AddWithValue("@v", tid);
                            cmd.Parameters.AddWithValue("@td", today);
                            cmd.Parameters.AddWithValue("@u", Teller(rt.User));
                            cmd.Parameters.AddWithValue("@fo", folio);
                            cmd.ExecuteNonQuery();
                        }
                    Posting(a, tx, o.regno, acadYear, "BILL", itemCode, amount, part, "", "", tid, tid, batchId, rt.User);
                    Log(a, tx, rt.User, "Fees Clearance BILL", o.regno, itemName + " " + Money(amount) + " (" + acadYear + " S" + semester + ") TID " + tid);
                    tx.Commit();
                    o.ok = true;
                    o.message = itemName + " of UGX " + Money(amount) + " billed to " + o.regno + ".";
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { }
                    o.message = ex.Message.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0
                        ? itemName + " is already billed for that term (" + o.regno + ")."
                        : "Nothing was posted: " + ex.Message;
                }
            }
        }
        if (o.ok) RefreshBalances(o.regno, acct);
        return o;
    }

    /// <summary>The Graduation Fee for the student's list year, at the configured amount.</summary>
    public static Outcome BillGradFee(Rights rt, string regno, decimal amount, string batchId)
    {
        Settings st = GetSettings();
        string year = ListYear(regno);
        if (year == "") return new Outcome { regno = regno, message = regno + " is not on a graduation list." };
        if (amount <= 0) amount = st.GradFeeAmount;
        if (HasGradFee(regno, year, st))
            return new Outcome { regno = regno, message = regno + " already has a Graduation Fee for " + year + "." };
        return Bill(rt, regno, st.GradFeeItem, amount, year, st.GradFeeSemester,
                    "Graduation Fee for Semester:" + st.GradFeeSemester + ", " + year, batchId);
    }

    /// <summary>
    /// Records money received: CR the student (their faculty receivables ledger) and DR the
    /// chosen bank or cash account through fin_TransactionCreator, plus the tracking Payment
    /// row on the same date so the two stores agree. The reference must be unique.
    /// </summary>
    public static Outcome RecordPayment(Rights rt, string regno, string bankCode, decimal amount, string payDate,
                                        string reference, string payer, bool confirmSameDay)
    {
        var o = new Outcome { regno = (regno ?? "").Trim() };
        if (!rt.Edit) { o.message = "You may view Fees Clearance but not record payments."; return o; }
        reference = (reference ?? "").Trim();
        payer = (payer ?? "").Trim();
        bankCode = (bankCode ?? "").Trim();
        if (o.regno == "") { o.message = "No student number."; return o; }
        if (amount <= 0 || amount > 50000000m || amount != Math.Round(amount)) { o.message = "Enter a whole amount between 1 and 50,000,000."; return o; }
        if (reference.Length < 3 || reference.Length > 60) { o.message = "Enter the receipt or bank slip number (3 to 60 characters)."; return o; }
        DateTime pd;
        if (!DateTime.TryParseExact((payDate ?? "").Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out pd))
        { o.message = "Enter the date the money was paid."; return o; }
        if (pd.Date > DateTime.Today || pd.Year < 2020) { o.message = "The payment date cannot be in the future or before 2020."; return o; }
        string year = ListYear(o.regno);
        if (year == "") { o.message = o.regno + " is not on a graduation list."; return o; }

        using (var a = new MySqlConnection(AcctConn()))
        {
            a.Open();
            string bankName = "";
            using (var cmd = new MySqlCommand(
                "SELECT AccountName FROM fin_subaccounts WHERE AccountCode=@b AND MainAccountCode='AC1300'", a))
            {
                cmd.Parameters.AddWithValue("@b", bankCode);
                object x = cmd.ExecuteScalar();
                if (x == null) { o.message = "Choose the bank or cash account the money went into."; return o; }
                bankName = x.ToString().Trim();
            }
            string folio = "GradPay:" + reference;
            using (var cmd = new MySqlCommand(
                "SELECT (SELECT COUNT(*) FROM fin_ledger WHERE folio=@f) + " +
                "       (SELECT COUNT(*) FROM campus_dynamics.acad_grad_finance_posting WHERE kind='PAYMENT' AND reference=@ref)", a))
            {
                cmd.Parameters.AddWithValue("@f", folio); cmd.Parameters.AddWithValue("@ref", reference);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                { o.message = "Receipt " + reference + " has already been recorded. Nothing was posted."; return o; }
            }
            if (!confirmSameDay)
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM fin_ledger WHERE accountcode=@r AND transactionType='CR' " +
                    " AND transaction_amount=@m AND DATE(transactionDate)=@d", a))
                {
                    cmd.Parameters.AddWithValue("@r", o.regno); cmd.Parameters.AddWithValue("@m", amount);
                    cmd.Parameters.AddWithValue("@d", pd.ToString("yyyy-MM-dd", IC));
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        o.message = "SAME_DAY|A payment of UGX " + Money(amount) + " on " + pd.ToString("d MMM yyyy", IC) +
                                    " is already on this student's account. Record it again only if this is a second, separate payment.";
                        return o;
                    }
                }

            string ledgerName;
            using (var cmd = new MySqlCommand("SELECT fin_GetStudentLedgerName(@r)", a))
            { cmd.Parameters.AddWithValue("@r", o.regno); object x = cmd.ExecuteScalar(); ledgerName = x == null || x == DBNull.Value ? "" : x.ToString(); }
            if (ledgerName == "") { o.message = "The student's fees ledger could not be determined (check the programme's faculty)."; return o; }

            string who = payer == "" ? "" : " by " + payer;
            string cr = "Fees Payment on " + pd.ToString("dd/MM/yyyy", IC) + " thru " + bankName + who + " Ref: " + reference;
            string drp = "Paid on " + pd.ToString("dd/MM/yyyy", IC) + who + " [" + o.regno + "] Ref: " + reference;
            if (cr.Length > 340) cr = cr.Substring(0, 340);
            if (drp.Length > 340) drp = drp.Substring(0, 340);

            using (var tx = a.BeginTransaction())
            {
                try
                {
                    using (var cmd = new MySqlCommand(
                        "CALL fin_TransactionCreator(@cra,@crt,@crp,@dra,'Chart Account',@drp,@m,0,@d,@u,'UGX',@f)", a, tx))
                    {
                        cmd.Parameters.AddWithValue("@cra", o.regno); cmd.Parameters.AddWithValue("@crt", ledgerName);
                        cmd.Parameters.AddWithValue("@crp", cr); cmd.Parameters.AddWithValue("@dra", bankCode);
                        cmd.Parameters.AddWithValue("@drp", drp); cmd.Parameters.AddWithValue("@m", (long)amount);
                        cmd.Parameters.AddWithValue("@d", pd.ToString("yyyy-MM-dd", IC));
                        cmd.Parameters.AddWithValue("@u", Teller(rt.User)); cmd.Parameters.AddWithValue("@f", folio);
                        cmd.ExecuteNonQuery();
                    }
                    long voucher = 0;
                    using (var cmd = new MySqlCommand(
                        "SELECT voucherNo FROM fin_ledger WHERE folio=@f AND accountcode=@r AND transactionType='CR' ORDER BY TID DESC LIMIT 1", a, tx))
                    {
                        cmd.Parameters.AddWithValue("@f", folio); cmd.Parameters.AddWithValue("@r", o.regno);
                        object x = cmd.ExecuteScalar();
                        if (x == null) throw new Exception("the ledger did not accept the payment");
                        voucher = Convert.ToInt64(x);
                    }
                    long tid;
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO fin_studentfeestracking (regno, semester, acadyear, amount, item_code, trans_type, detail, trans_date, post_status) " +
                        "VALUES (@r,@s,@y,@m,0,'Payment',@dt,@d,'Posted')", a, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", o.regno); cmd.Parameters.AddWithValue("@s", GetSettings().GradFeeSemester);
                        cmd.Parameters.AddWithValue("@y", year); cmd.Parameters.AddWithValue("@m", amount);
                        cmd.Parameters.AddWithValue("@dt", cr); cmd.Parameters.AddWithValue("@d", pd.ToString("yyyy-MM-dd", IC));
                        cmd.ExecuteNonQuery();
                        tid = cmd.LastInsertedId;
                    }
                    Posting(a, tx, o.regno, year, "PAYMENT", null, amount, cr, reference, bankCode, tid, voucher, null, rt.User);
                    Log(a, tx, rt.User, "Fees Clearance PAYMENT", o.regno,
                        Money(amount) + " to " + bankCode + " ref " + reference + " voucher " + voucher);
                    tx.Commit();
                    o.ok = true;
                    o.message = "Payment of UGX " + Money(amount) + " recorded for " + o.regno + " (voucher " + voucher + ").";
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { }
                    o.message = "Nothing was posted: " + ex.Message;
                }
            }
        }
        if (o.ok) RefreshBalances(o.regno, bankCode);
        return o;
    }

    // =================================================================
    //  Helpers
    // =================================================================
    public static bool OnList(string regno) { return ListYear(regno) != ""; }

    public static string ListYear(string regno)
    {
        using (var c = new MySqlConnection(MainConn()))
        {
            c.Open();
            using (var cmd = new MySqlCommand("SELECT IFNULL(acadyear,'') FROM acad_graduands WHERE regno=@r LIMIT 1", c))
            {
                cmd.Parameters.AddWithValue("@r", (regno ?? "").Trim());
                object x = cmd.ExecuteScalar();
                return x == null || x == DBNull.Value ? "" : x.ToString().Trim();
            }
        }
    }

    private static bool HasGradFee(string regno, string year, Settings st)
    {
        using (var a = new MySqlConnection(AcctConn()))
        {
            a.Open();
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM fin_studentfeestracking t WHERE t.regno=@r AND t.item_code=@i AND t.trans_type='Bill' " +
                " AND t.post_status='Posted' AND (t.acadyear=@y OR t.trans_date >= CONCAT(LEFT(@y,4),'-08-01'))", a))
            {
                cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@i", st.GradFeeItem);
                cmd.Parameters.AddWithValue("@y", year);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }
    }

    /// <summary>The ledger's teller column is CHAR(25); a long email login would be cut off.</summary>
    private static string Teller(string user)
    {
        string u = (user ?? "").Trim();
        return u.Length > 25 ? u.Substring(0, 25) : u;
    }

    private static void Posting(MySqlConnection a, MySqlTransaction tx, string regno, string year, string kind, int? item,
                                decimal amount, string detail, string reference, string bank, long tid, long voucher,
                                string batchId, string actor)
    {
        using (var cmd = new MySqlCommand(
            "INSERT INTO campus_dynamics.acad_grad_finance_posting (regno, acadyear, kind, item_code, amount, detail, reference, " +
            " bank_code, tracking_tid, voucher_no, batch_id, actor, ip, created_at) " +
            "VALUES (@r,@y,@k,@i,@m,@d,@ref,@b,@t,@v,@bt,@a,@ip,NOW())", a, tx))
        {
            cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@y", year ?? "");
            cmd.Parameters.AddWithValue("@k", kind); cmd.Parameters.AddWithValue("@i", item.HasValue ? (object)item.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@m", amount); cmd.Parameters.AddWithValue("@d", detail.Length > 350 ? detail.Substring(0, 350) : detail);
            cmd.Parameters.AddWithValue("@ref", reference ?? ""); cmd.Parameters.AddWithValue("@b", bank ?? "");
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@v", voucher);
            cmd.Parameters.AddWithValue("@bt", (object)batchId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", actor); cmd.Parameters.AddWithValue("@ip", (object)ClientIp() ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private static void Log(MySqlConnection c, MySqlTransaction tx, string user, string fn, string par, string comment)
    {
        using (var cmd = new MySqlCommand(
            "INSERT INTO campus_dynamics.acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u,@f,@p,@c,NOW())", c, tx))
        {
            cmd.Parameters.AddWithValue("@u", user.Length > 100 ? user.Substring(0, 100) : user);
            cmd.Parameters.AddWithValue("@f", fn);
            cmd.Parameters.AddWithValue("@p", par.Length > 200 ? par.Substring(0, 200) : par);
            cmd.Parameters.AddWithValue("@c", comment.Length > 300 ? comment.Substring(0, 300) : comment);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Running balances on the touched accounts, outside the transaction (as SchoolPay does).</summary>
    private static void RefreshBalances(string regno, string other)
    {
        try
        {
            using (var a = new MySqlConnection(AcctConn()))
            {
                a.Open();
                foreach (string acc in new[] { regno, other })
                {
                    if (string.IsNullOrEmpty(acc)) continue;
                    using (var cmd = new MySqlCommand("CALL fin_UpdateLedgerBalances(@a)", a))
                    { cmd.CommandTimeout = 120; cmd.Parameters.AddWithValue("@a", acc); cmd.ExecuteNonQuery(); }
                }
            }
        }
        catch { }
    }

    public static string Money(decimal v) { return Math.Round(v).ToString("#,0", IC); }

    private static string Err(string m) { return J.Serialize(new { success = false, message = m }); }

    public static string Serialize(object o) { return J.Serialize(o); }

    // =================================================================
    //  Read-only views used outside the controller
    // =================================================================

    /// <summary>Current finance status for each regno ("" = pending), for the Graduation List.</summary>
    public static Dictionary<string, string[]> StatusMap(string acadYear)
    {
        var m = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        using (var c = new MySqlConnection(MainConn()))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT s.regno, s.status, s.actor, DATE_FORMAT(s.decided_at,'%e %b %Y'), s.basis FROM acad_grad_finance_state s " +
                "JOIN acad_graduands g ON g.regno=s.regno AND g.acadyear=s.acadyear" +
                (string.IsNullOrEmpty(acadYear) ? "" : " WHERE s.acadyear=@y"), c))
            {
                if (!string.IsNullOrEmpty(acadYear)) cmd.Parameters.AddWithValue("@y", acadYear);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        m[rd.GetString(0).Trim()] = new[] { rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4) };
            }
        }
        return m;
    }

    /// <summary>
    /// Whether a document for this student needs finance clearance and lacks it. Used by the
    /// print gate. Returns "" when printing may proceed, otherwise the reason.
    /// </summary>
    public static string DocumentBlock(MySqlConnection c, string regno, string kind, Settings st)
    {
        string gate = st.DocumentGate;
        if (gate == "off") return "";
        if (gate == "certificate" && kind != "CERTIFICATE") return "";
        using (var cmd = new MySqlCommand(
            "SELECT g.acadyear, IFNULL(s.status,'') FROM acad_graduands g " +
            "LEFT JOIN acad_grad_finance_state s ON s.regno=g.regno AND s.acadyear=g.acadyear WHERE g.regno=@r LIMIT 1", c))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var rd = cmd.ExecuteReader())
            {
                if (!rd.Read()) return "";                      // not on a list: the graduation rule decides
                string year = rd.GetString(0), status = rd.GetString(1);
                if (!st.IsGated(year) || status == "CLEARED") return "";
                return regno + " has not been cleared by the Bursar's office for the " + year + " graduation" +
                       (status == "HELD" ? " (finance hold)" : "") +
                       ", so a " + (kind == "CERTIFICATE" ? "certificate" : "transcript") +
                       " cannot be issued. Clear them in Graduation > Fees Clearance first.";
            }
        }
    }
}
