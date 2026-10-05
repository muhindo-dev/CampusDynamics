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
/// Plus ?ajax=schedule_csv: the Council schedule as CSV (print version: ContractRenewalPrint.aspx?schedule=1).
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
            if (ajax == "schedule_csv") { ServeScheduleCsv(); return; }
            HandleAjax(ajax);
            return;
        }

        if (!IsPostBack)
        {
            if (!IsCallerAuthenticated()) return;   // SidebarMaster redirects to login
            if (!HasHrAccess())
            {
                pnlMain.Visible = false;
                litError.Text = "<div class='cr-alert cr-alert--error'>Access denied. Contract renewals are available to HR and administrators only.</div>";
                return;
            }
            try { LoadPage(); }
            catch (Exception ex)
            {
                litError.Text = "<div class='cr-alert cr-alert--error'>Error loading contract renewals: " + Enc(ex.Message) + "</div>";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH (same rule as the appraisal screens)
    // ═══════════════════════════════════════════════════════════════════
    private bool IsCallerAuthenticated()
    {
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (Session != null)
            {
                object u = Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    private string CurrentUsername()
    {
        try
        {
            if (Session != null && Session["username"] != null && !string.IsNullOrEmpty(Session["username"].ToString().Trim()))
                return Session["username"].ToString().Trim();
        }
        catch { }
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                return User.Identity.Name ?? "";
        }
        catch { }
        return "";
    }

    private bool? _hrAccess;

    /// <summary>RBAC admin wildcard / sys role admin|hr_manager / legacy my_aspnet HR or admin roles.</summary>
    private bool HasHrAccess()
    {
        if (_hrAccess.HasValue) return _hrAccess.Value;
        bool ok = false;
        try
        {
            if (IsCallerAuthenticated())
            {
                if (RoleAccessService.IsAdmin()) ok = true;
                string u = CurrentUsername();
                if (!ok && !string.IsNullOrEmpty(u))
                {
                    DataTable dt = Q(
                        @"SELECT
                            (SELECT COUNT(*) FROM sys_user_roles ur
                               JOIN sys_roles r ON r.id = ur.role_id
                              WHERE ur.username = @u AND ur.is_active = 1 AND r.is_active = 1
                                AND (ur.expires_at IS NULL OR ur.expires_at > NOW())
                                AND r.role_code IN ('admin','hr_manager'))
                          + (SELECT COUNT(*) FROM my_aspnet_users mu
                               JOIN my_aspnet_usersinroles mur ON mur.userId = mu.id
                               JOIN my_aspnet_roles mr ON mr.id = mur.roleId
                              WHERE mu.name = @u
                                AND mr.name IN ('Administrator','System Admin','Human Resource','Human Resource Manager')) AS n",
                        new MySqlParameter("@u", u));
                    ok = dt.Rows.Count > 0 && SafeInt(dt.Rows[0]["n"]) > 0;
                }
            }
        }
        catch { ok = false; }
        _hrAccess = ok;
        return ok;
    }

    private string ActorTag { get { return "eadmin:" + CurrentUsername(); } }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            if (!IsCallerAuthenticated())
                Response.Write("{\"ok\":false,\"msg\":\"Your session has expired. Please sign in again, then retry.\"}");
            else if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                     || !MarksAntiForgeryService.ValidateRequest())
                Response.Write("{\"ok\":false,\"msg\":\"Security validation failed. Please refresh and try again.\"}");
            else if (!HasHrAccess())
                Response.Write("{\"ok\":false,\"msg\":\"Access denied. HR or administrator access is required.\"}");
            else
            {
                switch (action)
                {
                    case "send_reminder": AjaxSendReminder(); break;
                    case "save_round":    AjaxSaveRound(); break;
                    case "close_round":   AjaxCloseRound(); break;
                    default: Response.Write("{\"ok\":false,\"msg\":\"Unknown action\"}"); break;
                }
            }
        }
        catch (System.Threading.ThreadAbortException) { }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"msg\":\"" + Js(ex.Message) + "\"}");
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
        if (dt.Rows.Count == 0) { Response.Write("{\"ok\":false,\"status\":\"ERROR\",\"msg\":\"Employee not found\"}"); return; }
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
            string deadline = round != null && D(round["submission_deadline"]).HasValue ? D(round["submission_deadline"]).Value.ToString("dddd d MMMM yyyy") : "";
            string sitting = round != null ? SafeStr(round["council_sitting"]) : "";
            StringBuilder b = new StringBuilder();
            b.Append("<p style='margin:0 0 12px;'>Our records show that your current contract");
            if (SafeStr(r["jobname"]) != "") b.Append(" as <strong>" + Enc(SafeStr(r["jobname"])) + "</strong>");
            if (SafeStr(r["dept_name"]) != "") b.Append(" (" + Enc(SafeStr(r["dept_name"])) + ")");
            if (end.HasValue)
                b.Append(end.Value.Date < DateTime.Today ? " <strong style='color:#dc3545;'>ended on " + end.Value.ToString("d MMMM yyyy") + "</strong>."
                                                         : " ends on <strong>" + end.Value.ToString("d MMMM yyyy") + "</strong>.");
            else b.Append(" is due for renewal.");
            b.Append("</p>");
            if (appStatus == "DRAFT" || appStatus == "RETURNED")
                b.Append("<p style='margin:0 0 12px;'>You have started a renewal application" + (appRef != "" ? " (<strong>" + Enc(appRef) + "</strong>)" : "") +
                         (appStatus == "RETURNED" ? " that was returned to you for correction" : "") + ", but it has <strong>not yet been submitted</strong>.</p>");
            b.Append("<p style='margin:0 0 12px;'>The HR Manual requires staff who wish to have their contracts renewed to apply <strong>at least three (3) months before expiry</strong>.");
            if (sitting != "") b.Append(" Applications will be presented to the Governance Council at its <strong>" + Enc(sitting) + "</strong> sitting.");
            b.Append("</p>");
            if (deadline != "")
                b.Append("<p style='margin:0 0 12px;'>The deadline for complete applications is <strong style='color:#c0392b;'>" + Enc(deadline) + "</strong>.</p>");
            b.Append("<p style='margin:0 0 6px;'>Your application in the staff portal must include:</p><ul style='margin:0 0 12px 18px;padding:0;'>" +
                     "<li>an application letter;</li><li>a letter of motivation;</li>" +
                     "<li>the Evaluation Form for Achievements of Staff Responsibilities and Key Performance Areas (with evidence);</li>" +
                     "<li>your online performance appraisal.</li></ul>");
            b.Append("<p style='margin:0 0 12px;'>Please consult your supervisor, who reviews and recommends your application. Note that an expired contract cannot remain on the payroll.</p>");
            string subject = "[MRU HR] Apply for the renewal of your contract" + (deadline != "" ? " by " + D(round["submission_deadline"]).Value.ToString("d MMM yyyy") : "");
            string html = COOPERP_NewScreens_ContractRenewalView.BuildEmailHtml(SafeStr(r["emp_name"]), "Contract renewal &mdash; please apply", b.ToString());
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

        string msg = status == "SENT" ? "Sent to " + email : (status == "NO_EMAIL" ? "No email address on record" : "Failed: " + error);
        Response.Write("{\"ok\":" + (status == "SENT" ? "true" : "false") + ",\"status\":\"" + status + "\",\"msg\":\"" + Js(msg) + "\"}");
    }

    // ── rounds ─────────────────────────────────────────────────────────
    private void AjaxSaveRound()
    {
        int id = SafeInt(F("round_id"));
        string title = F("title"), sitting = F("council_sitting"), notes = F("notes"), status = F("status").ToUpper();
        DateTime deadline, eligibleTo, councilDate;
        bool hasCouncilDate = DateTime.TryParseExact(F("council_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out councilDate);
        if (title == "" || title.Length > 200) { Response.Write("{\"ok\":false,\"msg\":\"Title is required (max 200 characters)\"}"); return; }
        if (sitting.Length > 100) { Response.Write("{\"ok\":false,\"msg\":\"Council sitting is too long (max 100 characters)\"}"); return; }
        if (!DateTime.TryParseExact(F("submission_deadline"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out deadline))
        { Response.Write("{\"ok\":false,\"msg\":\"Submission deadline is required\"}"); return; }
        if (!DateTime.TryParseExact(F("eligible_expiry_to"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out eligibleTo))
        { Response.Write("{\"ok\":false,\"msg\":\"'Contracts ending on or before' date is required\"}"); return; }
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
            Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Round updated\"}" : "{\"ok\":false,\"msg\":\"Round not found\"}");
        }
        else
        {
            X(@"INSERT INTO hr_renewal_rounds (title, council_sitting, council_date, submission_deadline, eligible_expiry_to, status, notes, created_by, created_at, updated_at)
                VALUES (@t, @s, @cd, @dl, @ef, @st, @n, @by, NOW(), NOW())",
                new MySqlParameter("@t", title), new MySqlParameter("@s", st), new MySqlParameter("@cd", cd),
                new MySqlParameter("@dl", deadline), new MySqlParameter("@ef", eligibleTo), new MySqlParameter("@st", status),
                new MySqlParameter("@n", nt), new MySqlParameter("@by", ActorTag));
            Response.Write("{\"ok\":true,\"msg\":\"Round created\"}");
        }
    }

    private void AjaxCloseRound()
    {
        int id = SafeInt(F("round_id"));
        int n = X("UPDATE hr_renewal_rounds SET status = 'CLOSED', updated_at = NOW() WHERE round_id = @id AND status <> 'CLOSED'",
            new MySqlParameter("@id", id));
        Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Round closed. Staff can no longer apply under it.\"}" : "{\"ok\":false,\"msg\":\"Round not found or already closed\"}");
    }

    // ── Council schedule CSV ───────────────────────────────────────────
    public const string ScheduleSql =
        @"SELECT r.renewal_id, r.ref_no, r.status, r.emp_name, r.emp_code, r.staff_category, r.cur_job, r.cur_department,
                 r.cur_start, r.cur_end, r.requested_term_months, r.sup_recommendation, r.sup_term_months, r.hr_comments,
                 r.council_sitting, r.is_late,
                 (SELECT CONCAT(ROUND(ar.final_percentage,1), '%', IF(IFNULL(ar.classification,'') = '', '', CONCAT(' - ', ar.classification)))
                    FROM appraisal_records ar
                   WHERE ar.employee_id = r.employee_id AND ar.final_percentage IS NOT NULL
                   ORDER BY COALESCE(ar.employee_submitted_at, ar.created_at) DESC, ar.record_id DESC LIMIT 1) AS appraisal_score
          FROM hr_contract_renewals r ";

    private void ServeScheduleCsv()
    {
        Response.Clear();
        if (!IsCallerAuthenticated())
        {
            Response.Redirect("~/Default.aspx?ReturnUrl=" + HttpUtility.UrlEncode(Request.RawUrl), true);
            return;
        }
        if (!HasHrAccess())
        {
            Response.StatusCode = 403;
            Response.ContentType = "text/plain";
            Response.Write("Access denied. HR or administrator access is required.");
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
            return;
        }

        List<MySqlParameter> ps = new List<MySqlParameter>();
        string where = ScheduleWhere(Request.QueryString["ids"], QsRound, Request.QueryString["status"], ps);
        DataTable dt = Q(ScheduleSql + where + " ORDER BY r.cur_department, r.emp_name", ps.ToArray());

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("No,Ref,Name,Employee No,Category,Position,Department,Current Contract End,Requested Term (months),Supervisor Recommendation,Supervisor Term (months),Appraisal Score,HR Remarks,Council Sitting,Status,Late");
        int i = 0;
        foreach (DataRow r in dt.Rows)
        {
            i++;
            DateTime? end = D(r["cur_end"]);
            sb.AppendLine(string.Join(",", new string[] {
                i.ToString(), Csv(SafeStr(r["ref_no"])), Csv(SafeStr(r["emp_name"])), Csv(SafeStr(r["emp_code"])),
                Csv(COOPERP_NewScreens_ContractRenewalView.CategoryLabel(SafeStr(r["staff_category"]))),
                Csv(SafeStr(r["cur_job"])), Csv(SafeStr(r["cur_department"])), end.HasValue ? end.Value.ToString("yyyy-MM-dd") : "",
                SafeInt(r["requested_term_months"]) > 0 ? SafeInt(r["requested_term_months"]).ToString() : "",
                Csv(RecLabel(SafeStr(r["sup_recommendation"]))),
                SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]).ToString() : "",
                Csv(SafeStr(r["appraisal_score"])), Csv(SafeStr(r["hr_comments"])), Csv(SafeStr(r["council_sitting"])),
                Csv(COOPERP_NewScreens_ContractRenewalView.StatusLabel(SafeStr(r["status"]))), SafeInt(r["is_late"]) == 1 ? "Yes" : "No" }));
        }
        Response.ContentType = "text/csv";
        Response.ContentEncoding = Encoding.UTF8;
        Response.AddHeader("Cache-Control", "private, no-store");
        Response.AddHeader("Content-Disposition", "attachment; filename=\"Council_Schedule_Contract_Renewals_" + DateTime.Now.ToString("yyyyMMdd") + ".csv\"");
        Response.BinaryWrite(Encoding.UTF8.GetPreamble());
        Response.Write(sb.ToString());
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
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

    private static string Csv(string s)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
        if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;   // no formula injection in Excel
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string RecLabel(string r)
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

        // tabs
        litTabs.Text = string.Format(
            "<a class='cr-tab{0}' href='ContractRenewals.aspx'>Applications</a><a class='cr-tab{1}' href='ContractRenewals.aspx?tab=expiring'>Expiring contracts</a><a class='cr-tab{2}' href='ContractRenewals.aspx?tab=rounds'>Rounds</a>",
            tab == "apps" ? " cr-tab--active" : "", tab == "expiring" ? " cr-tab--active" : "", tab == "rounds" ? " cr-tab--active" : "");

        // round banner
        if (openRound != null)
        {
            DateTime? dl = D(openRound["submission_deadline"]);
            int daysTo = dl.HasValue ? (int)(dl.Value.Date - DateTime.Today).TotalDays : 0;
            litRoundBanner.Text = string.Format(
                "<div class='cr-round'><div class='cr-round__t'>{0}</div><div class='cr-round__m'>Council sitting: <strong>{1}</strong>{2} &middot; Deadline: <strong>{3}</strong> {4} &middot; Contracts ending on or before {5}</div></div>",
                Enc(SafeStr(openRound["title"])), Enc(SafeStr(openRound["council_sitting"])),
                D(openRound["council_date"]).HasValue ? " (" + FmtD(openRound["council_date"]) + ")" : "",
                FmtD(openRound["submission_deadline"]),
                dl.HasValue ? (daysTo >= 0 ? "<span class='cr-days cr-days--" + (daysTo <= 7 ? "red" : "amber") + "'>" + daysTo + "d to go</span>" : "<span class='cr-days cr-days--red'>passed</span>") : "",
                FmtD(openRound["eligible_expiry_to"]));
        }
        else litRoundBanner.Text = "<div class='cr-alert cr-alert--warn'>No renewal round is open. Staff can still apply within six months of their contract end; open a round on the Rounds tab to set the Council sitting and deadline.</div>";

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
        DataTable k = Q(@"SELECT
                SUM(status = 'DRAFT') AS drafts, SUM(status = 'RETURNED') AS returned,
                SUM(status = 'AWAITING_SUPERVISOR') AS sup, SUM(status = 'AWAITING_HR') AS hr,
                SUM(status = 'FORWARDED') AS fwd,
                SUM(status IN ('APPROVED','NOT_APPROVED','DEFERRED')) AS decided,
                SUM(status = 'APPROVED') AS approved, SUM(status = 'NOT_APPROVED') AS notapp, SUM(status = 'DEFERRED') AS deferred,
                SUM(status = 'CONTRACT_ISSUED') AS issued, SUM(is_late = 1 AND submitted_at IS NOT NULL) AS late
              FROM hr_contract_renewals" + w, ps.ToArray());
        DataRow r = k.Rows[0];
        int noApp = SafeInt(Q("SELECT COUNT(*) AS n FROM (" + ExpiringSql + " AND c.contractEnd <= DATE_ADD(CURDATE(), INTERVAL 90 DAY)) z WHERE z.renewal_id IS NULL").Rows[0]["n"]);

        StringBuilder sb = new StringBuilder();
        Kpi(sb, "red", noApp, "Expiring &le; 3 months, no application", "ContractRenewals.aspx?tab=expiring&amp;win=90&amp;noapp=1", "Ended in the last 60 days included");
        Kpi(sb, "grey", SafeInt(r["drafts"]) + SafeInt(r["returned"]), "Drafts", "ContractRenewals.aspx?status=DRAFT", SafeInt(r["returned"]) > 0 ? SafeInt(r["returned"]) + " returned for correction" : "Not yet submitted");
        Kpi(sb, "blue", SafeInt(r["sup"]), "Awaiting supervisor", "ContractRenewals.aspx?status=AWAITING_SUPERVISOR", "");
        Kpi(sb, "navy", SafeInt(r["hr"]), "Awaiting HR", "ContractRenewals.aspx?status=AWAITING_HR", SafeInt(r["late"]) > 0 ? SafeInt(r["late"]) + " submitted late" : "");
        Kpi(sb, "accent", SafeInt(r["fwd"]), "Forwarded to Council", "ContractRenewals.aspx?status=FORWARDED", "");
        Kpi(sb, "amber", SafeInt(r["decided"]), "Decided", "", SafeInt(r["approved"]) + " approved &middot; " + SafeInt(r["notapp"]) + " not &middot; " + SafeInt(r["deferred"]) + " deferred");
        Kpi(sb, "green", SafeInt(r["issued"]), "Contracts issued", "ContractRenewals.aspx?status=CONTRACT_ISSUED", "");
        litKpis.Text = sb.ToString();
    }

    private static void Kpi(StringBuilder sb, string color, int val, string label, string href, string sub)
    {
        sb.AppendFormat("<{0} class='cr-kpi cr-kpi--{1}'{2}><div class='cr-kpi__val'>{3}</div><div class='cr-kpi__lbl'>{4}</div>{5}</{0}>",
            href == "" ? "div" : "a", color, href == "" ? "" : " href='" + href + "'", val.ToString("N0"), label,
            sub == "" ? "" : "<div class='cr-kpi__sub'>" + sub + "</div>");
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

    // ── applications tab ───────────────────────────────────────────────
    private void RenderApps(DataTable rounds)
    {
        StringBuilder f = new StringBuilder();
        f.Append("<select id='fRound' class='cr-input'>" + RoundOptions(rounds, QsRound, "All rounds") + "</select>");
        f.Append("<select id='fStatus' class='cr-input'><option value=''>All statuses</option>");
        foreach (string s in Statuses)
            f.AppendFormat("<option value='{0}'{1}>{2}</option>", s, s == QsStatus ? " selected" : "", Enc(COOPERP_NewScreens_ContractRenewalView.StatusLabel(s)));
        f.Append("</select>");
        f.Append("<select id='fCat' class='cr-input'><option value=''>All categories</option>");
        foreach (string c in new string[] { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" })
            f.AppendFormat("<option value='{0}'{1}>{2}</option>", c, c == QsCat ? " selected" : "", Enc(COOPERP_NewScreens_ContractRenewalView.CategoryLabel(c)));
        f.Append("</select>");
        f.Append("<select id='fDept' class='cr-input'>" + DeptOptions(QsDept) + "</select>");
        f.AppendFormat("<select id='fLate' class='cr-input'><option value=''>Late or on time</option><option value='1'{0}>Late only</option><option value='0'{1}>On time only</option></select>",
            QsLate == "1" ? " selected" : "", QsLate == "0" ? " selected" : "");
        f.AppendFormat("<input type='text' id='fQ' class='cr-input cr-input--search' placeholder='Search name, ref, employee no' value='{0}' />", Enc(QsSearch));
        f.Append("<button type='button' class='cr-btn cr-btn--primary' onclick='applyFilters()'>Filter</button>");
        f.Append("<a class='cr-btn' href='ContractRenewals.aspx'>Reset</a>");
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
            t.AppendFormat("<tr data-id='{0}' data-status='{1}'>", id, st);
            t.AppendFormat("<td class='cr-cb'>{0}</td>", selectable ? "<input type='checkbox' class='cr-row-cb' value='" + id + "' data-status='" + st + "' onchange='selChanged()' />" : "");
            t.AppendFormat("<td><a class='cr-link' href='ContractRenewalView.aspx?id={0}'><span class='cr-code'>{1}</span></a></td>", id, Enc(SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : "#" + id));
            t.AppendFormat("<td><a class='cr-link cr-strong' href='ContractRenewalView.aspx?id={0}'>{1}</a><div class='cr-muted'>{2}{3}</div></td>", id,
                Enc(SafeStr(r["emp_name"])), Enc(SafeStr(r["emp_code"])), SafeStr(r["staff_category"]) != "" ? " &middot; " + Enc(COOPERP_NewScreens_ContractRenewalView.CategoryLabel(SafeStr(r["staff_category"]))) : "");
            t.AppendFormat("<td>{0}<div class='cr-muted'>{1}</div></td>", Enc(SafeStr(r["cur_job"])), Enc(SafeStr(r["cur_department"])));
            t.AppendFormat("<td class='cr-nowrap'>{0}<div>{1}</div></td>", FmtD(r["cur_end"]),
                end.HasValue && st != "CONTRACT_ISSUED" && st != "NOT_APPROVED" && st != "WITHDRAWN" ? COOPERP_NewScreens_ContractRenewalView.DaysLeftHtml(SafeInt(r["days_left"])) : "");
            t.AppendFormat("<td>{0}{1}</td>", COOPERP_NewScreens_ContractRenewalView.StatusBadge(st),
                st == "FORWARDED" && SafeStr(r["council_sitting"]) != "" ? "<div class='cr-muted'>" + Enc(SafeStr(r["council_sitting"])) + "</div>" : "");
            t.AppendFormat("<td>{0}</td>", COOPERP_NewScreens_ContractRenewalView.RecBadge(SafeStr(r["sup_recommendation"])));
            t.AppendFormat("<td class='cr-nowrap cr-muted'>{0}</td>", FmtD(r["submitted_at"]));
            t.AppendFormat("<td>{0}</td>", SafeInt(r["is_late"]) == 1 ? "<span class='cr-badge cr-badge--red'>Late</span>" : "");
            t.AppendFormat("<td class='cr-nowrap'><a class='cr-icon-btn' title='Open' href='ContractRenewalView.aspx?id={0}'>{1}</a><a class='cr-icon-btn' title='Print pack' target='_blank' href='ContractRenewalPrint.aspx?id={0}'>{2}</a></td>",
                id, IconEye(), IconPrint());
            t.Append("</tr>");
        }
        if (dt.Rows.Count == 0) t.Append("<tr><td colspan='10' class='cr-empty'>No applications match these filters.</td></tr>");
        litApps.Text = t.ToString();
        litAppsCount.Text = dt.Rows.Count.ToString("N0") + " application(s)" + (dt.Rows.Count >= 1000 ? " (first 1,000)" : "");

        // bulk modal defaults
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
            winLabel = round != null ? "ending on or before " + to.ToString("d MMM yyyy") + " (" + SafeStr(round["title"]) + ")" : "next 90 days (no open round)";
        }
        else
        {
            to = DateTime.Today.AddDays(int.Parse(win));
            winLabel = "ending within " + win + " days";
        }

        StringBuilder f = new StringBuilder();
        f.Append("<select id='xWin' class='cr-input'>");
        foreach (string[] o in new string[][] { new[] { "30", "Next 30 days" }, new[] { "60", "Next 60 days" }, new[] { "90", "Next 90 days" }, new[] { "180", "Next 180 days" }, new[] { "round", "Round window" } })
            f.AppendFormat("<option value='{0}'{1}>{2}</option>", o[0], o[0] == win ? " selected" : "", o[1]);
        f.Append("</select>");
        f.Append("<select id='xRound' class='cr-input' title='Round (for the round window and the reminder deadline)'>" + RoundOptions(rounds, QsRound > 0 ? QsRound : (openRound != null ? SafeInt(openRound["round_id"]) : 0), "Open round") + "</select>");
        f.Append("<select id='xDept' class='cr-input'>" + DeptOptions(QsDept) + "</select>");
        f.AppendFormat("<label class='cr-inline'><input type='checkbox' id='xNoApp'{0} /> Without an application only</label>", QsNoApp ? " checked" : "");
        f.AppendFormat("<input type='text' id='xQ' class='cr-input cr-input--search' placeholder='Search name or employee no' value='{0}' />", Enc(QsSearch));
        f.Append("<button type='button' class='cr-btn cr-btn--primary' onclick='applyExpFilters()'>Show</button>");
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
            t.AppendFormat("<tr data-emp='{0}'>", emp);
            t.AppendFormat("<td class='cr-cb'>{0}</td>", hasEmail
                ? "<input type='checkbox' class='cr-exp-cb' value='" + emp + "' data-name='" + Enc(SafeStr(r["emp_name"])) + "' onchange='expSelChanged()' />" : "");
            t.AppendFormat("<td><span class='cr-strong'>{0}</span><div class='cr-muted'>{1} &middot; {2}</div></td>", Enc(SafeStr(r["emp_name"])),
                Enc(SafeStr(r["EMP_CODE"])), Enc(COOPERP_NewScreens_ContractRenewalView.CategoryLabel(StaffCategory(SafeStr(r["EmpType"])))));
            t.AppendFormat("<td>{0}<div class='cr-muted'>{1}</div></td>", Enc(SafeStr(r["jobname"])), Enc(SafeStr(r["dept_name"])));
            t.AppendFormat("<td class='cr-nowrap'>#{0} &middot; {1}<div class='cr-muted'>{2} &middot; {3}</div></td>", SafeInt(r["cid"]), Enc(SafeStr(r["contractStatus"])),
                Enc(SafeStr(r["contract_type"])), FmtD(r["contractStart"]));
            t.AppendFormat("<td class='cr-nowrap'>{0}<div>{1}</div></td>", FmtD(r["contractEnd"]), COOPERP_NewScreens_ContractRenewalView.DaysLeftHtml(SafeInt(r["days_left"])));
            t.AppendFormat("<td>{0}</td>", rid > 0
                ? "<a class='cr-link' href='ContractRenewalView.aspx?id=" + rid + "'><span class='cr-code'>" + Enc(SafeStr(r["ref_no"])) + "</span></a> " + COOPERP_NewScreens_ContractRenewalView.StatusBadge(ast)
                : "<span class='cr-badge cr-badge--red'>No application</span>");
            t.AppendFormat("<td class='cr-muted'>{0}</td>", hasEmail ? Enc(email) : "<span class='cr-badge cr-badge--grey'>No email</span>");
            t.AppendFormat("<td class='cr-nowrap cr-muted' id='rem_{0}'>{1}</td>", emp,
                SafeInt(r["n_reminders"]) > 0 ? FmtDT(r["last_reminder"]) + (SafeInt(r["n_reminders"]) > 1 ? " (" + SafeInt(r["n_reminders"]) + "x)" : "") : "&mdash;");
            t.Append("</tr>");
        }
        if (dt.Rows.Count == 0) t.Append("<tr><td colspan='8' class='cr-empty'>No contracts in this window.</td></tr>");
        litExpiring.Text = t.ToString();
        litExpCount.Text = string.Format("{0} contract(s) {1}, or ended in the last 60 days &middot; <strong>{2}</strong> without an application",
            dt.Rows.Count, Enc(winLabel), noApp);
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
            t.AppendFormat("<td><span class='cr-strong'>{0}</span>{1}</td>", Enc(SafeStr(r["title"])),
                SafeStr(r["notes"]) != "" ? "<div class='cr-muted'>" + Enc(SafeStr(r["notes"])) + "</div>" : "");
            t.AppendFormat("<td>{0}<div class='cr-muted'>{1}</div></td>", Enc(SafeStr(r["council_sitting"])), D(r["council_date"]).HasValue ? FmtD(r["council_date"]) : "");
            t.AppendFormat("<td class='cr-nowrap'>{0}</td><td class='cr-nowrap'>{1}</td>", FmtD(r["submission_deadline"]), FmtD(r["eligible_expiry_to"]));
            t.AppendFormat("<td>{0}</td>", open ? "<span class='cr-badge cr-badge--green'>Open</span>" : "<span class='cr-badge cr-badge--grey'>Closed</span>");
            t.AppendFormat("<td class='cr-num'><a class='cr-link' href='ContractRenewals.aspx?round={0}'>{1}</a></td><td class='cr-num'>{2}</td>", id, SafeInt(r["n_apps"]), SafeInt(r["n_fwd"]));
            t.AppendFormat("<td class='cr-nowrap'><button type='button' class='cr-btn cr-btn--sm' onclick='editRound(this)'>Edit</button>{0}" +
                           "<a class='cr-btn cr-btn--sm' target='_blank' href='ContractRenewalPrint.aspx?schedule=1&amp;round={1}'>Schedule</a></td>",
                open ? "<button type='button' class='cr-btn cr-btn--sm cr-btn--danger-outline' onclick='closeRound(" + id + ")'>Close</button>" : "", id);
            t.Append("</tr>");
        }
        if (rounds.Rows.Count == 0) t.Append("<tr><td colspan='8' class='cr-empty'>No rounds yet.</td></tr>");
        litRounds.Text = t.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string IconEye()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z'/><circle cx='12' cy='12' r='3'/></svg>";
    }
    private static string IconPrint()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='6 9 6 2 18 2 18 9'/><path d='M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2'/><rect x='6' y='14' width='12' height='8'/></svg>";
    }

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
