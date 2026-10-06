using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Contract Renewal Requests - HR console (Muteesa I Royal University).
///
/// Tabs (GET ?tab=):
///   apps      - every application with KPIs, filters and bulk Forward / Council decision
///               (the bulk writes post to ContractRenewalView.aspx, which owns the state machine)
///   expiring  - employees whose current contract (hr_current_contract_id) ends within the
///               window or ended in the last 60 days, whether they have applied, and a bulk
///               "send reminder" (one AJAX call per employee, each logged in hr_renewal_reminders)
///   rounds    - renewal rounds (hr_renewal_rounds): create / edit / close
/// Plus ?ajax=schedule_xlsx / schedule_csv: the Council schedule through HrExport, with exactly the columns
/// of the printed schedule (ContractRenewalPrint.aspx?schedule=1); both use ScheduleHeaders / ScheduleRows.
/// </summary>
public partial class COOPERP_NewScreens_ContractRenewals : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private string QsTab
    {
        get
        {
            string t = (Request.QueryString["tab"] ?? "").Trim().ToLower();
            return t == "expiring" || t == "rounds" ? t : "apps";
        }
    }
    private int QsRound { get { int v; return int.TryParse(Request.QueryString["round"] ?? "0", out v) && v > 0 ? v : 0; } }
    private string QsStatus { get { return (Request.QueryString["status"] ?? "").Trim().ToUpper(); } }
    private string QsCat { get { return (Request.QueryString["cat"] ?? "").Trim().ToUpper(); } }
    private int QsDept { get { int v; return int.TryParse(Request.QueryString["dept"] ?? "0", out v) && v > 0 ? v : 0; } }
    private string QsLate { get { string v = (Request.QueryString["late"] ?? "").Trim(); return v == "1" || v == "0" ? v : ""; } }
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }
    private string QsWin
    {
        get
        {
            string v = (Request.QueryString["win"] ?? "90").Trim().ToLower();
            return v == "30" || v == "60" || v == "90" || v == "180" || v == "round" ? v : "90";
        }
    }
    private bool QsNoApp { get { return (Request.QueryString["noapp"] ?? "") == "1"; } }

    private static readonly string[] Statuses = { "DRAFT", "AWAITING_SUPERVISOR", "AWAITING_HR", "RETURNED", "FORWARDED", "APPROVED", "NOT_APPROVED", "DEFERRED", "CONTRACT_ISSUED", "WITHDRAWN" };

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            if (ajax == "schedule_csv" || ajax == "schedule_xlsx")
            {
                if (!HrAccess.RequireHr(false)) return;
                ServeSchedule(ajax == "schedule_xlsx");
                return;
            }
            HandleAjax(ajax);
            return;
        }

        if (!HrAccess.RequireHr(false)) return;
        if (!IsPostBack)
        {
            try { LoadPage(); }
            catch (Exception)
            {
                pnlMain.Visible = false;
                litError.Text = "<div class='hr-notice hr-notice--bad'>Contract renewals could not be loaded. Reload the page, or contact MIS if this continues.</div>";
            }
        }
    }

    private static string CurrentUsername() { return HrAccess.Username(); }

    private string ActorTag { get { return "eadmin:" + CurrentUsername(); } }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        if (!HrAccess.RequireHr(true)) return;
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                || !MarksAntiForgeryService.ValidateRequest())
                Response.Write("{\"ok\":false,\"msg\":\"The page has expired. Reload it and try again.\"}");
            else
            {
                switch (action)
                {
                    case "send_reminder": AjaxSendReminder(); break;
                    case "save_round":    AjaxSaveRound(); break;
                    case "close_round":   AjaxCloseRound(); break;
                    default: Response.Write("{\"ok\":false,\"msg\":\"Unknown action.\"}"); break;
                }
            }
        }
        catch (System.Threading.ThreadAbortException) { }
        catch (Exception)
        {
            Response.Write("{\"ok\":false,\"msg\":\"The action could not be completed. Reload the page and try again.\"}");
        }
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private string F(string key) { return (Request.Form[key] ?? "").Trim(); }

    // ── reminders ──────────────────────────────────────────────────────
    private void AjaxSendReminder()
    {
        int empId = SafeInt(F("emp_id"));
        int roundId = SafeInt(F("round_id"));
        DataTable dt = Q(
            @"SELECT e.empID, e.emp_name, IFNULL(e.emp_email,'') AS emp_email,
                     c.ID AS cid, c.contractEnd, IFNULL(j.jobname,'') AS jobname, IFNULL(d.dept_name,'') AS dept_name
              FROM hrm_employee e
              LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
              LEFT JOIN hrm_jobs j ON j.ID = c.jobID
              LEFT JOIN hrm_departments d ON d.ID = c.departmentID
              WHERE e.empID = @e", new MySqlParameter("@e", empId));
        if (dt.Rows.Count == 0) { Response.Write("{\"ok\":false,\"status\":\"ERROR\",\"msg\":\"Employee not found.\"}"); return; }
        DataRow r = dt.Rows[0];
        int cid = SafeInt(r["cid"]);

        DataRow round = null;
        DataTable rd = roundId > 0
            ? Q("SELECT * FROM hr_renewal_rounds WHERE round_id = @r", new MySqlParameter("@r", roundId))
            : Q("SELECT * FROM hr_renewal_rounds WHERE status = 'OPEN' ORDER BY submission_deadline LIMIT 1");
        if (rd.Rows.Count > 0) round = rd.Rows[0];

        DataTable app = cid > 0
            ? Q("SELECT renewal_id, ref_no, status FROM hr_contract_renewals WHERE contract_id = @c AND status <> 'WITHDRAWN' ORDER BY renewal_id DESC LIMIT 1", new MySqlParameter("@c", cid))
            : new DataTable();
        int renewalId = app.Rows.Count > 0 ? SafeInt(app.Rows[0]["renewal_id"]) : 0;
        string appStatus = app.Rows.Count > 0 ? SafeStr(app.Rows[0]["status"]) : "";
        string appRef = app.Rows.Count > 0 ? SafeStr(app.Rows[0]["ref_no"]) : "";

        string email = SafeStr(r["emp_email"]).Trim();
        string status, error = "";
        if (email == "" || !email.Contains("@")) status = "NO_EMAIL";
        else
        {
            DateTime? end = D(r["contractEnd"]);
            DateTime? deadline = round != null ? D(round["submission_deadline"]) : null;
            string sitting = round != null ? SafeStr(round["council_sitting"]).Trim() : "";
            string job = SafeStr(r["jobname"]).Trim(), dept = SafeStr(r["dept_name"]).Trim();

            // One plain paragraph: what is ending, what to do, by when.
            StringBuilder b = new StringBuilder("Your contract");
            if (job != "") b.Append(" as " + Enc(job));
            if (dept != "") b.Append(" in " + Enc(dept));
            if (end.HasValue) b.Append(end.Value.Date < DateTime.Today ? " ended on " : " ends on ").Append(end.Value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)).Append(".");
            else b.Append(" is due for renewal.");
            b.Append(" If you wish to have it renewed, please ");
            b.Append((appStatus == "DRAFT" || appStatus == "RETURNED") && appRef != ""
                ? "complete and submit your renewal application " + Enc(appRef) + " in the staff portal"
                : "submit a renewal application in the staff portal");
            b.Append(deadline.HasValue ? " by " + deadline.Value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) : " at least three months before the contract ends");
            if (sitting != "") b.Append(" for the " + Enc(sitting) + " sitting of the Governance Council");
            b.Append(". The application needs an application letter, a letter of motivation, the achievements evaluation form with evidence, and your online performance appraisal, and is reviewed by your supervisor.");

            string subject = "Contract renewal application" + (deadline.HasValue ? " due " + deadline.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "");
            string html = COOPERP_NewScreens_ContractRenewalView.BuildEmailHtml(SafeStr(r["emp_name"]), COOPERP_NewScreens_ContractRenewalView.EmailP(b.ToString()));
            try
            {
                string res = EmailSenderProtocol.SendHtmlEmail(html, email, subject, "MRU Human Resource");
                if (res != null && res.StartsWith("Email sent", StringComparison.OrdinalIgnoreCase)) status = "SENT";
                else { status = "FAILED"; error = res ?? "unknown error"; }
            }
            catch (Exception ex) { status = "FAILED"; error = ex.Message; }
        }
        if (error.Length > 250) error = error.Substring(0, 250);

        X(@"INSERT INTO hr_renewal_reminders (employee_id, contract_id, round_id, renewal_id, email, send_status, error_text, sent_by, sent_at)
            VALUES (@e, @c, @r, @rn, @em, @s, @er, @by, NOW())",
            new MySqlParameter("@e", empId), new MySqlParameter("@c", cid > 0 ? (object)cid : DBNull.Value),
            new MySqlParameter("@r", round != null ? (object)SafeInt(round["round_id"]) : DBNull.Value),
            new MySqlParameter("@rn", renewalId > 0 ? (object)renewalId : DBNull.Value),
            new MySqlParameter("@em", email == "" ? (object)DBNull.Value : email), new MySqlParameter("@s", status),
            new MySqlParameter("@er", error == "" ? (object)DBNull.Value : error), new MySqlParameter("@by", ActorTag));
        if (renewalId > 0)
        {
            try
            {
                X(@"INSERT INTO hr_renewal_audit (renewal_id, actor_username, action, old_status, new_status, payload_json, created_at)
                    VALUES (@r, @u, 'REMINDER_EMAIL', @s, @s, @p, NOW())",
                    new MySqlParameter("@r", renewalId), new MySqlParameter("@u", ActorTag), new MySqlParameter("@s", appStatus),
                    new MySqlParameter("@p", "{\"email\":\"" + Js(status + (email != "" ? " " + email : "") + (error != "" ? ": " + error : "")) + "\"}"));
            }
            catch { }
        }

        string msg = status == "SENT" ? "Sent to " + email : (status == "NO_EMAIL" ? "No email address on record" : "Could not be sent. Try again later.");
        Response.Write("{\"ok\":" + (status == "SENT" ? "true" : "false") + ",\"status\":\"" + status + "\",\"msg\":\"" + Js(msg) + "\"}");
    }

    // ── rounds ─────────────────────────────────────────────────────────
    private void AjaxSaveRound()
    {
        int id = SafeInt(F("round_id"));
        string title = F("title"), sitting = F("council_sitting"), notes = F("notes"), status = F("status").ToUpper();
        DateTime deadline, eligibleTo, councilDate;
        bool hasCouncilDate = DateTime.TryParseExact(F("council_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out councilDate);
        if (title == "" || title.Length > 200) { Response.Write("{\"ok\":false,\"msg\":\"Enter a title of up to 200 characters.\"}"); return; }
        if (sitting.Length > 100) { Response.Write("{\"ok\":false,\"msg\":\"The Council sitting can be up to 100 characters.\"}"); return; }
        if (!DateTime.TryParseExact(F("submission_deadline"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out deadline))
        { Response.Write("{\"ok\":false,\"msg\":\"Enter the submission deadline.\"}"); return; }
        if (!DateTime.TryParseExact(F("eligible_expiry_to"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out eligibleTo))
        { Response.Write("{\"ok\":false,\"msg\":\"Enter the contract end date limit.\"}"); return; }
        if (status != "OPEN" && status != "CLOSED") status = "OPEN";

        object cd = hasCouncilDate ? (object)councilDate : DBNull.Value;
        object nt = notes == "" ? (object)DBNull.Value : notes;
        object st = sitting == "" ? (object)DBNull.Value : sitting;
        if (id > 0)
        {
            int n = X(@"UPDATE hr_renewal_rounds SET title = @t, council_sitting = @s, council_date = @cd, submission_deadline = @dl,
                               eligible_expiry_to = @ef, status = @st, notes = @n, updated_at = NOW()
                         WHERE round_id = @id",
                new MySqlParameter("@t", title), new MySqlParameter("@s", st), new MySqlParameter("@cd", cd),
                new MySqlParameter("@dl", deadline), new MySqlParameter("@ef", eligibleTo), new MySqlParameter("@st", status),
                new MySqlParameter("@n", nt), new MySqlParameter("@id", id));
            Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Round updated.\"}" : "{\"ok\":false,\"msg\":\"Round not found.\"}");
        }
        else
        {
            X(@"INSERT INTO hr_renewal_rounds (title, council_sitting, council_date, submission_deadline, eligible_expiry_to, status, notes, created_by, created_at, updated_at)
                VALUES (@t, @s, @cd, @dl, @ef, @st, @n, @by, NOW(), NOW())",
                new MySqlParameter("@t", title), new MySqlParameter("@s", st), new MySqlParameter("@cd", cd),
                new MySqlParameter("@dl", deadline), new MySqlParameter("@ef", eligibleTo), new MySqlParameter("@st", status),
                new MySqlParameter("@n", nt), new MySqlParameter("@by", ActorTag));
            Response.Write("{\"ok\":true,\"msg\":\"Round created.\"}");
        }
    }

    private void AjaxCloseRound()
    {
        int id = SafeInt(F("round_id"));
        int n = X("UPDATE hr_renewal_rounds SET status = 'CLOSED', updated_at = NOW() WHERE round_id = @id AND status <> 'CLOSED'",
            new MySqlParameter("@id", id));
        Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Round closed. Staff can no longer apply under it.\"}" : "{\"ok\":false,\"msg\":\"The round is already closed.\"}");
    }

    // ── Council schedule (Excel / CSV): the same columns as the printed schedule ──
    public const string ScheduleSql =
        @"SELECT r.renewal_id, r.ref_no, r.status, r.emp_name, r.emp_code, r.staff_category, r.cur_job, r.cur_department,
                 r.cur_start, r.cur_end, r.requested_term_months, r.sup_recommendation, r.sup_term_months, r.hr_comments,
                 r.council_sitting, r.is_late, r.round_id,
                 (SELECT CONCAT(ROUND(ar.final_percentage,1), '%', IF(IFNULL(ar.classification,'') = '', '', CONCAT(' (', ar.classification, ')')))
                    FROM appraisal_records ar
                   WHERE ar.employee_id = r.employee_id AND ar.final_percentage IS NOT NULL
                   ORDER BY COALESCE(ar.employee_submitted_at, ar.created_at) DESC, ar.record_id DESC LIMIT 1) AS appraisal_score
          FROM hr_contract_renewals r ";

    public const string ScheduleOrder = " ORDER BY r.cur_department, r.emp_name";

    /// <summary>Column set shared by the printed schedule, the .xlsx and the .csv.</summary>
    public static readonly string[] ScheduleHeaders = {
        "No", "Ref", "Name", "Staff no", "Position", "Department", "Contract ends", "Requested term (months)",
        "Supervisor recommendation", "Latest appraisal score", "HR remarks", "Late" };

    /// <summary>One row per application, raw values (dates as DateTime, numbers as int, blanks as null).</summary>
    public static List<object[]> ScheduleRows(DataTable dt)
    {
        List<object[]> rows = new List<object[]>();
        int i = 0;
        foreach (DataRow r in dt.Rows)
        {
            i++;
            DateTime? end = D(r["cur_end"]);
            int term = SafeInt(r["requested_term_months"]);
            int supTerm = SafeInt(r["sup_term_months"]);
            string rec = RecLabel(SafeStr(r["sup_recommendation"]));
            if (rec != "" && supTerm > 0) rec += ", " + supTerm + " months";
            rows.Add(new object[] {
                i, SafeStr(r["ref_no"]), SafeStr(r["emp_name"]), SafeStr(r["emp_code"]), SafeStr(r["cur_job"]), SafeStr(r["cur_department"]),
                end.HasValue ? (object)end.Value : null, term > 0 ? (object)term : null, rec,
                SafeStr(r["appraisal_score"]), SafeStr(r["hr_comments"]).Trim(), SafeInt(r["is_late"]) == 1 ? "Yes" : "No" });
        }
        return rows;
    }

    /// <summary>Scope of a schedule request, as label/value pairs (used by the export and the print).</summary>
    public static List<KeyValuePair<string, string>> ScheduleScope(DataTable dt, string idsCsv, int round, string status, Func<string, DataTable> query)
    {
        List<KeyValuePair<string, string>> scope = new List<KeyValuePair<string, string>>();
        string sitting = "", roundTitle = "";
        if (round > 0)
        {
            DataTable rd = query("SELECT title, council_sitting FROM hr_renewal_rounds WHERE round_id = " + round);
            if (rd.Rows.Count > 0) { sitting = SafeStr(rd.Rows[0]["council_sitting"]); roundTitle = SafeStr(rd.Rows[0]["title"]); }
        }
        if (sitting == "" && dt.Rows.Count > 0) sitting = SafeStr(dt.Rows[0]["council_sitting"]);
        if (sitting != "") scope.Add(new KeyValuePair<string, string>("Council sitting", sitting));
        if (roundTitle != "") scope.Add(new KeyValuePair<string, string>("Round", roundTitle));
        bool byIds = false;
        foreach (string p in (idsCsv ?? "").Split(',')) { int v; if (int.TryParse(p.Trim(), out v) && v > 0) byIds = true; }
        string st = (status ?? "").Trim().ToUpper();
        if (byIds) scope.Add(new KeyValuePair<string, string>("Applications", "Selected (" + dt.Rows.Count + ")"));
        else scope.Add(new KeyValuePair<string, string>("Status", st == "ALL" ? "All submitted" : COOPERP_NewScreens_ContractRenewalView.StatusLabel(Array.IndexOf(Statuses, st) >= 0 ? st : "FORWARDED")));
        return scope;
    }

    private void ServeSchedule(bool xlsx)
    {
        List<MySqlParameter> ps = new List<MySqlParameter>();
        string ids = Request.QueryString["ids"], status = Request.QueryString["status"];
        string where = ScheduleWhere(ids, QsRound, status, ps);
        DataTable dt = Q(ScheduleSql + where + ScheduleOrder, ps.ToArray());

        HrExport.Report rep = new HrExport.Report("Council schedule: contract renewals", "council-schedule");
        rep.PreparedBy = CurrentUsername();
        foreach (KeyValuePair<string, string> kv in ScheduleScope(dt, ids, QsRound, status, delegate (string sql) { return Q(sql); }))
            rep.AddScope(kv.Key, kv.Value);
        HrExport.Sheet sh = rep.NewSheet("Council schedule");
        HrExport.Kind[] kinds = {
            HrExport.Kind.Number, HrExport.Kind.Text, HrExport.Kind.Text, HrExport.Kind.Text, HrExport.Kind.Text, HrExport.Kind.Text,
            HrExport.Kind.Date, HrExport.Kind.Number, HrExport.Kind.Text, HrExport.Kind.Text, HrExport.Kind.Text, HrExport.Kind.Text };
        double[] widths = { 6, 15, 26, 12, 26, 26, 13, 12, 26, 18, 45, 7 };
        for (int i = 0; i < ScheduleHeaders.Length; i++)
            sh.Cols.Add(new HrExport.Col(ScheduleHeaders[i], kinds[i], widths[i], false));
        foreach (object[] row in ScheduleRows(dt)) sh.Rows.Add(row);

        if (xlsx) HrExport.SendXlsx(Response, rep);
        else HrExport.SendCsv(Response, rep, 0);
    }

    /// <summary>Selected ids win; otherwise round (optional) + status (default FORWARDED; ALL = every live application).</summary>
    public static string ScheduleWhere(string idsCsv, int round, string status, List<MySqlParameter> ps)
    {
        List<string> ids = new List<string>();
        foreach (string p in (idsCsv ?? "").Split(','))
        {
            int v;
            if (int.TryParse(p.Trim(), out v) && v > 0) ids.Add(v.ToString());
        }
        if (ids.Count > 0) return "WHERE r.renewal_id IN (" + string.Join(",", ids.ToArray()) + ") ";
        string w = "WHERE 1=1 ";
        if (round > 0) { w += "AND r.round_id = @round "; ps.Add(new MySqlParameter("@round", round)); }
        string st = (status ?? "").Trim().ToUpper();
        if (st == "ALL") w += "AND r.status NOT IN ('DRAFT','WITHDRAWN') ";
        else if (Array.IndexOf(Statuses, st) >= 0) { w += "AND r.status = @st "; ps.Add(new MySqlParameter("@st", st)); }
        else w += "AND r.status = 'FORWARDED' ";
        return w;
    }

    public static string RecLabel(string r)
    {
        switch (r)
        {
            case "RECOMMEND": return "Recommended";
            case "RECOMMEND_WITH_CONDITIONS": return "Recommended with conditions";
            case "NOT_RECOMMENDED": return "Not recommended";
            default: return r ?? "";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE RENDER
    // ═══════════════════════════════════════════════════════════════════
    private void LoadPage()
    {
        string tab = QsTab;
        DataTable rounds = Q(@"SELECT r.*, (SELECT COUNT(*) FROM hr_contract_renewals a WHERE a.round_id = r.round_id) AS n_apps,
                                      (SELECT COUNT(*) FROM hr_contract_renewals a WHERE a.round_id = r.round_id AND a.status = 'FORWARDED') AS n_fwd
                               FROM hr_renewal_rounds r ORDER BY (r.status = 'OPEN') DESC, r.submission_deadline DESC");
        DataRow openRound = null;
        foreach (DataRow r in rounds.Rows) if (SafeStr(r["status"]) == "OPEN") { openRound = r; break; }

        litTabs.Text = string.Format(
            "<a class='hr-subtab{0}' href='ContractRenewals.aspx'>Applications</a><a class='hr-subtab{1}' href='ContractRenewals.aspx?tab=expiring'>Expiring contracts</a><a class='hr-subtab{2}' href='ContractRenewals.aspx?tab=rounds'>Rounds</a>",
            tab == "apps" ? " hr-subtab--active" : "", tab == "expiring" ? " hr-subtab--active" : "", tab == "rounds" ? " hr-subtab--active" : "");

        // open round: one line
        if (openRound != null)
        {
            DateTime? dl = D(openRound["submission_deadline"]);
            int daysTo = dl.HasValue ? (int)(dl.Value.Date - DateTime.Today).TotalDays : 0;
            StringBuilder b = new StringBuilder("<div class='hr-notice'><strong>Open round:</strong> " + Enc(SafeStr(openRound["title"])) + ".");
            if (SafeStr(openRound["council_sitting"]) != "")
                b.Append(" Council sitting " + Enc(SafeStr(openRound["council_sitting"])) + (D(openRound["council_date"]).HasValue ? " (" + FmtD(openRound["council_date"]) + ")" : "") + ".");
            if (dl.HasValue)
                b.Append(" Deadline " + FmtD(openRound["submission_deadline"]) + ", " +
                         (daysTo > 1 ? daysTo + " days left" : daysTo == 1 ? "1 day left" : daysTo == 0 ? "today" : "passed") + ".");
            b.Append(" Contracts ending on or before " + FmtD(openRound["eligible_expiry_to"]) + ".</div>");
            litRoundBanner.Text = b.ToString();
        }
        else litRoundBanner.Text = "<div class='hr-notice hr-notice--warn'>No renewal round is open. Open one on the Rounds tab.</div>";

        RenderKpis();
        pnlApps.Visible = tab == "apps";
        pnlExpiring.Visible = tab == "expiring";
        pnlRounds.Visible = tab == "rounds";

        if (tab == "apps") RenderApps(rounds);
        else if (tab == "expiring") RenderExpiring(rounds, openRound);
        else RenderRounds(rounds);
    }

    private const string ExpiringSql =
        @"SELECT c.ID AS cid, c.empID, c.contractStart, c.contractEnd, c.contractStatus, c.contract_type, c.departmentID,
                 IFNULL(j.jobname,'') AS jobname, IFNULL(d.dept_name,'') AS dept_name,
                 e.emp_name, e.EMP_CODE, e.EmpType, IFNULL(e.emp_email,'') AS emp_email,
                 DATEDIFF(c.contractEnd, CURDATE()) AS days_left,
                 a.renewal_id, a.ref_no, a.status AS app_status,
                 (SELECT MAX(m.sent_at) FROM hr_renewal_reminders m WHERE m.employee_id = c.empID AND m.contract_id = c.ID AND m.send_status = 'SENT') AS last_reminder,
                 (SELECT COUNT(*) FROM hr_renewal_reminders m WHERE m.employee_id = c.empID AND m.contract_id = c.ID AND m.send_status = 'SENT') AS n_reminders
          FROM (SELECT e2.empID, hr_current_contract_id(e2.empID) AS cid FROM hrm_employee e2) x
          JOIN hrm_emp_contracts c ON c.ID = x.cid
          JOIN hrm_employee e ON e.empID = x.empID
          LEFT JOIN hrm_jobs j ON j.ID = c.jobID
          LEFT JOIN hrm_departments d ON d.ID = c.departmentID
          LEFT JOIN hr_contract_renewals a ON a.renewal_id =
                (SELECT MAX(a2.renewal_id) FROM hr_contract_renewals a2 WHERE a2.contract_id = c.ID AND a2.status <> 'WITHDRAWN')
          WHERE c.contractStatus NOT IN ('TERMINATED','RESIGNED')
            AND IFNULL(e.employment_status,'ACTIVE') = 'ACTIVE'
            AND c.contractEnd >= DATE_SUB(CURDATE(), INTERVAL 60 DAY) ";

    private void RenderKpis()
    {
        int rid = QsRound;
        List<MySqlParameter> ps = new List<MySqlParameter>();
        string w = "";
        if (rid > 0) { w = " WHERE round_id = @r"; ps.Add(new MySqlParameter("@r", rid)); }
        DataTable k = Q(@"SELECT SUM(status = 'AWAITING_HR') AS hr, SUM(status = 'FORWARDED') AS fwd,
                                 SUM(status = 'CONTRACT_ISSUED') AS issued,
                                 SUM(status = 'AWAITING_HR' AND is_late = 1 AND submitted_at IS NOT NULL) AS hr_late
                          FROM hr_contract_renewals" + w, ps.ToArray());
        DataRow r = k.Rows[0];
        int noApp = SafeInt(Q("SELECT COUNT(*) AS n FROM (" + ExpiringSql + " AND c.contractEnd <= DATE_ADD(CURDATE(), INTERVAL 90 DAY)) z WHERE z.renewal_id IS NULL").Rows[0]["n"]);
        int hrLate = SafeInt(r["hr_late"]);

        StringBuilder sb = new StringBuilder();
        Kpi(sb, noApp, "No application", "ContractRenewals.aspx?tab=expiring&amp;win=90&amp;noapp=1", "Contracts ending within 90 days", noApp > 0);
        Kpi(sb, SafeInt(r["hr"]), "With HR", "ContractRenewals.aspx?status=AWAITING_HR", hrLate > 0 ? hrLate + " submitted late" : "Awaiting verification", false);
        Kpi(sb, SafeInt(r["fwd"]), "At Council", "ContractRenewals.aspx?status=FORWARDED", "Awaiting decision", false);
        Kpi(sb, SafeInt(r["issued"]), "Issued", "ContractRenewals.aspx?status=CONTRACT_ISSUED", "New contracts", false);
        litKpis.Text = sb.ToString();
    }

    private static void Kpi(StringBuilder sb, int val, string label, string href, string sub, bool alert)
    {
        sb.AppendFormat("<a class='hr-kpi{0}' href='{1}'><div class='hr-kpi__label'>{2}</div><div class='hr-kpi__value'>{3}</div><div class='hr-kpi__sub'>{4}</div></a>",
            alert ? " hr-kpi--alert" : "", href, Enc(label), val.ToString("N0"), Enc(sub));
    }

    private string RoundOptions(DataTable rounds, int selected, string allLabel)
    {
        StringBuilder sb = new StringBuilder("<option value='0'>" + allLabel + "</option>");
        foreach (DataRow r in rounds.Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2}{3}</option>", SafeInt(r["round_id"]), SafeInt(r["round_id"]) == selected ? " selected" : "",
                Enc(SafeStr(r["title"])), SafeStr(r["status"]) == "OPEN" ? " (open)" : "");
        return sb.ToString();
    }

    private string DeptOptions(int selected)
    {
        StringBuilder sb = new StringBuilder("<option value='0'>All departments</option>");
        foreach (DataRow d in Q("SELECT ID, dept_name FROM hrm_departments ORDER BY dept_name").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", SafeInt(d["ID"]), SafeInt(d["ID"]) == selected ? " selected" : "", Enc(SafeStr(d["dept_name"])));
        return sb.ToString();
    }

    private static string Filter(string label, string forId, string control, bool grow)
    {
        return "<div class='hr-filter" + (grow ? " hr-filter--grow" : "") + "'><label for='" + forId + "'>" + label + "</label>" + control + "</div>";
    }

    // ── applications tab ───────────────────────────────────────────────
    private void RenderApps(DataTable rounds)
    {
        StringBuilder f = new StringBuilder();
        f.Append(Filter("Round", "fRound", "<select id='fRound' class='hr-select'>" + RoundOptions(rounds, QsRound, "All rounds") + "</select>", false));
        StringBuilder so = new StringBuilder("<select id='fStatus' class='hr-select'><option value=''>All statuses</option>");
        foreach (string s in Statuses)
            so.AppendFormat("<option value='{0}'{1}>{2}</option>", s, s == QsStatus ? " selected" : "", Enc(COOPERP_NewScreens_ContractRenewalView.StatusLabel(s)));
        f.Append(Filter("Status", "fStatus", so.Append("</select>").ToString(), false));
        StringBuilder co = new StringBuilder("<select id='fCat' class='hr-select'><option value=''>All categories</option>");
        foreach (string c in new string[] { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" })
            co.AppendFormat("<option value='{0}'{1}>{2}</option>", c, c == QsCat ? " selected" : "", Enc(COOPERP_NewScreens_ContractRenewalView.CategoryLabel(c)));
        f.Append(Filter("Category", "fCat", co.Append("</select>").ToString(), false));
        f.Append(Filter("Department", "fDept", "<select id='fDept' class='hr-select'>" + DeptOptions(QsDept) + "</select>", false));
        f.Append(Filter("Submission", "fLate", string.Format("<select id='fLate' class='hr-select'><option value=''>Late or on time</option><option value='1'{0}>Late only</option><option value='0'{1}>On time only</option></select>",
            QsLate == "1" ? " selected" : "", QsLate == "0" ? " selected" : ""), false));
        f.Append(Filter("Search", "fQ", "<input type='text' id='fQ' class='hr-input' placeholder='Name, ref or staff no' value='" + Enc(QsSearch) + "' />", true));
        f.Append("<div class='hr-filters__actions'><button type='button' class='hr-btn hr-btn--primary' onclick='applyFilters()'>Filter</button><a class='hr-btn hr-btn--secondary' href='ContractRenewals.aspx'>Reset</a></div>");
        litFilters.Text = f.ToString();

        List<MySqlParameter> ps = new List<MySqlParameter>();
        StringBuilder w = new StringBuilder(" WHERE 1=1");
        if (QsRound > 0) { w.Append(" AND r.round_id = @round"); ps.Add(new MySqlParameter("@round", QsRound)); }
        if (Array.IndexOf(Statuses, QsStatus) >= 0) { w.Append(" AND r.status = @st"); ps.Add(new MySqlParameter("@st", QsStatus)); }
        if (QsCat != "") { w.Append(" AND r.staff_category = @cat"); ps.Add(new MySqlParameter("@cat", QsCat)); }
        if (QsDept > 0) { w.Append(" AND r.cur_department_id = @dept"); ps.Add(new MySqlParameter("@dept", QsDept)); }
        if (QsLate == "1") w.Append(" AND r.is_late = 1");
        if (QsLate == "0") w.Append(" AND r.is_late = 0");
        if (QsSearch != "")
        {
            w.Append(" AND (r.emp_name LIKE @q OR r.ref_no LIKE @q OR r.emp_code LIKE @q)");
            ps.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }

        DataTable dt = Q(@"SELECT r.renewal_id, r.ref_no, r.status, r.emp_name, r.emp_code, r.staff_category, r.cur_job, r.cur_department,
                                  r.cur_end, DATEDIFF(r.cur_end, CURDATE()) AS days_left, r.sup_recommendation, r.submitted_at, r.is_late,
                                  r.requested_term_months, r.council_sitting
                           FROM hr_contract_renewals r" + w +
                         @" ORDER BY FIELD(r.status,'AWAITING_HR','FORWARDED','APPROVED','AWAITING_SUPERVISOR','RETURNED','DRAFT','DEFERRED','CONTRACT_ISSUED','NOT_APPROVED','WITHDRAWN'),
                                     r.cur_end, r.emp_name
                            LIMIT 1000", ps.ToArray());

        StringBuilder t = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            int id = SafeInt(r["renewal_id"]);
            string st = SafeStr(r["status"]);
            bool selectable = st == "AWAITING_HR" || st == "FORWARDED";
            DateTime? end = D(r["cur_end"]);
            string code = SafeStr(r["emp_code"]), cat = COOPERP_NewScreens_ContractRenewalView.CategoryLabel(SafeStr(r["staff_category"]));
            t.AppendFormat("<tr data-id='{0}' data-status='{1}'>", id, st);
            t.AppendFormat("<td class='cr-cb'>{0}</td>", selectable ? "<input type='checkbox' class='cr-row-cb' value='" + id + "' data-status='" + st + "' onchange='selChanged()' />" : "");
            t.AppendFormat("<td class='cr-nowrap'><a href='ContractRenewalView.aspx?id={0}'><span class='hr-code'>{1}</span></a></td>", id, Enc(SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : "#" + id));
            t.AppendFormat("<td><a class='hr-btn--link' href='ContractRenewalView.aspx?id={0}'>{1}</a><span class='hr-sub'>{2}</span></td>", id,
                Enc(SafeStr(r["emp_name"])), Enc(code != "" && cat != "" ? code + ", " + cat : code + cat));
            t.AppendFormat("<td>{0}<span class='hr-sub'>{1}</span></td>", Enc(SafeStr(r["cur_job"])), Enc(SafeStr(r["cur_department"])));
            t.AppendFormat("<td class='cr-nowrap'>{0}{1}</td>", FmtD(r["cur_end"]),
                end.HasValue && st != "CONTRACT_ISSUED" && st != "NOT_APPROVED" && st != "WITHDRAWN" ? "<div>" + COOPERP_NewScreens_ContractRenewalView.DaysLeftHtml(SafeInt(r["days_left"])) + "</div>" : "");
            t.AppendFormat("<td>{0}{1}</td>", COOPERP_NewScreens_ContractRenewalView.StatusBadge(st),
                st == "FORWARDED" && SafeStr(r["council_sitting"]) != "" ? "<span class='hr-sub'>" + Enc(SafeStr(r["council_sitting"])) + "</span>" : "");
            t.AppendFormat("<td>{0}</td>", COOPERP_NewScreens_ContractRenewalView.RecBadge(SafeStr(r["sup_recommendation"])));
            t.AppendFormat("<td class='cr-nowrap'>{0}</td>", FmtD(r["submitted_at"]));
            t.AppendFormat("<td>{0}</td>", SafeInt(r["is_late"]) == 1 ? "<span class='hr-badge hr-badge--bad'>Late</span>" : "");
            t.AppendFormat("<td class='cr-nowrap hr-right'><a class='hr-btn hr-btn--secondary hr-btn--sm' href='ContractRenewalView.aspx?id={0}'>Open</a> <a class='hr-btn hr-btn--secondary hr-btn--sm' target='_blank' href='ContractRenewalPrint.aspx?id={0}'>Print</a></td>", id);
            t.Append("</tr>");
        }
        if (dt.Rows.Count == 0) t.Append("<tr><td colspan='10' class='hr-empty'>No applications match these filters.</td></tr>");
        litApps.Text = t.ToString();
        litAppsCount.Text = dt.Rows.Count.ToString("N0") + (dt.Rows.Count == 1 ? " application" : " applications") + (dt.Rows.Count >= 1000 ? " (first 1,000)" : "");

        DataRow open = null;
        foreach (DataRow r in rounds.Rows) if (SafeStr(r["status"]) == "OPEN") { open = r; break; }
        hfDefaultSitting.Value = open != null ? SafeStr(open["council_sitting"]) : "";
    }

    // ── expiring tab ───────────────────────────────────────────────────
    private void RenderExpiring(DataTable rounds, DataRow openRound)
    {
        string win = QsWin;
        DateTime to;
        string winLabel;
        DataRow round = null;
        if (win == "round")
        {
            if (QsRound > 0) foreach (DataRow r in rounds.Rows) if (SafeInt(r["round_id"]) == QsRound) round = r;
            if (round == null) round = openRound;
            DateTime? ef = round != null ? D(round["eligible_expiry_to"]) : null;
            to = ef.HasValue ? ef.Value : DateTime.Today.AddDays(90);
            winLabel = round != null ? "ending on or before " + to.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "ending within 90 days";
        }
        else
        {
            to = DateTime.Today.AddDays(int.Parse(win));
            winLabel = "ending within " + win + " days";
        }

        StringBuilder f = new StringBuilder();
        StringBuilder wo = new StringBuilder("<select id='xWin' class='hr-select'>");
        foreach (string[] o in new string[][] { new[] { "30", "Next 30 days" }, new[] { "60", "Next 60 days" }, new[] { "90", "Next 90 days" }, new[] { "180", "Next 180 days" }, new[] { "round", "Round window" } })
            wo.AppendFormat("<option value='{0}'{1}>{2}</option>", o[0], o[0] == win ? " selected" : "", o[1]);
        f.Append(Filter("Ending", "xWin", wo.Append("</select>").ToString(), false));
        f.Append(Filter("Round", "xRound", "<select id='xRound' class='hr-select'>" + RoundOptions(rounds, QsRound > 0 ? QsRound : (openRound != null ? SafeInt(openRound["round_id"]) : 0), "Open round") + "</select>", false));
        f.Append(Filter("Department", "xDept", "<select id='xDept' class='hr-select'>" + DeptOptions(QsDept) + "</select>", false));
        f.Append(Filter("Search", "xQ", "<input type='text' id='xQ' class='hr-input' placeholder='Name or staff no' value='" + Enc(QsSearch) + "' />", true));
        f.AppendFormat("<label class='cr-inline'><input type='checkbox' id='xNoApp'{0} /> Without an application</label>", QsNoApp ? " checked" : "");
        f.Append("<div class='hr-filters__actions'><button type='button' class='hr-btn hr-btn--primary' onclick='applyExpFilters()'>Show</button></div>");
        litExpFilters.Text = f.ToString();

        List<MySqlParameter> ps = new List<MySqlParameter> { new MySqlParameter("@to", to) };
        StringBuilder w = new StringBuilder(" AND c.contractEnd <= @to");
        if (QsDept > 0) { w.Append(" AND c.departmentID = @dept"); ps.Add(new MySqlParameter("@dept", QsDept)); }
        if (QsSearch != "") { w.Append(" AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q)"); ps.Add(new MySqlParameter("@q", "%" + QsSearch + "%")); }
        string sql = ExpiringSql + w + (QsNoApp ? " HAVING renewal_id IS NULL" : "") + " ORDER BY c.contractEnd, e.emp_name";
        DataTable dt = Q(sql, ps.ToArray());

        int noApp = 0;
        StringBuilder t = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            int emp = SafeInt(r["empID"]);
            int rid = SafeInt(r["renewal_id"]);
            string ast = SafeStr(r["app_status"]);
            if (rid == 0) noApp++;
            string email = SafeStr(r["emp_email"]).Trim();
            bool hasEmail = email.Contains("@");
            string code = SafeStr(r["EMP_CODE"]), cat = COOPERP_NewScreens_ContractRenewalView.CategoryLabel(StaffCategory(SafeStr(r["EmpType"])));
            t.AppendFormat("<tr data-emp='{0}'>", emp);
            t.AppendFormat("<td class='cr-cb'>{0}</td>", hasEmail
                ? "<input type='checkbox' class='cr-exp-cb' value='" + emp + "' data-name='" + Enc(SafeStr(r["emp_name"])) + "' onchange='expSelChanged()' />" : "");
            t.AppendFormat("<td><strong>{0}</strong><span class='hr-sub'>{1}</span></td>", Enc(SafeStr(r["emp_name"])), Enc(code != "" ? code + ", " + cat : cat));
            t.AppendFormat("<td>{0}<span class='hr-sub'>{1}</span></td>", Enc(SafeStr(r["jobname"])), Enc(SafeStr(r["dept_name"])));
            t.AppendFormat("<td class='cr-nowrap'>{0}<span class='hr-sub'>{1}</span></td>",
                Enc(COOPERP_NewScreens_ContractRenewalView.Words(SafeStr(r["contract_type"]))),
                D(r["contractStart"]).HasValue ? "From " + FmtD(r["contractStart"]) : "");
            t.AppendFormat("<td class='cr-nowrap'>{0}<div>{1}</div></td>", FmtD(r["contractEnd"]), COOPERP_NewScreens_ContractRenewalView.DaysLeftHtml(SafeInt(r["days_left"])));
            t.AppendFormat("<td class='cr-nowrap'>{0}</td>", rid > 0
                ? "<a href='ContractRenewalView.aspx?id=" + rid + "'><span class='hr-code'>" + Enc(SafeStr(r["ref_no"])) + "</span></a> " + COOPERP_NewScreens_ContractRenewalView.StatusBadge(ast)
                : "<span class='hr-badge hr-badge--bad'>No application</span>");
            t.AppendFormat("<td>{0}</td>", hasEmail ? Enc(email) : "<span class='hr-badge hr-badge--neutral'>No email</span>");
            t.AppendFormat("<td class='cr-nowrap' id='rem_{0}'>{1}</td>", emp,
                SafeInt(r["n_reminders"]) > 0 ? FmtDT(r["last_reminder"]) + (SafeInt(r["n_reminders"]) > 1 ? "<span class='hr-sub'>" + SafeInt(r["n_reminders"]) + " reminders sent</span>" : "") : "");
            t.Append("</tr>");
        }
        if (dt.Rows.Count == 0) t.Append("<tr><td colspan='8' class='hr-empty'>No contracts end in this period.</td></tr>");
        litExpiring.Text = t.ToString();
        litExpCount.Text = string.Format("{0} {1} {2} or ended in the last 60 days. {3} without an application.",
            dt.Rows.Count, dt.Rows.Count == 1 ? "contract" : "contracts", Enc(winLabel), noApp);
    }

    private static string StaffCategory(string empType)
    {
        string t = (empType ?? "").ToUpper();
        if (t.Contains("ACADEMIC") && !t.Contains("NON")) return "ACADEMIC";
        if (t.Contains("ADMIN") || t.Contains("NON-ACADEMIC")) return "ADMINISTRATIVE";
        return "SUPPORT";
    }

    // ── rounds tab ─────────────────────────────────────────────────────
    private void RenderRounds(DataTable rounds)
    {
        StringBuilder t = new StringBuilder();
        foreach (DataRow r in rounds.Rows)
        {
            int id = SafeInt(r["round_id"]);
            bool open = SafeStr(r["status"]) == "OPEN";
            t.AppendFormat("<tr data-round='{0}' data-title='{1}' data-sitting='{2}' data-cdate='{3}' data-deadline='{4}' data-eligible='{5}' data-status='{6}' data-notes='{7}'>",
                id, Enc(SafeStr(r["title"])), Enc(SafeStr(r["council_sitting"])), IsoD(r["council_date"]), IsoD(r["submission_deadline"]),
                IsoD(r["eligible_expiry_to"]), Enc(SafeStr(r["status"])), Enc(SafeStr(r["notes"])));
            t.AppendFormat("<td><strong>{0}</strong>{1}</td>", Enc(SafeStr(r["title"])),
                SafeStr(r["notes"]) != "" ? "<span class='hr-sub'>" + Enc(SafeStr(r["notes"])) + "</span>" : "");
            t.AppendFormat("<td>{0}{1}</td>", Enc(SafeStr(r["council_sitting"])), D(r["council_date"]).HasValue ? "<span class='hr-sub'>" + FmtD(r["council_date"]) + "</span>" : "");
            t.AppendFormat("<td class='cr-nowrap'>{0}</td><td class='cr-nowrap'>{1}</td>", FmtD(r["submission_deadline"]), FmtD(r["eligible_expiry_to"]));
            t.AppendFormat("<td>{0}</td>", open ? "<span class='hr-badge hr-badge--ok'>Open</span>" : "<span class='hr-badge hr-badge--neutral'>Closed</span>");
            t.AppendFormat("<td class='hr-num'><a href='ContractRenewals.aspx?round={0}'>{1}</a></td><td class='hr-num'>{2}</td>", id, SafeInt(r["n_apps"]), SafeInt(r["n_fwd"]));
            t.AppendFormat("<td class='cr-nowrap hr-right'><button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick='editRound(this)'>Edit</button> {0}" +
                           "<a class='hr-btn hr-btn--secondary hr-btn--sm' target='_blank' href='ContractRenewalPrint.aspx?schedule=1&amp;round={1}'>Schedule</a></td>",
                open ? "<button type='button' class='hr-btn hr-btn--danger hr-btn--sm' onclick='closeRound(" + id + ")'>Close</button> " : "", id);
            t.Append("</tr>");
        }
        if (rounds.Rows.Count == 0) t.Append("<tr><td colspan='8' class='hr-empty'>No rounds yet.</td></tr>");
        litRounds.Text = t.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string Js(string val) { return COOPERP_NewScreens_ContractRenewalView.Js(val); }
    private static string Enc(string s) { return HttpUtility.HtmlEncode(s ?? ""); }
    private static int SafeInt(object val) { return COOPERP_NewScreens_ContractRenewalView.SafeInt(val); }
    private static string SafeStr(object val) { return COOPERP_NewScreens_ContractRenewalView.SafeStr(val); }
    private static DateTime? D(object v) { return COOPERP_NewScreens_ContractRenewalView.D(v); }
    private static string FmtD(object v) { return COOPERP_NewScreens_ContractRenewalView.FmtD(v); }
    private static string FmtDT(object v) { return COOPERP_NewScreens_ContractRenewalView.FmtDT(v); }
    private static string IsoD(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("yyyy-MM-dd") : ""; }

    private DataTable Q(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }

    private int X(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
