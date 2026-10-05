using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Contract Renewal Requests - one application, HR side (Muteesa I Royal University).
///
/// Employees and supervisors act in the staff portal (DRAFT, AWAITING_SUPERVISOR, sup_*).
/// HR acts here from AWAITING_HR onward:
///   AWAITING_SUPERVISOR -> AWAITING_HR   skip_supervisor (reason required)
///   AWAITING_HR         -> FORWARDED     forward / bulk_forward (checklist + council sitting)
///   AWAITING_HR|FORWARDED -> RETURNED    return (reason required, employee emailed)
///   FORWARDED           -> APPROVED | NOT_APPROVED | DEFERRED   decision / bulk_decision (emailed)
///   APPROVED            -> CONTRACT_ISSUED   issue_contract (one transaction: new VALID contract,
///                                            old + any other VALID contract of the employee EXPIRED)
/// Every status change is guarded by "WHERE status = &lt;expected&gt;" so two HR officers can never
/// apply conflicting actions, and writes one hr_renewal_audit row (actor 'eadmin:&lt;user&gt;').
///
/// The console (ContractRenewals.aspx) posts its bulk actions to this page so the state
/// machine lives in exactly one place.
///
/// Files live outside the web roots in ..\Data_Private\ContractRenewals\&lt;renewal_id&gt;\ and are
/// only ever streamed through ?ajax=doc (login + HR access).
/// </summary>
public partial class COOPERP_NewScreens_ContractRenewalView : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsId { get { int v; return int.TryParse(Request.QueryString["id"] ?? "0", out v) && v > 0 ? v : 0; } }

    public static readonly string[] CheckKeys = { "app_letter", "motivation", "form_complete", "appraisal", "sup_rec", "contract_ok" };
    public static readonly string[] CheckLabels = {
        "Application letter attached",
        "Letter of motivation attached",
        "Achievement evaluation form complete",
        "Online performance appraisal submitted / completed",
        "Supervisor recommendation present",
        "Current contract details verified correct"
    };

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        // Content Page_Load runs BEFORE SidebarMaster's login redirect, so every ?ajax= path gates itself.
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            if (ajax == "doc") { ServeDocument(); return; }
            HandleAjax(ajax);
            return;
        }

        if (!IsPostBack)
        {
            if (!IsCallerAuthenticated()) return;   // SidebarMaster redirects to login
            if (!HasHrAccess())
            {
                pnlMain.Visible = false;
                litError.Text = "<div class='cr-alert cr-alert--error'>Access denied. Contract renewal applications are available to HR and administrators only.</div>";
                return;
            }
            if (QsId <= 0) { Response.Redirect("ContractRenewals.aspx", false); Context.ApplicationInstance.CompleteRequest(); return; }
            try { RenderPage(QsId); }
            catch (Exception ex)
            {
                pnlMain.Visible = false;
                litError.Text = "<div class='cr-alert cr-alert--error'>Error loading application: " + Enc(ex.Message) + "</div>";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH
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

    /// <summary>
    /// Same rule as the appraisal screens (HasHrAppraisalAccess): RBAC admin wildcard, an active
    /// sys role 'admin' or 'hr_manager', or legacy my_aspnet role Administrator / System Admin /
    /// Human Resource / Human Resource Manager.
    /// </summary>
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

    private int? _actorEmpId;
    private int ActorEmpId()
    {
        if (_actorEmpId.HasValue) return _actorEmpId.Value;
        int v = 0;
        try
        {
            string u = CurrentUsername();
            if (!string.IsNullOrEmpty(u))
            {
                DataTable dt = Q("SELECT empID FROM hrm_employee WHERE usernames = @u LIMIT 1", new MySqlParameter("@u", u));
                if (dt.Rows.Count > 0) v = SafeInt(dt.Rows[0]["empID"]);
            }
        }
        catch { }
        _actorEmpId = v;
        return v;
    }

    private string ActorTag { get { return "eadmin:" + CurrentUsername(); } }

    /// <summary>One hr_renewal_audit row. With a transaction it is part of it (and may throw); without, it never throws.</summary>
    private void WriteAudit(int rid, string action, string oldStatus, string newStatus, string payloadJson,
                            MySqlConnection conn = null, MySqlTransaction tx = null)
    {
        const string sql = @"INSERT INTO hr_renewal_audit (renewal_id, actor_empid, actor_username, action, old_status, new_status, payload_json, created_at)
                             VALUES (@rid, @emp, @usr, @act, @old, @new, @pl, NOW())";
        int emp = ActorEmpId();
        Func<MySqlParameter[]> ps = () => new MySqlParameter[] {
            new MySqlParameter("@rid", rid),
            new MySqlParameter("@emp", emp > 0 ? (object)emp : DBNull.Value),
            new MySqlParameter("@usr", ActorTag),
            new MySqlParameter("@act", action),
            new MySqlParameter("@old", (object)oldStatus ?? DBNull.Value),
            new MySqlParameter("@new", (object)newStatus ?? DBNull.Value),
            new MySqlParameter("@pl", (object)payloadJson ?? DBNull.Value) };
        if (conn != null)
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddRange(ps());
                cmd.ExecuteNonQuery();
            }
            return;
        }
        try { X(sql, ps()); } catch { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DOCUMENT DOWNLOAD (private folder, never a web path)
    // ═══════════════════════════════════════════════════════════════════
    public static string DocRoot(HttpServerUtility server)
    {
        return Path.Combine(Path.Combine(Directory.GetParent(server.MapPath("~/").TrimEnd('\\')).FullName, "Data_Private"), "ContractRenewals");
    }

    private void ServeDocument()
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

        int docId;
        if (!int.TryParse(Request.QueryString["doc"], out docId) || docId <= 0) { DocNotFound(); return; }
        DataTable dt = Q("SELECT renewal_id, stored_name, original_name, content_type FROM hr_renewal_documents WHERE doc_id = @id",
            new MySqlParameter("@id", docId));
        if (dt.Rows.Count == 0) { DocNotFound(); return; }

        int rid = SafeInt(dt.Rows[0]["renewal_id"]);
        string stored = SafeStr(dt.Rows[0]["stored_name"]).Trim();
        string original = SafeStr(dt.Rows[0]["original_name"]).Trim();
        string ctype = SafeStr(dt.Rows[0]["content_type"]).Trim().ToLowerInvariant();

        if (stored.Length == 0 || stored != Path.GetFileName(stored)
            || stored.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || stored.Contains(".."))
        { DocNotFound(); return; }

        string root = Path.GetFullPath(DocRoot(Server));
        string full = Path.GetFullPath(Path.Combine(Path.Combine(root, rid.ToString()), stored));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) { DocNotFound(); return; }

        bool inline = ctype == "application/pdf" || ctype == "image/jpeg" || ctype == "image/png" || ctype == "image/gif";
        string safeName = System.Text.RegularExpressions.Regex.Replace(string.IsNullOrEmpty(original) ? stored : original, @"[^\w\.\- ]", "_");

        Response.ContentType = inline ? ctype : "application/octet-stream";
        Response.AddHeader("X-Content-Type-Options", "nosniff");
        Response.AddHeader("Cache-Control", "private, no-store");
        Response.AddHeader("Content-Disposition", (inline ? "inline" : "attachment") + "; filename=\"" + safeName + "\"");
        Response.TransmitFile(full);
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private void DocNotFound()
    {
        Response.StatusCode = 404;
        Response.ContentType = "text/plain";
        Response.Write("Document not found.");
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX ROUTER
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
                    case "save_checklist":  AjaxSaveChecklist(); break;
                    case "forward":         AjaxForward(); break;
                    case "bulk_forward":    AjaxBulkForward(); break;
                    case "return":          AjaxReturn(); break;
                    case "decision":        AjaxDecision(); break;
                    case "bulk_decision":   AjaxBulkDecision(); break;
                    case "issue_contract":  AjaxIssueContract(); break;
                    case "skip_supervisor": AjaxSkipSupervisor(); break;
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

    private void Fail(string msg) { Response.Write("{\"ok\":false,\"msg\":\"" + Js(msg) + "\"}"); }
    private void Ok(string msg) { Response.Write("{\"ok\":true,\"msg\":\"" + Js(msg) + "\"}"); }

    private DataRow LoadRenewal(int id)
    {
        DataTable dt = Q(
            @"SELECT r.*, IFNULL(e.emp_email,'') AS emp_email, IFNULL(e.emp_phone,'') AS emp_phone,
                     IFNULL(e.EmpType,'') AS emp_type, IFNULL(rev.emp_name,'') AS reviewer_live_name,
                     rd.title AS round_title, rd.council_sitting AS round_sitting, rd.council_date AS round_council_date,
                     rd.submission_deadline AS round_deadline,
                     c.contractStatus AS cur_contract_status, c.contractStart AS live_start, c.contractEnd AS live_end,
                     c.jobID AS live_job_id, c.departmentID AS live_dept_id, c.contract_type AS live_type,
                     c.payscale AS live_payscale, c.fixedamount AS live_fixed
              FROM hr_contract_renewals r
              LEFT JOIN hrm_employee e ON e.empID = r.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = r.reviewer_id
              LEFT JOIN hr_renewal_rounds rd ON rd.round_id = r.round_id
              LEFT JOIN hrm_emp_contracts c ON c.ID = r.contract_id
              WHERE r.renewal_id = @id",
            new MySqlParameter("@id", id));
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    /// <summary>What the data says about each checklist item (contract_ok is HR's judgement only).</summary>
    private Dictionary<string, bool> AutoChecks(DataRow r)
    {
        int id = SafeInt(r["renewal_id"]);
        var res = new Dictionary<string, bool>();
        int appLetters = 0, motivation = 0;
        foreach (DataRow d in Q("SELECT doc_type, COUNT(*) AS n FROM hr_renewal_documents WHERE renewal_id = @id GROUP BY doc_type",
                                new MySqlParameter("@id", id)).Rows)
        {
            string t = SafeStr(d["doc_type"]);
            if (t == "APPLICATION_LETTER") appLetters = SafeInt(d["n"]);
            if (t == "MOTIVATION_LETTER") motivation = SafeInt(d["n"]);
        }
        DataTable form = Q(
            @"SELECT COUNT(*) AS n,
                     SUM(CASE WHEN TRIM(IFNULL(kpa,'')) <> '' AND TRIM(IFNULL(achievement,'')) <> '' THEN 1 ELSE 0 END) AS ok
              FROM hr_renewal_achievements WHERE renewal_id = @id", new MySqlParameter("@id", id));
        int n = SafeInt(form.Rows[0]["n"]), okRows = SafeInt(form.Rows[0]["ok"]);
        int appr = SafeInt(Q(
            @"SELECT COUNT(*) AS n FROM appraisal_records
              WHERE employee_id = @e AND (employee_submitted_at IS NOT NULL OR status IN ('EMPLOYEE_SUBMITTED','COMPLETED','HR_REVIEWED'))",
            new MySqlParameter("@e", SafeInt(r["employee_id"]))).Rows[0]["n"]);

        res["app_letter"] = appLetters > 0;
        res["motivation"] = motivation > 0;
        res["form_complete"] = n > 0 && okRows == n;
        res["appraisal"] = appr > 0;
        res["sup_rec"] = SafeStr(r["sup_recommendation"]).Trim() != "";
        res["contract_ok"] = false;
        return res;
    }

    private static string ChecklistJson(Dictionary<string, bool> ticks, Dictionary<string, bool> auto)
    {
        StringBuilder sb = new StringBuilder("{");
        foreach (string k in CheckKeys) sb.AppendFormat("\"{0}\":{1},", k, ticks.ContainsKey(k) && ticks[k] ? 1 : 0);
        sb.Append("\"auto\":{");
        bool first = true;
        foreach (string k in CheckKeys)
        {
            if (k == "contract_ok") continue;
            if (!first) sb.Append(",");
            first = false;
            sb.AppendFormat("\"{0}\":{1}", k, auto.ContainsKey(k) && auto[k] ? 1 : 0);
        }
        sb.Append("}}");
        return sb.ToString();
    }

    private Dictionary<string, bool> TicksFromForm()
    {
        var t = new Dictionary<string, bool>();
        foreach (string k in CheckKeys) t[k] = F("chk_" + k) == "1";
        return t;
    }

    // ── save checklist (no status change) ───────────────────────────────
    private void AjaxSaveChecklist()
    {
        int id = SafeInt(F("id"));
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found"); return; }
        string st = SafeStr(r["status"]);
        if (st != "AWAITING_HR" && st != "FORWARDED") { Fail("The checklist can be edited only while the application is with HR or at Council."); return; }

        string json = ChecklistJson(TicksFromForm(), AutoChecks(r));
        string comments = F("hr_comments");
        int n = X(@"UPDATE hr_contract_renewals SET hr_checklist_json = @j, hr_comments = @c, hr_actor = @a, updated_at = NOW()
                    WHERE renewal_id = @id AND status = @st",
            new MySqlParameter("@j", json), new MySqlParameter("@c", comments == "" ? (object)DBNull.Value : comments),
            new MySqlParameter("@a", ActorTag), new MySqlParameter("@id", id), new MySqlParameter("@st", st));
        if (n == 0) { Fail("The application changed while you were working on it. Reload the page."); return; }
        WriteAudit(id, "HR_CHECKLIST_SAVED", st, st, "{\"checklist\":" + json + ",\"comments\":\"" + Js(comments) + "\"}");
        Ok("Checklist saved");
    }

    // ── forward one ─────────────────────────────────────────────────────
    private void AjaxForward()
    {
        int id = SafeInt(F("id"));
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found"); return; }
        if (SafeStr(r["status"]) != "AWAITING_HR") { Fail("Only an application with HR (AWAITING_HR) can be forwarded. Current status: " + SafeStr(r["status"])); return; }

        var ticks = TicksFromForm();
        string comments = F("hr_comments");
        bool allTicked = true;
        foreach (string k in CheckKeys) if (!ticks[k]) allTicked = false;
        if (!ticks["contract_ok"]) { Fail("Confirm that the current contract details are correct before forwarding."); return; }
        if (!allTicked && comments == "") { Fail("Some checklist items are not ticked. Explain them in the HR comments before forwarding."); return; }

        string sitting = F("council_sitting");
        if (sitting == "") sitting = SafeStr(r["round_sitting"]).Trim();
        if (sitting == "") { Fail("Enter the Council sitting the application is forwarded to."); return; }
        if (sitting.Length > 100) sitting = sitting.Substring(0, 100);

        string json = ChecklistJson(ticks, AutoChecks(r));
        int n = X(@"UPDATE hr_contract_renewals
                       SET status = 'FORWARDED', hr_checklist_json = @j, hr_comments = @c, hr_actor = @a,
                           hr_verified_at = NOW(), forwarded_at = NOW(), council_sitting = @s, updated_at = NOW()
                     WHERE renewal_id = @id AND status = 'AWAITING_HR'",
            new MySqlParameter("@j", json), new MySqlParameter("@c", comments == "" ? (object)DBNull.Value : comments),
            new MySqlParameter("@a", ActorTag), new MySqlParameter("@s", sitting), new MySqlParameter("@id", id));
        if (n == 0) { Fail("The application changed while you were working on it. Reload the page."); return; }
        WriteAudit(id, "HR_FORWARDED", "AWAITING_HR", "FORWARDED",
            "{\"council_sitting\":\"" + Js(sitting) + "\",\"checklist\":" + json + ",\"comments\":\"" + Js(comments) + "\"}");
        Ok("Forwarded to the Governance Council (" + sitting + ")");
    }

    // ── forward many (from the console) ─────────────────────────────────
    private void AjaxBulkForward()
    {
        List<int> ids = ParseIds(F("ids"));
        if (ids.Count == 0) { Fail("Select at least one application"); return; }
        if (F("contract_ok") != "1") { Fail("Confirm that you have verified the contract details of the selected applications."); return; }
        bool onlyComplete = F("only_complete") == "1";
        string sittingIn = F("council_sitting");
        string comments = F("hr_comments");

        int done = 0;
        List<string> skipped = new List<string>();
        foreach (int id in ids)
        {
            DataRow r = LoadRenewal(id);
            if (r == null) continue;
            string refNo = SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : ("#" + id);
            if (SafeStr(r["status"]) != "AWAITING_HR") { skipped.Add(refNo + ": not with HR (" + SafeStr(r["status"]) + ")"); continue; }

            var auto = AutoChecks(r);
            List<string> missing = new List<string>();
            for (int i = 0; i < CheckKeys.Length; i++)
                if (CheckKeys[i] != "contract_ok" && !auto[CheckKeys[i]]) missing.Add(CheckLabels[i].ToLower());
            if (onlyComplete && missing.Count > 0) { skipped.Add(refNo + ": missing " + string.Join(", ", missing.ToArray())); continue; }

            string sitting = sittingIn != "" ? sittingIn : SafeStr(r["round_sitting"]).Trim();
            if (sitting == "") { skipped.Add(refNo + ": no Council sitting (enter one)"); continue; }
            if (sitting.Length > 100) sitting = sitting.Substring(0, 100);

            var ticks = new Dictionary<string, bool>(auto);
            ticks["contract_ok"] = true;
            string json = ChecklistJson(ticks, auto);
            string rowComments = comments;
            if (missing.Count > 0)
                rowComments = (comments == "" ? "" : comments + " ") + "[Forwarded in bulk; missing: " + string.Join(", ", missing.ToArray()) + "]";

            int n = X(@"UPDATE hr_contract_renewals
                           SET status = 'FORWARDED', hr_checklist_json = @j,
                               hr_comments = CASE WHEN @c = '' THEN hr_comments ELSE @c END,
                               hr_actor = @a, hr_verified_at = NOW(), forwarded_at = NOW(), council_sitting = @s, updated_at = NOW()
                         WHERE renewal_id = @id AND status = 'AWAITING_HR'",
                new MySqlParameter("@j", json), new MySqlParameter("@c", rowComments),
                new MySqlParameter("@a", ActorTag), new MySqlParameter("@s", sitting), new MySqlParameter("@id", id));
            if (n == 0) { skipped.Add(refNo + ": changed by someone else"); continue; }
            WriteAudit(id, "HR_FORWARDED", "AWAITING_HR", "FORWARDED",
                "{\"bulk\":1,\"council_sitting\":\"" + Js(sitting) + "\",\"checklist\":" + json + ",\"comments\":\"" + Js(rowComments) + "\"}");
            done++;
        }
        WriteBulkResult(done, "forwarded to Council", skipped);
    }

    // ── return to employee ──────────────────────────────────────────────
    private void AjaxReturn()
    {
        int id = SafeInt(F("id"));
        string reason = F("reason");
        if (reason.Length < 5) { Fail("Give the employee a clear reason (what must be corrected)."); return; }
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found"); return; }
        string st = SafeStr(r["status"]);
        if (st != "AWAITING_HR" && st != "FORWARDED") { Fail("Only an application with HR or at Council can be returned. Current status: " + st); return; }

        int n = X(@"UPDATE hr_contract_renewals
                       SET status = 'RETURNED', returned_by = 'HR', return_reason = @rs, returned_at = NOW(), hr_actor = @a, updated_at = NOW()
                     WHERE renewal_id = @id AND status = @st",
            new MySqlParameter("@rs", reason), new MySqlParameter("@a", ActorTag),
            new MySqlParameter("@id", id), new MySqlParameter("@st", st));
        if (n == 0) { Fail("The application changed while you were working on it. Reload the page."); return; }

        string mail = SendEmployeeEmail(r,
            "[MRU HR] Your contract renewal application " + SafeStr(r["ref_no"]) + " has been returned",
            "Action required &mdash; application returned",
            "<p style='margin:0 0 12px;'>Human Resource has reviewed your contract renewal application <strong>" + Enc(SafeStr(r["ref_no"])) +
            "</strong> and returned it to you for correction:</p>" +
            "<div style='background:#fff8e1;border-left:3px solid #d97706;padding:10px 14px;margin:0 0 14px;white-space:pre-wrap;'>" + Enc(reason) + "</div>" +
            "<p style='margin:0 0 12px;'>Please sign in to the staff portal, make the corrections, consult your supervisor where needed and resubmit before the deadline" +
            DeadlineText(r) + ".</p>");
        WriteAudit(id, "HR_RETURNED", st, "RETURNED", "{\"reason\":\"" + Js(reason) + "\",\"email\":\"" + Js(mail) + "\"}");
        Ok("Returned to the employee. " + mail);
    }

    // ── Council decision ────────────────────────────────────────────────
    private class DecisionPlan { public string Decision; public int Term; public DateTime? Start; public DateTime? End; public string Error; }

    private DecisionPlan PlanDecision(DataRow r, string decision, int term, string startIn, string endIn)
    {
        DecisionPlan p = new DecisionPlan { Decision = decision };
        if (decision != "APPROVED" && decision != "NOT_APPROVED" && decision != "DEFERRED") { p.Error = "Choose the Council decision"; return p; }
        if (decision != "APPROVED") return p;
        if (term < 1 || term > 120) { p.Error = "Approved term must be between 1 and 120 months"; return p; }
        p.Term = term;
        DateTime s;
        if (startIn != "" && DateTime.TryParseExact(startIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out s)) p.Start = s;
        else
        {
            DateTime? curEnd = D(r["cur_end"]);
            if (!curEnd.HasValue) { p.Error = "The current contract has no end date; enter the new contract start date"; return p; }
            p.Start = curEnd.Value.AddDays(1);
        }
        DateTime en;
        if (endIn != "" && DateTime.TryParseExact(endIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out en)) p.End = en;
        else p.End = p.Start.Value.AddMonths(term).AddDays(-1);
        if (p.End.Value <= p.Start.Value) { p.Error = "The new contract must end after it starts"; return p; }
        return p;
    }

    private bool ApplyDecision(DataRow r, DecisionPlan p, string notes, bool bulk, out string err)
    {
        err = "";
        int id = SafeInt(r["renewal_id"]);
        int n = X(@"UPDATE hr_contract_renewals
                       SET status = @d, decision = @d, decision_term_months = @t, decision_start = @s, decision_end = @e,
                           decision_notes = @nt, decision_recorded_by = @a, decision_at = NOW(), updated_at = NOW()
                     WHERE renewal_id = @id AND status = 'FORWARDED'",
            new MySqlParameter("@d", p.Decision),
            new MySqlParameter("@t", p.Decision == "APPROVED" ? (object)p.Term : DBNull.Value),
            new MySqlParameter("@s", p.Start.HasValue ? (object)p.Start.Value : DBNull.Value),
            new MySqlParameter("@e", p.End.HasValue ? (object)p.End.Value : DBNull.Value),
            new MySqlParameter("@nt", notes == "" ? (object)DBNull.Value : notes),
            new MySqlParameter("@a", ActorTag), new MySqlParameter("@id", id));
        if (n == 0) { err = "changed by someone else"; return false; }
        string payload = "{" + (bulk ? "\"bulk\":1," : "") + "\"decision\":\"" + p.Decision + "\",\"term_months\":" + p.Term +
                         ",\"start\":\"" + (p.Start.HasValue ? p.Start.Value.ToString("yyyy-MM-dd") : "") +
                         "\",\"end\":\"" + (p.End.HasValue ? p.End.Value.ToString("yyyy-MM-dd") : "") +
                         "\",\"notes\":\"" + Js(notes) + "\"}";
        WriteAudit(id, "COUNCIL_DECISION", "FORWARDED", p.Decision, payload);
        return true;
    }

    private string DecisionEmailBody(DataRow r, DecisionPlan p, string notes)
    {
        string sitting = SafeStr(r["council_sitting"]);
        string intro = "<p style='margin:0 0 12px;'>The Governance Council" + (sitting != "" ? " (" + Enc(sitting) + " sitting)" : "") +
                       " has considered your contract renewal application <strong>" + Enc(SafeStr(r["ref_no"])) + "</strong>.</p>";
        string body;
        if (p.Decision == "APPROVED")
            body = "<p style='margin:0 0 12px;'>We are pleased to inform you that your contract has been <strong style='color:#16a34a;'>approved for renewal</strong> for " +
                   p.Term + " months, from <strong>" + p.Start.Value.ToString("d MMMM yyyy") + "</strong> to <strong>" + p.End.Value.ToString("d MMMM yyyy") +
                   "</strong>. Human Resource will issue the new contract and contact you about signing it.</p>";
        else if (p.Decision == "DEFERRED")
            body = "<p style='margin:0 0 12px;'>The Council has <strong style='color:#b45309;'>deferred</strong> a decision on your application. Human Resource will advise you on the next steps.</p>";
        else
            body = "<p style='margin:0 0 12px;'>We regret to inform you that the Council has <strong style='color:#dc3545;'>not approved</strong> the renewal of your contract. Please contact Human Resource for further guidance.</p>";
        if (notes != "") body += "<div style='background:#f5f7fa;border-left:3px solid #174DA4;padding:10px 14px;margin:0 0 14px;white-space:pre-wrap;'>" + Enc(notes) + "</div>";
        return intro + body;
    }

    private void AjaxDecision()
    {
        int id = SafeInt(F("id"));
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found"); return; }
        if (SafeStr(r["status"]) != "FORWARDED") { Fail("A Council decision can be recorded only for a forwarded application. Current status: " + SafeStr(r["status"])); return; }
        string notes = F("notes");
        DecisionPlan p = PlanDecision(r, F("decision").ToUpper(), SafeInt(F("term_months")), F("start"), F("end"));
        if (p.Error != null) { Fail(p.Error); return; }
        if (p.Decision != "APPROVED" && notes == "") { Fail("Add the Council's notes / reasons for this decision."); return; }
        string err;
        if (!ApplyDecision(r, p, notes, false, out err)) { Fail("The application " + err + ". Reload the page."); return; }

        string mail = SendEmployeeEmail(r, "[MRU HR] Council decision on your contract renewal application " + SafeStr(r["ref_no"]),
            "Governance Council decision", DecisionEmailBody(r, p, notes));
        WriteAudit(id, "EMAIL_DECISION", p.Decision, p.Decision, "{\"email\":\"" + Js(mail) + "\"}");
        Ok("Decision recorded: " + DecisionLabel(p.Decision) + ". " + mail);
    }

    private void AjaxBulkDecision()
    {
        List<int> ids = ParseIds(F("ids"));
        if (ids.Count == 0) { Fail("Select at least one application"); return; }
        string decision = F("decision").ToUpper();
        string notes = F("notes");
        bool useRequested = F("term_mode") != "fixed";
        int fixedTerm = SafeInt(F("term_months"));
        if (decision != "APPROVED" && notes == "") { Fail("Add the Council's notes / reasons for this decision."); return; }

        int done = 0;
        List<string> skipped = new List<string>();
        List<string[]> mails = new List<string[]>();   // to, subject, html, renewal_id
        foreach (int id in ids)
        {
            DataRow r = LoadRenewal(id);
            if (r == null) continue;
            string refNo = SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : ("#" + id);
            if (SafeStr(r["status"]) != "FORWARDED") { skipped.Add(refNo + ": not forwarded (" + SafeStr(r["status"]) + ")"); continue; }
            int term = 0;
            if (decision == "APPROVED")
            {
                term = useRequested ? SafeInt(r["requested_term_months"]) : fixedTerm;
                if (term <= 0 && useRequested) term = SafeInt(r["sup_term_months"]);
            }
            DecisionPlan p = PlanDecision(r, decision, term, "", "");
            if (p.Error != null) { skipped.Add(refNo + ": " + p.Error); continue; }
            string err;
            if (!ApplyDecision(r, p, notes, true, out err)) { skipped.Add(refNo + ": " + err); continue; }
            done++;
            string to = RecipientOf(r);
            mails.Add(new string[] {
                to, "[MRU HR] Council decision on your contract renewal application " + SafeStr(r["ref_no"]),
                BuildEmailHtml(SafeStr(r["emp_name"]), "Governance Council decision", DecisionEmailBody(r, p, notes)), id.ToString(), p.Decision });
        }
        SendInBackground(mails, "EMAIL_DECISION");
        WriteBulkResult(done, "recorded as " + DecisionLabel(decision) + (mails.Count > 0 ? " - outcome emails are being sent" : ""), skipped);
    }

    // ── issue the new contract ──────────────────────────────────────────
    private void AjaxIssueContract()
    {
        int id = SafeInt(F("id"));
        if (F("confirm") != "1") { Fail("Tick the confirmation before issuing the contract."); return; }
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found"); return; }
        if (SafeStr(r["status"]) != "APPROVED") { Fail("A contract can be issued only for an approved application. Current status: " + SafeStr(r["status"])); return; }
        if (SafeInt(r["new_contract_id"]) > 0) { Fail("A contract has already been issued for this application (#" + SafeInt(r["new_contract_id"]) + ")."); return; }

        DateTime start, end;
        if (!DateTime.TryParseExact(F("start"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) { Fail("Enter the contract start date"); return; }
        if (!DateTime.TryParseExact(F("end"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)) { Fail("Enter the contract end date"); return; }
        if (end <= start) { Fail("The contract must end after it starts"); return; }
        int jobId = SafeInt(F("job_id")), deptId = SafeInt(F("dept_id")), scale = SafeInt(F("payscale"));
        string ctype = F("contract_type").ToUpper();
        if (ctype != "FULL TIME" && ctype != "PART TIME") { Fail("Choose FULL TIME or PART TIME"); return; }
        double fixedAmt;
        if (!double.TryParse(F("fixedamount").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out fixedAmt) || fixedAmt < 0) fixedAmt = 0;
        if (jobId <= 0 || Q("SELECT 1 FROM hrm_jobs WHERE ID = @j", new MySqlParameter("@j", jobId)).Rows.Count == 0) { Fail("Choose a valid position (job)"); return; }
        if (deptId <= 0 || Q("SELECT 1 FROM hrm_departments WHERE ID = @d", new MySqlParameter("@d", deptId)).Rows.Count == 0) { Fail("Choose a valid department"); return; }
        if (scale < 0) scale = 0;

        int empId = SafeInt(r["employee_id"]);
        int oldId = SafeInt(r["contract_id"]);
        string refNo = SafeStr(r["ref_no"]);
        string sitting = SafeStr(r["council_sitting"]);
        string comment = "Renewal " + refNo + " approved by Council " + (sitting == "" ? "" : sitting);
        int newId;
        List<int> superseded = new List<int>();

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                try
                {
                    // Lock the application row and re-check it inside the transaction.
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT status, IFNULL(new_contract_id,0) AS nc FROM hr_contract_renewals WHERE renewal_id = @id FOR UPDATE", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (MySqlDataReader dr = cmd.ExecuteReader())
                        {
                            if (!dr.Read() || dr["status"].ToString() != "APPROVED" || Convert.ToInt32(dr["nc"]) > 0)
                                throw new InvalidOperationException("The application changed while you were working on it. Reload the page.");
                        }
                    }

                    using (MySqlCommand cmd = new MySqlCommand(
                        @"INSERT INTO hrm_emp_contracts (empID, contractStart, contractEnd, jobID, departmentID, comments, contractStatus, contract_type, payscale, fixedamount)
                          VALUES (@e, @s, @en, @j, @d, @c, 'VALID', @t, @p, @f)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@e", empId);
                        cmd.Parameters.AddWithValue("@s", start);
                        cmd.Parameters.AddWithValue("@en", end);
                        cmd.Parameters.AddWithValue("@j", jobId);
                        cmd.Parameters.AddWithValue("@d", deptId);
                        cmd.Parameters.AddWithValue("@c", comment.Trim());
                        cmd.Parameters.AddWithValue("@t", ctype);
                        cmd.Parameters.AddWithValue("@p", scale);
                        cmd.Parameters.AddWithValue("@f", fixedAmt);
                        cmd.ExecuteNonQuery();
                        newId = (int)cmd.LastInsertedId;
                    }

                    // The renewed contract, plus any other VALID contract of this employee, is superseded:
                    // payroll and the staff API read "the VALID contract", so exactly one must remain.
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT ID FROM hrm_emp_contracts WHERE empID = @e AND ID <> @n AND (ID = @o OR contractStatus = 'VALID') FOR UPDATE", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@e", empId);
                        cmd.Parameters.AddWithValue("@n", newId);
                        cmd.Parameters.AddWithValue("@o", oldId);
                        using (MySqlDataReader dr = cmd.ExecuteReader())
                            while (dr.Read()) superseded.Add(Convert.ToInt32(dr["ID"]));
                    }
                    foreach (int cid in superseded)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            @"UPDATE hrm_emp_contracts
                                 SET contractStatus = CASE WHEN contractStatus = 'VALID' THEN 'EXPIRED' ELSE contractStatus END,
                                     comments = CONCAT(IFNULL(NULLIF(TRIM(comments),''), ''), CASE WHEN IFNULL(TRIM(comments),'') = '' THEN '' ELSE ' | ' END, @c)
                               WHERE ID = @id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@c", "Superseded by renewal " + refNo + " (contract #" + newId + ")");
                            cmd.Parameters.AddWithValue("@id", cid);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    using (MySqlCommand cmd = new MySqlCommand(
                        @"UPDATE hr_contract_renewals
                             SET status = 'CONTRACT_ISSUED', new_contract_id = @n, contract_issued_at = NOW(), contract_issued_by = @a, updated_at = NOW()
                           WHERE renewal_id = @id AND status = 'APPROVED'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@n", newId);
                        cmd.Parameters.AddWithValue("@a", ActorTag);
                        cmd.Parameters.AddWithValue("@id", id);
                        if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("The application changed while you were working on it. Reload the page.");
                    }

                    StringBuilder sup = new StringBuilder();
                    foreach (int cid in superseded) { if (sup.Length > 0) sup.Append(","); sup.Append(cid); }
                    WriteAudit(id, "CONTRACT_ISSUED", "APPROVED", "CONTRACT_ISSUED",
                        "{\"new_contract_id\":" + newId + ",\"superseded\":[" + sup + "],\"start\":\"" + start.ToString("yyyy-MM-dd") +
                        "\",\"end\":\"" + end.ToString("yyyy-MM-dd") + "\",\"job_id\":" + jobId + ",\"dept_id\":" + deptId +
                        ",\"contract_type\":\"" + ctype + "\",\"payscale\":" + scale + ",\"fixedamount\":" + fixedAmt.ToString(CultureInfo.InvariantCulture) + "}",
                        conn, tx);
                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
            }
        }
        Response.Write("{\"ok\":true,\"msg\":\"New contract #" + newId + " issued (" + start.ToString("d MMM yyyy") + " - " + end.ToString("d MMM yyyy") +
                       "). " + superseded.Count + " earlier contract(s) marked superseded.\",\"contract_id\":" + newId + "}");
    }

    // ── skip supervisor ─────────────────────────────────────────────────
    private void AjaxSkipSupervisor()
    {
        int id = SafeInt(F("id"));
        string reason = F("reason");
        if (reason.Length < 5) { Fail("Give the reason the supervisor stage is being skipped."); return; }
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found"); return; }
        if (SafeStr(r["status"]) != "AWAITING_SUPERVISOR") { Fail("Only an application waiting for the supervisor can be moved to HR. Current status: " + SafeStr(r["status"])); return; }
        int n = X(@"UPDATE hr_contract_renewals SET status = 'AWAITING_HR', hr_actor = @a, updated_at = NOW()
                    WHERE renewal_id = @id AND status = 'AWAITING_SUPERVISOR'",
            new MySqlParameter("@a", ActorTag), new MySqlParameter("@id", id));
        if (n == 0) { Fail("The application changed while you were working on it. Reload the page."); return; }
        WriteAudit(id, "HR_SKIPPED_SUPERVISOR", "AWAITING_SUPERVISOR", "AWAITING_HR", "{\"reason\":\"" + Js(reason) + "\"}");
        Ok("Moved to HR verification without a supervisor recommendation");
    }

    private void WriteBulkResult(int done, string what, List<string> skipped)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("{{\"ok\":{0},\"done\":{1},\"msg\":\"{2}\",\"skipped\":[", done > 0 ? "true" : "false", done,
            Js(done + " application(s) " + what + (skipped.Count > 0 ? "; " + skipped.Count + " skipped" : "")));
        for (int i = 0; i < skipped.Count; i++) { if (i > 0) sb.Append(","); sb.Append("\"" + Js(skipped[i]) + "\""); }
        sb.Append("]}");
        Response.Write(sb.ToString());
    }

    private static List<int> ParseIds(string csv)
    {
        List<int> ids = new List<int>();
        foreach (string p in (csv ?? "").Split(','))
        {
            int v;
            if (int.TryParse(p.Trim(), out v) && v > 0 && !ids.Contains(v)) ids.Add(v);
            if (ids.Count >= 300) break;
        }
        return ids;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EMAIL
    // ═══════════════════════════════════════════════════════════════════
    private static string RecipientOf(DataRow r)
    {
        string to = (r.Table.Columns.Contains("contact_email") ? SafeStr(r["contact_email"]) : "").Trim();
        if (to == "" || !to.Contains("@")) to = SafeStr(r["emp_email"]).Trim();
        return to;
    }

    private static string DeadlineText(DataRow r)
    {
        DateTime? d = D(r["round_deadline"]);
        return d.HasValue ? " (<strong>" + d.Value.ToString("d MMMM yyyy") + "</strong>)" : "";
    }

    /// <summary>Synchronous single send. Returns a short human-readable result; never throws.</summary>
    private string SendEmployeeEmail(DataRow r, string subject, string headline, string bodyHtml)
    {
        string to = RecipientOf(r);
        if (to == "" || !to.Contains("@")) return "No email address on record - inform the employee directly.";
        try
        {
            string res = EmailSenderProtocol.SendHtmlEmail(BuildEmailHtml(SafeStr(r["emp_name"]), headline, bodyHtml), to, subject, "MRU Human Resource");
            return res != null && res.StartsWith("Email sent", StringComparison.OrdinalIgnoreCase)
                ? "Email sent to " + to + "."
                : "Email to " + to + " failed: " + (res ?? "unknown error");
        }
        catch (Exception ex) { return "Email to " + to + " failed: " + ex.Message; }
    }

    /// <summary>Bulk sends run after the response, on a background thread, each logged in hr_renewal_audit.</summary>
    private void SendInBackground(List<string[]> mails, string auditAction)
    {
        if (mails.Count == 0) return;
        string connStr = ConnStr, actor = ActorTag;
        int actorEmp = ActorEmpId();
        System.Threading.Thread t = new System.Threading.Thread(delegate ()
        {
            foreach (string[] m in mails)
            {
                string result;
                try
                {
                    if (m[0] == "" || !m[0].Contains("@")) result = "NO_EMAIL";
                    else
                    {
                        string res = EmailSenderProtocol.SendHtmlEmail(m[2], m[0], m[1], "MRU Human Resource");
                        result = res != null && res.StartsWith("Email sent", StringComparison.OrdinalIgnoreCase) ? "SENT to " + m[0] : "FAILED " + m[0] + ": " + res;
                    }
                }
                catch (Exception ex) { result = "FAILED " + m[0] + ": " + ex.Message; }
                try
                {
                    using (MySqlConnection c = new MySqlConnection(connStr))
                    {
                        c.Open();
                        using (MySqlCommand cmd = new MySqlCommand(
                            @"INSERT INTO hr_renewal_audit (renewal_id, actor_empid, actor_username, action, old_status, new_status, payload_json, created_at)
                              VALUES (@r, @e, @u, @a, @s, @s, @p, NOW())", c))
                        {
                            cmd.Parameters.AddWithValue("@r", int.Parse(m[3]));
                            cmd.Parameters.AddWithValue("@e", actorEmp > 0 ? (object)actorEmp : DBNull.Value);
                            cmd.Parameters.AddWithValue("@u", actor);
                            cmd.Parameters.AddWithValue("@a", auditAction);
                            cmd.Parameters.AddWithValue("@s", m[4]);
                            cmd.Parameters.AddWithValue("@p", "{\"email\":\"" + Js(result.Length > 250 ? result.Substring(0, 250) : result) + "\"}");
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch { }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    public static string BuildEmailHtml(string empName, string headline, string bodyHtml)
    {
        string portal = (ConfigurationManager.AppSettings["PORTAL_BASE_URL"] ?? "https://eportal.mru.ac.ug").TrimEnd('/');
        string link = portal + "/MyContracts.aspx";
        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>" +
               "<body style=\"margin:0;padding:0;background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;\">" +
               "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#f0f2f5;padding:30px 12px;\"><tr><td align=\"center\">" +
               "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border-radius:4px;overflow:hidden;\">" +
               "<tr><td style=\"background:#05275C;padding:22px 32px;text-align:center;\">" +
               "<div style=\"color:#ffffff;font-size:22px;font-weight:700;letter-spacing:2px;\">MRU</div>" +
               "<div style=\"color:#8ab4d8;font-size:11px;letter-spacing:1px;margin-top:5px;text-transform:uppercase;\">Muteesa I Royal University &middot; Human Resource</div></td></tr>" +
               "<tr><td style=\"background:#174DA4;padding:9px 32px;text-align:center;color:#ffffff;font-size:12px;font-weight:600;\">" + headline + "</td></tr>" +
               "<tr><td style=\"padding:28px 32px;font-size:14px;color:#333;line-height:1.7;\">" +
               "<p style=\"margin:0 0 4px;font-size:13px;color:#888;\">Dear</p>" +
               "<p style=\"margin:0 0 18px;font-size:18px;font-weight:700;color:#05275C;\">" + HttpUtility.HtmlEncode(empName) + ",</p>" +
               bodyHtml +
               "<p style=\"margin:18px 0 0;text-align:center;\"><a href=\"" + HttpUtility.HtmlAttributeEncode(link) + "\" style=\"display:inline-block;background:#05275C;color:#ffffff;text-decoration:none;padding:11px 26px;font-weight:700;font-size:13px;\">Open My Contracts in the staff portal</a></p>" +
               "<p style=\"margin:10px 0 0;text-align:center;font-size:11px;color:#888;\">" + HttpUtility.HtmlEncode(link) + "</p>" +
               "</td></tr>" +
               "<tr><td style=\"background:#f5f7fa;padding:14px 32px;font-size:11px;color:#888;text-align:center;border-top:1px solid #e0e5ed;\">" +
               "Human Resource Office &middot; Muteesa I Royal University. This is an automated message; please do not reply to it.</td></tr>" +
               "</table></td></tr></table></body></html>";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE RENDER
    // ═══════════════════════════════════════════════════════════════════
    private void RenderPage(int id)
    {
        DataRow r = LoadRenewal(id);
        if (r == null)
        {
            pnlMain.Visible = false;
            litError.Text = "<div class='cr-alert cr-alert--error'>Application not found. <a href='ContractRenewals.aspx'>Back to Contract Renewals</a></div>";
            return;
        }
        string st = SafeStr(r["status"]);
        hfId.Value = id.ToString();
        hfStatus.Value = st;

        // ── header ──
        litRef.Text = Enc(SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : "#" + id);
        litName.Text = Enc(SafeStr(r["emp_name"]));
        litStatus.Text = StatusBadge(st);
        litHeaderMeta.Text = string.Format("{0}{1} &middot; {2} &middot; Submitted {3}{4}",
            SafeStr(r["emp_code"]) != "" ? "<span class='cr-code'>" + Enc(SafeStr(r["emp_code"])) + "</span> " : "",
            Enc(CategoryLabel(SafeStr(r["staff_category"]))),
            Enc(SafeStr(r["round_title"]) != "" ? SafeStr(r["round_title"]) : "No round"),
            FmtDT(r["submitted_at"]),
            SafeInt(r["is_late"]) == 1 ? " <span class='cr-badge cr-badge--red'>Late</span>" : "");
        litPrintLink.Text = "<a class='cr-btn' href='ContractRenewalPrint.aspx?id=" + id + "' target='_blank'>" + IconPrint() + " Print pack</a>";

        // ── alerts ──
        StringBuilder al = new StringBuilder();
        if (st == "RETURNED")
            al.AppendFormat("<div class='cr-alert cr-alert--warn'><strong>Returned by {0}</strong> on {1}: {2}</div>",
                Enc(SafeStr(r["returned_by"]) == "HR" ? "HR" : "the supervisor"), FmtDT(r["returned_at"]), Nl(SafeStr(r["return_reason"])));
        if (st == "WITHDRAWN")
            al.AppendFormat("<div class='cr-alert cr-alert--info'><strong>Withdrawn by the employee</strong> on {0}{1}</div>",
                FmtDT(r["withdrawn_at"]), SafeStr(r["withdraw_reason"]) != "" ? ": " + Nl(SafeStr(r["withdraw_reason"])) : "");
        if (SafeInt(r["is_late"]) == 1)
            al.AppendFormat("<div class='cr-alert cr-alert--warn'>Submitted <strong>{0} day(s)</strong> before the contract ends - less than the three months required by the HR Manual.</div>",
                SafeInt(r["days_to_expiry_at_submit"]));
        DateTime? curEnd = D(r["cur_end"]);
        if (curEnd.HasValue && st != "CONTRACT_ISSUED" && st != "NOT_APPROVED" && st != "WITHDRAWN")
        {
            int left = (int)(curEnd.Value.Date - DateTime.Today).TotalDays;
            if (left < 0) al.AppendFormat("<div class='cr-alert cr-alert--error'>The current contract <strong>ended {0} day(s) ago</strong>. An expired contract cannot remain on payroll.</div>", -left);
        }
        litAlerts.Text = al.ToString();

        // ── applicant + current contract ──
        int days = curEnd.HasValue ? (int)(curEnd.Value.Date - DateTime.Today).TotalDays : 0;
        StringBuilder a = new StringBuilder("<dl class='cr-dl'>");
        Dl(a, "Employee", Enc(SafeStr(r["emp_name"])) + (SafeStr(r["emp_code"]) != "" ? " <span class='cr-code'>" + Enc(SafeStr(r["emp_code"])) + "</span>" : ""));
        Dl(a, "Category", Enc(CategoryLabel(SafeStr(r["staff_category"]))));
        Dl(a, "Position", Enc(SafeStr(r["cur_job"])));
        Dl(a, "Department", Enc(SafeStr(r["cur_department"])));
        Dl(a, "Contract", "#" + SafeInt(r["contract_id"]) + " &middot; " + Enc(SafeStr(r["cur_type"])) +
                          (SafeStr(r["cur_contract_status"]) != "" ? " &middot; " + Enc(SafeStr(r["cur_contract_status"])) : ""));
        Dl(a, "Period", FmtD(r["cur_start"]) + " &ndash; " + FmtD(r["cur_end"]) + (curEnd.HasValue ? " " + DaysLeftHtml(days) : ""));
        Dl(a, "Supervisor", Enc(SafeStr(r["reviewer_live_name"]) != "" ? SafeStr(r["reviewer_live_name"]) : "None resolved"));
        Dl(a, "Contact", Enc(SafeStr(r["contact_phone"])) + (SafeStr(r["contact_email"]) != "" ? " &middot; " + Enc(SafeStr(r["contact_email"])) : ""));
        a.Append("</dl>");
        // live contract drift warning
        DateTime? liveEnd = D(r["live_end"]);
        if (liveEnd.HasValue && curEnd.HasValue && liveEnd.Value.Date != curEnd.Value.Date)
            a.AppendFormat("<div class='cr-hint cr-hint--warn'>The contract record now ends {0} (snapshot says {1}). Check before issuing.</div>", FmtD(r["live_end"]), FmtD(r["cur_end"]));
        litApplicant.Text = a.ToString();

        // ── request ──
        StringBuilder q = new StringBuilder("<dl class='cr-dl'>");
        Dl(q, "Requested term", SafeInt(r["requested_term_months"]) > 0 ? SafeInt(r["requested_term_months"]) + " months" : "&mdash;");
        Dl(q, "Requested period", FmtD(r["requested_start"]) + " &ndash; " + FmtD(r["requested_end"]));
        Dl(q, "Contract type", Enc(SafeStr(r["requested_type"])));
        Dl(q, "Position", Enc(SafeStr(r["requested_position"])));
        Dl(q, "Signed", SafeStr(r["employee_sign_name"]) != "" ? Enc(SafeStr(r["employee_sign_name"])) + " &middot; " + FmtDT(r["employee_signed_at"]) : "&mdash;");
        q.Append("</dl>");
        q.Append("<div class='cr-para'><div class='cr-para__lbl'>Why the contract should be renewed</div><div class='cr-para__txt'>" + NlOrDash(SafeStr(r["justification"])) + "</div></div>");
        q.Append("<div class='cr-para'><div class='cr-para__lbl'>Plans for the next contract</div><div class='cr-para__txt'>" + NlOrDash(SafeStr(r["future_plans"])) + "</div></div>");
        litRequest.Text = q.ToString();

        // ── documents (needed by achievements for per-row evidence) ──
        DataTable docs = Q(@"SELECT doc_id, doc_type, item_id, original_name, content_type, size_bytes, uploaded_at
                             FROM hr_renewal_documents WHERE renewal_id = @id ORDER BY FIELD(doc_type,'APPLICATION_LETTER','MOTIVATION_LETTER','CV','SUPPORTING','EVIDENCE'), doc_id",
            new MySqlParameter("@id", id));

        // ── achievements ──
        DataTable ach = Q(@"SELECT item_id, sort_order, source, kpa, expected_standard, achievement, evidence, reviewer_comment
                            FROM hr_renewal_achievements WHERE renewal_id = @id ORDER BY sort_order, item_id", new MySqlParameter("@id", id));
        StringBuilder t = new StringBuilder();
        int i = 0;
        foreach (DataRow x in ach.Rows)
        {
            i++;
            int itemId = SafeInt(x["item_id"]);
            StringBuilder files = new StringBuilder();
            foreach (DataRow d in docs.Rows)
                if (SafeStr(d["doc_type"]) == "EVIDENCE" && SafeInt(d["item_id"]) == itemId)
                    files.Append(DocLink(d));
            t.AppendFormat("<tr><td class='cr-num'>{0}</td><td class='cr-strong'>{1}</td><td>{2}</td><td>{3}</td><td>{4}{5}</td><td>{6}</td></tr>",
                i, NlOrDash(SafeStr(x["kpa"])), NlOrDash(SafeStr(x["expected_standard"])), NlOrDash(SafeStr(x["achievement"])),
                Nl(SafeStr(x["evidence"])), files.Length > 0 ? "<div class='cr-files'>" + files + "</div>" : "",
                NlOrDash(SafeStr(x["reviewer_comment"])));
        }
        if (ach.Rows.Count == 0) t.Append("<tr><td colspan='6' class='cr-empty'>No achievement rows entered.</td></tr>");
        litAchievements.Text = t.ToString();

        // ── documents list ──
        StringBuilder dl = new StringBuilder();
        foreach (DataRow d in docs.Rows)
        {
            string type = SafeStr(d["doc_type"]);
            string forItem = "";
            if (type == "EVIDENCE")
            {
                int k = 0;
                foreach (DataRow x in ach.Rows) { k++; if (SafeInt(x["item_id"]) == SafeInt(d["item_id"])) { forItem = " (row " + k + ")"; break; } }
            }
            dl.AppendFormat("<tr><td>{0}{1}</td><td>{2}</td><td class='cr-muted'>{3}</td><td class='cr-muted'>{4}</td></tr>",
                Enc(DocTypeLabel(type)), forItem, DocLink(d), SizeLabel(SafeInt(d["size_bytes"])), FmtDT(d["uploaded_at"]));
        }
        if (docs.Rows.Count == 0) dl.Append("<tr><td colspan='4' class='cr-empty'>No documents uploaded.</td></tr>");
        litDocs.Text = dl.ToString();

        // ── supervisor ──
        StringBuilder sv = new StringBuilder();
        if (SafeStr(r["sup_recommendation"]) == "")
            sv.Append("<div class='cr-empty'>" + (st == "AWAITING_SUPERVISOR" ? "Waiting for the supervisor's recommendation." : "No supervisor recommendation recorded.") + "</div>");
        else
        {
            sv.Append("<dl class='cr-dl'>");
            Dl(sv, "Recommendation", RecBadge(SafeStr(r["sup_recommendation"])));
            Dl(sv, "Suggested term", SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]) + " months" : "&mdash;");
            Dl(sv, "Reviewer", Enc(SafeStr(r["sup_name"])) + (SafeStr(r["sup_title"]) != "" ? ", " + Enc(SafeStr(r["sup_title"])) : ""));
            Dl(sv, "Signed", FmtDT(r["sup_signed_at"]));
            sv.Append("</dl>");
            sv.Append("<div class='cr-para'><div class='cr-para__lbl'>Comments and approval</div><div class='cr-para__txt'>" + NlOrDash(SafeStr(r["sup_comments"])) + "</div></div>");
        }
        litSupervisor.Text = sv.ToString();

        // ── HR verification ──
        var auto = AutoChecks(r);
        Dictionary<string, bool> saved = ParseChecklist(SafeStr(r["hr_checklist_json"]));
        bool editable = st == "AWAITING_HR" || st == "FORWARDED";
        StringBuilder hv = new StringBuilder("<div class='cr-check'>");
        for (int k = 0; k < CheckKeys.Length; k++)
        {
            string key = CheckKeys[k];
            bool isTicked = saved != null ? (saved.ContainsKey(key) && saved[key]) : (key != "contract_ok" && auto[key]);
            string autoTag = key == "contract_ok" ? "<span class='cr-auto cr-auto--na'>HR check</span>"
                           : (auto[key] ? "<span class='cr-auto cr-auto--ok'>Found</span>" : "<span class='cr-auto cr-auto--miss'>Not found</span>");
            hv.AppendFormat("<label class='cr-check__row'><input type='checkbox' id='chk_{0}' {1}{2}/> <span>{3}</span>{4}</label>",
                key, isTicked ? "checked " : "", editable ? "" : "disabled ", Enc(CheckLabels[k]), autoTag);
        }
        hv.Append("</div>");
        hv.AppendFormat("<div class='cr-field'><label for='hrComments'>HR comments / remarks for Council</label><textarea id='hrComments' {0}>{1}</textarea></div>",
            editable ? "" : "disabled", Enc(SafeStr(r["hr_comments"])));
        if (st == "AWAITING_HR")
            hv.AppendFormat("<div class='cr-field'><label for='councilSitting'>Council sitting</label><input type='text' id='councilSitting' maxlength='100' value='{0}' placeholder='e.g. November 2026' /></div>",
                Enc(SafeStr(r["council_sitting"]) != "" ? SafeStr(r["council_sitting"]) : SafeStr(r["round_sitting"])));
        if (SafeStr(r["hr_verified_at"]) != "" || SafeStr(r["hr_actor"]) != "")
            hv.AppendFormat("<div class='cr-hint'>Last HR action by {0}{1}{2}</div>", Enc(SafeStr(r["hr_actor"])),
                SafeStr(r["hr_verified_at"]) != "" ? " &middot; verified " + FmtDT(r["hr_verified_at"]) : "",
                SafeStr(r["forwarded_at"]) != "" ? " &middot; forwarded " + FmtDT(r["forwarded_at"]) + " to " + Enc(SafeStr(r["council_sitting"])) : "");
        litHrVerify.Text = hv.ToString();

        // ── Council decision + contract ──
        StringBuilder dc = new StringBuilder();
        if (SafeStr(r["decision"]) == "")
            dc.Append("<div class='cr-empty'>" + (st == "FORWARDED" ? "Awaiting the Council's decision (" + Enc(SafeStr(r["council_sitting"])) + ")." : "No decision recorded.") + "</div>");
        else
        {
            dc.Append("<dl class='cr-dl'>");
            Dl(dc, "Decision", StatusBadge(SafeStr(r["decision"])));
            Dl(dc, "Council sitting", Enc(SafeStr(r["council_sitting"])));
            if (SafeStr(r["decision"]) == "APPROVED")
            {
                Dl(dc, "Approved term", SafeInt(r["decision_term_months"]) + " months");
                Dl(dc, "New period", FmtD(r["decision_start"]) + " &ndash; " + FmtD(r["decision_end"]));
            }
            Dl(dc, "Recorded", Enc(SafeStr(r["decision_recorded_by"])) + " &middot; " + FmtDT(r["decision_at"]));
            dc.Append("</dl>");
            if (SafeStr(r["decision_notes"]) != "")
                dc.Append("<div class='cr-para'><div class='cr-para__lbl'>Council notes</div><div class='cr-para__txt'>" + Nl(SafeStr(r["decision_notes"])) + "</div></div>");
        }
        if (SafeInt(r["new_contract_id"]) > 0)
        {
            DataTable nc = Q(@"SELECT c.ID, c.contractStart, c.contractEnd, c.contractStatus, c.contract_type, IFNULL(j.jobname,'') AS jobname, IFNULL(d.dept_name,'') AS dept_name
                               FROM hrm_emp_contracts c LEFT JOIN hrm_jobs j ON j.ID = c.jobID LEFT JOIN hrm_departments d ON d.ID = c.departmentID WHERE c.ID = @c",
                new MySqlParameter("@c", SafeInt(r["new_contract_id"])));
            dc.Append("<div class='cr-issued'><div class='cr-issued__t'>New contract issued</div>");
            if (nc.Rows.Count > 0)
            {
                DataRow c = nc.Rows[0];
                dc.AppendFormat("<div>Contract <strong>#{0}</strong> &middot; {1} &ndash; {2} &middot; {3} &middot; {4}, {5} &middot; {6}</div>",
                    SafeInt(c["ID"]), FmtD(c["contractStart"]), FmtD(c["contractEnd"]), Enc(SafeStr(c["contract_type"])),
                    Enc(SafeStr(c["jobname"])), Enc(SafeStr(c["dept_name"])), Enc(SafeStr(c["contractStatus"])));
            }
            else dc.AppendFormat("<div>Contract #{0} (record no longer found)</div>", SafeInt(r["new_contract_id"]));
            dc.AppendFormat("<div class='cr-hint'>Issued by {0} &middot; {1} &middot; <a href='HRContracts.aspx'>Open Contracts</a></div></div>",
                Enc(SafeStr(r["contract_issued_by"])), FmtDT(r["contract_issued_at"]));
        }
        litDecision.Text = dc.ToString();

        // ── actions ──
        StringBuilder ac = new StringBuilder();
        switch (st)
        {
            case "AWAITING_SUPERVISOR":
                ac.Append("<button type='button' class='cr-btn' onclick=\"openModal('skipModal')\">Skip supervisor (unavailable)</button>");
                break;
            case "AWAITING_HR":
                ac.Append("<button type='button' class='cr-btn' onclick='saveChecklist()'>Save checklist</button>");
                ac.Append("<button type='button' class='cr-btn cr-btn--danger-outline' onclick=\"openModal('returnModal')\">Return to employee</button>");
                ac.Append("<button type='button' class='cr-btn cr-btn--primary' onclick='forwardApp()'>" + IconSend() + " Forward to Council</button>");
                break;
            case "FORWARDED":
                ac.Append("<button type='button' class='cr-btn' onclick='saveChecklist()'>Save checklist</button>");
                ac.Append("<button type='button' class='cr-btn cr-btn--danger-outline' onclick=\"openModal('returnModal')\">Return to employee</button>");
                ac.Append("<button type='button' class='cr-btn cr-btn--primary' onclick=\"openModal('decisionModal')\">Record Council decision</button>");
                break;
            case "APPROVED":
                ac.Append("<button type='button' class='cr-btn cr-btn--success' onclick=\"openModal('issueModal')\">Issue new contract</button>");
                break;
        }
        litActions.Text = ac.Length > 0 ? ac.ToString() : "<span class='cr-muted'>No HR action at this stage (" + Enc(StatusLabel(st)) + ").</span>";

        // decision modal defaults
        int defTerm = SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]) : SafeInt(r["requested_term_months"]);
        if (defTerm <= 0) defTerm = 24;
        DateTime defStart = curEnd.HasValue ? curEnd.Value.AddDays(1) : DateTime.Today;
        hfDefTerm.Value = defTerm.ToString();
        hfDefStart.Value = defStart.ToString("yyyy-MM-dd");
        hfCurEnd.Value = curEnd.HasValue ? curEnd.Value.ToString("yyyy-MM-dd") : "";

        // issue modal
        if (st == "APPROVED") RenderIssueModal(r);

        // ── appraisals ──
        DataTable ap = Q(@"SELECT ar.record_id, ar.status, ar.final_percentage, ar.classification, ar.employee_submitted_at, ar.created_at,
                                  IFNULL(s.session_title,'') AS session_title
                           FROM appraisal_records ar LEFT JOIN appraisal_sessions s ON s.session_id = ar.session_id
                           WHERE ar.employee_id = @e
                           ORDER BY COALESCE(ar.employee_submitted_at, ar.created_at) DESC, ar.record_id DESC",
            new MySqlParameter("@e", SafeInt(r["employee_id"])));
        StringBuilder apb = new StringBuilder();
        foreach (DataRow x in ap.Rows)
        {
            int recId = SafeInt(x["record_id"]);
            string pct = x["final_percentage"] == DBNull.Value ? "&mdash;" : Convert.ToDecimal(x["final_percentage"]).ToString("0.0") + "%";
            apb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td class='cr-num'>{2}</td><td>{3}</td><td class='cr-muted'>{4}</td><td class='cr-actions'><a class='cr-link' href='AppraisalView.aspx?rid={5}'>View</a> &middot; <a class='cr-link' href='AppraisalPrint.aspx?rid={5}' target='_blank'>Print</a></td></tr>",
                Enc(SafeStr(x["session_title"])), Enc(SafeStr(x["status"]).Replace("_", " ")), pct, Enc(SafeStr(x["classification"])), FmtDT(x["employee_submitted_at"]), recId);
        }
        if (ap.Rows.Count == 0) apb.Append("<tr><td colspan='6' class='cr-empty'>No appraisal records for this employee.</td></tr>");
        litAppraisals.Text = apb.ToString();

        // ── audit timeline ──
        DataTable au = Q(@"SELECT action, old_status, new_status, actor_username, payload_json, created_at
                           FROM hr_renewal_audit WHERE renewal_id = @id ORDER BY audit_id DESC", new MySqlParameter("@id", id));
        StringBuilder tl = new StringBuilder();
        foreach (DataRow x in au.Rows)
        {
            string os = SafeStr(x["old_status"]), ns = SafeStr(x["new_status"]);
            string payload = SafeStr(x["payload_json"]);
            string detail = AuditDetail(payload);
            tl.AppendFormat("<li><div class='cr-tl__when'>{0}</div><div class='cr-tl__what'><strong>{1}</strong>{2} <span class='cr-muted'>by {3}</span>{4}</div></li>",
                FmtDT(x["created_at"]), Enc(SafeStr(x["action"]).Replace("_", " ").ToLower()),
                os != ns && ns != "" ? " &middot; " + Enc(StatusLabel(os)) + " &rarr; " + Enc(StatusLabel(ns)) : "",
                Enc(SafeStr(x["actor_username"])), detail != "" ? "<div class='cr-tl__d'>" + detail + "</div>" : "");
        }
        if (au.Rows.Count == 0) tl.Append("<li><div class='cr-empty'>No history yet.</div></li>");
        litAudit.Text = tl.ToString();
    }

    private void RenderIssueModal(DataRow r)
    {
        DateTime? ds = D(r["decision_start"]), de = D(r["decision_end"]);
        int jobId = SafeInt(r["live_job_id"]) > 0 ? SafeInt(r["live_job_id"]) : SafeInt(r["cur_job_id"]);
        int deptId = SafeInt(r["live_dept_id"]) > 0 ? SafeInt(r["live_dept_id"]) : SafeInt(r["cur_department_id"]);
        int scale = SafeInt(r["live_payscale"]);
        string ctype = SafeStr(r["live_type"]) != "" ? SafeStr(r["live_type"]) : SafeStr(r["cur_type"]);
        double fixedAmt = r["live_fixed"] == DBNull.Value ? 0 : Convert.ToDouble(r["live_fixed"]);

        StringBuilder sb = new StringBuilder();
        sb.Append("<div class='cr-field-row'>");
        sb.AppendFormat("<div class='cr-field'><label for='icStart'>Contract start *</label><input type='date' id='icStart' value='{0}' /></div>", ds.HasValue ? ds.Value.ToString("yyyy-MM-dd") : "");
        sb.AppendFormat("<div class='cr-field'><label for='icEnd'>Contract end *</label><input type='date' id='icEnd' value='{0}' /></div>", de.HasValue ? de.Value.ToString("yyyy-MM-dd") : "");
        sb.Append("</div><div class='cr-field-row'>");
        sb.Append("<div class='cr-field'><label for='icJob'>Position (job) *</label><select id='icJob'>");
        foreach (DataRow j in Q("SELECT ID, jobname FROM hrm_jobs ORDER BY jobname").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", SafeInt(j["ID"]), SafeInt(j["ID"]) == jobId ? " selected" : "", Enc(SafeStr(j["jobname"])));
        sb.Append("</select></div>");
        sb.Append("<div class='cr-field'><label for='icDept'>Department *</label><select id='icDept'>");
        foreach (DataRow d in Q("SELECT ID, dept_name FROM hrm_departments ORDER BY dept_name").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", SafeInt(d["ID"]), SafeInt(d["ID"]) == deptId ? " selected" : "", Enc(SafeStr(d["dept_name"])));
        sb.Append("</select></div></div><div class='cr-field-row'>");
        sb.AppendFormat("<div class='cr-field'><label for='icType'>Contract type *</label><select id='icType'><option{0}>FULL TIME</option><option{1}>PART TIME</option></select></div>",
            ctype == "PART TIME" ? "" : " selected", ctype == "PART TIME" ? " selected" : "");
        sb.Append("<div class='cr-field'><label for='icScale'>Pay scale</label><select id='icScale'><option value='0'>(none)</option>");
        foreach (DataRow p in Q("SELECT ID, scale_name, basicpay FROM hrm_payscales ORDER BY scale_name").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2} - UGX {3}</option>", SafeInt(p["ID"]), SafeInt(p["ID"]) == scale ? " selected" : "",
                Enc(SafeStr(p["scale_name"])), p["basicpay"] == DBNull.Value ? "0" : Convert.ToDouble(p["basicpay"]).ToString("N0"));
        sb.Append("</select></div></div>");
        sb.AppendFormat("<div class='cr-field'><label for='icFixed'>Fixed amount (UGX)</label><input type='number' id='icFixed' min='0' step='1' value='{0}' /><div class='cr-hint'>Copied from the current contract. Payroll uses the pay scale's basic pay, falling back to this amount.</div></div>",
            fixedAmt.ToString("0", CultureInfo.InvariantCulture));

        // other VALID contracts that will be superseded
        DataTable others = Q(@"SELECT ID, contractStart, contractEnd, contractStatus FROM hrm_emp_contracts
                               WHERE empID = @e AND (ID = @o OR contractStatus = 'VALID') ORDER BY ID",
            new MySqlParameter("@e", SafeInt(r["employee_id"])), new MySqlParameter("@o", SafeInt(r["contract_id"])));
        sb.Append("<div class='cr-alert cr-alert--info' style='margin-top:4px;'><strong>On issue:</strong> a new VALID contract is created and these contract(s) are marked superseded (VALID &rarr; EXPIRED, comment appended):<ul class='cr-ul'>");
        foreach (DataRow o in others.Rows)
            sb.AppendFormat("<li>#{0} &middot; {1} &ndash; {2} &middot; {3}{4}</li>", SafeInt(o["ID"]), FmtD(o["contractStart"]), FmtD(o["contractEnd"]),
                Enc(SafeStr(o["contractStatus"])), SafeInt(o["ID"]) == SafeInt(r["contract_id"]) ? " (the contract being renewed)" : " (another VALID contract)");
        sb.Append("</ul></div>");
        sb.Append("<label class='cr-check__row cr-check__row--confirm'><input type='checkbox' id='icConfirm' /> <span>I confirm the Governance Council approved this renewal and the new contract details above are correct.</span></label>");
        litIssueForm.Text = sb.ToString();
    }

    private static Dictionary<string, bool> ParseChecklist(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var d = new Dictionary<string, bool>();
        foreach (string k in CheckKeys)
        {
            int p = json.IndexOf("\"" + k + "\":", StringComparison.Ordinal);
            d[k] = p >= 0 && p + k.Length + 3 < json.Length && json[p + k.Length + 3] == '1';
        }
        return d;
    }

    private static string AuditDetail(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return "";
        List<string> parts = new List<string>();
        foreach (string key in new string[] { "reason", "council_sitting", "decision", "term_months", "start", "end", "notes", "comments", "new_contract_id", "email" })
        {
            string v = JsonValue(payload, key);
            if (v == null || v == "" || v == "0") continue;
            parts.Add(HttpUtility.HtmlEncode(key.Replace("_", " ")) + ": " + HttpUtility.HtmlEncode(v.Length > 300 ? v.Substring(0, 300) + "..." : v));
        }
        return string.Join(" &middot; ", parts.ToArray());
    }

    /// <summary>Minimal top-level value reader for the flat payloads this module writes.</summary>
    private static string JsonValue(string json, string key)
    {
        int p = json.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
        if (p < 0) return null;
        p += key.Length + 3;
        if (p >= json.Length) return null;
        if (json[p] == '"')
        {
            StringBuilder sb = new StringBuilder();
            for (int i = p + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[++i];
                    if (n == 'n') sb.Append('\n');
                    else if (n == 't') sb.Append(' ');
                    else if (n == 'u' && i + 4 < json.Length) { int cp; if (int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber, null, out cp)) sb.Append((char)cp); i += 4; }
                    else sb.Append(n);
                }
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }
        int e = p;
        while (e < json.Length && json[e] != ',' && json[e] != '}' && json[e] != ']') e++;
        string raw = json.Substring(p, e - p).Trim();
        return raw.StartsWith("{") || raw.StartsWith("[") ? null : raw;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LABELS / FORMATTING
    // ═══════════════════════════════════════════════════════════════════
    public static string StatusLabel(string s)
    {
        switch (s)
        {
            case "DRAFT": return "Draft";
            case "AWAITING_SUPERVISOR": return "With supervisor";
            case "AWAITING_HR": return "With HR";
            case "RETURNED": return "Returned";
            case "FORWARDED": return "Forwarded to Council";
            case "APPROVED": return "Approved";
            case "NOT_APPROVED": return "Not approved";
            case "DEFERRED": return "Deferred";
            case "CONTRACT_ISSUED": return "Contract issued";
            case "WITHDRAWN": return "Withdrawn";
            default: return s ?? "";
        }
    }

    private static string DecisionLabel(string d) { return StatusLabel(d).ToLower(); }

    public static string StatusBadge(string s)
    {
        string cls;
        switch (s)
        {
            case "DRAFT": case "WITHDRAWN": cls = "grey"; break;
            case "AWAITING_SUPERVISOR": cls = "blue"; break;
            case "AWAITING_HR": cls = "primary"; break;
            case "RETURNED": case "DEFERRED": cls = "amber"; break;
            case "FORWARDED": cls = "accent"; break;
            case "APPROVED": case "CONTRACT_ISSUED": cls = "green"; break;
            case "NOT_APPROVED": cls = "red"; break;
            default: cls = "grey"; break;
        }
        return "<span class='cr-badge cr-badge--" + cls + "'>" + HttpUtility.HtmlEncode(StatusLabel(s)) + "</span>";
    }

    public static string RecBadge(string r)
    {
        switch (r)
        {
            case "RECOMMEND": return "<span class='cr-badge cr-badge--green'>Recommended</span>";
            case "RECOMMEND_WITH_CONDITIONS": return "<span class='cr-badge cr-badge--amber'>With conditions</span>";
            case "NOT_RECOMMENDED": return "<span class='cr-badge cr-badge--red'>Not recommended</span>";
            case "": case null: return "<span class='cr-muted'>&mdash;</span>";
            default: return HttpUtility.HtmlEncode(r);
        }
    }

    public static string CategoryLabel(string c)
    {
        switch ((c ?? "").ToUpper())
        {
            case "ACADEMIC": return "Academic staff";
            case "ADMINISTRATIVE": return "Administrative staff";
            case "SUPPORT": return "Support staff";
            default: return c ?? "";
        }
    }

    public static string DocTypeLabel(string t)
    {
        switch (t)
        {
            case "APPLICATION_LETTER": return "Application letter";
            case "MOTIVATION_LETTER": return "Letter of motivation";
            case "CV": return "Curriculum vitae";
            case "SUPPORTING": return "Supporting document";
            case "EVIDENCE": return "Evidence";
            default: return t ?? "";
        }
    }

    private static string DocLink(DataRow d)
    {
        return "<a class='cr-file' href='ContractRenewalView.aspx?ajax=doc&amp;doc=" + SafeInt(d["doc_id"]) + "' target='_blank'>" +
               "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/></svg> " +
               HttpUtility.HtmlEncode(SafeStr(d["original_name"])) + "</a>";
    }

    private static string SizeLabel(int b)
    {
        if (b >= 1048576) return (b / 1048576.0).ToString("0.0") + " MB";
        if (b >= 1024) return (b / 1024.0).ToString("0") + " KB";
        return b + " B";
    }

    public static string DaysLeftHtml(int days)
    {
        string cls = days <= 30 ? "red" : (days <= 90 ? "amber" : "ok");
        string txt = days < 0 ? "ended " + (-days) + "d ago" : (days == 0 ? "ends today" : days + "d left");
        return "<span class='cr-days cr-days--" + cls + "'>" + txt + "</span>";
    }

    private static void Dl(StringBuilder sb, string label, string html)
    {
        sb.AppendFormat("<dt>{0}</dt><dd>{1}</dd>", HttpUtility.HtmlEncode(label), string.IsNullOrEmpty(html) ? "&mdash;" : html);
    }

    private static string Nl(string s) { return HttpUtility.HtmlEncode(s ?? "").Replace("\r\n", "\n").Replace("\n", "<br/>"); }
    private static string NlOrDash(string s) { return string.IsNullOrEmpty((s ?? "").Trim()) ? "<span class='cr-muted'>&mdash;</span>" : Nl(s); }

    private static string IconPrint()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='6 9 6 2 18 2 18 9'/><path d='M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2'/><rect x='6' y='14' width='12' height='8'/></svg>";
    }

    private static string IconSend()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><line x1='22' y1='2' x2='11' y2='13'/><polygon points='22 2 15 22 11 13 2 9 22 2'/></svg>";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    public static string Js(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        StringBuilder sb = new StringBuilder(val.Length + 8);
        foreach (char c in val)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': break;
                case '\t': sb.Append("\\t"); break;
                case '<': sb.Append("\\u003c"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c); else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Enc(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

    public static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        // TINYINT(1) columns (is_late, is_active ...) arrive as bool with this connection string.
        if (val is bool) return (bool)val ? 1 : 0;
        int result;
        if (int.TryParse(val.ToString(), out result)) return result;
        decimal dec;
        return decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out dec) ? (int)dec : 0;
    }

    public static string SafeStr(object val) { return (val == null || val == DBNull.Value) ? "" : val.ToString(); }

    public static DateTime? D(object v)
    {
        if (v == null || v == DBNull.Value) return null;
        if (v is DateTime) return (DateTime)v;
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) ? (DateTime?)d : null;
    }

    public static string FmtD(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy") : "&mdash;"; }
    public static string FmtDT(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy HH:mm") : "&mdash;"; }

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
