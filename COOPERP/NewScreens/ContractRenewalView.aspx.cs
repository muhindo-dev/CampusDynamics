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
        "Online performance appraisal submitted",
        "Supervisor recommendation present",
        "Current contract details verified"
    };
    /// <summary>Short names used when listing what an application is missing.</summary>
    private static readonly string[] MissingLabels = {
        "application letter", "letter of motivation", "complete evaluation form", "performance appraisal", "supervisor recommendation", "contract check" };

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        // Content Page_Load runs BEFORE SidebarMaster's login redirect, so every path gates itself (HrAccess).
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            if (ajax == "doc")
            {
                if (!HrAccess.RequireHr(false)) return;
                ServeDocument();
                return;
            }
            HandleAjax(ajax);
            return;
        }

        if (!HrAccess.RequireHr(false)) return;
        if (!IsPostBack)
        {
            if (QsId <= 0) { Response.Redirect("ContractRenewals.aspx", false); Context.ApplicationInstance.CompleteRequest(); return; }
            try { RenderPage(QsId); }
            catch (Exception)
            {
                pnlMain.Visible = false;
                litError.Text = "<div class='hr-notice hr-notice--bad'>The application could not be loaded. Reload the page, or contact MIS if this continues.</div>";
            }
        }
    }

    private static string CurrentUsername() { return HrAccess.Username(); }

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
                    case "save_checklist":  AjaxSaveChecklist(); break;
                    case "forward":         AjaxForward(); break;
                    case "bulk_forward":    AjaxBulkForward(); break;
                    case "return":          AjaxReturn(); break;
                    case "decision":        AjaxDecision(); break;
                    case "bulk_decision":   AjaxBulkDecision(); break;
                    case "issue_contract":  AjaxIssueContract(); break;
                    case "skip_supervisor": AjaxSkipSupervisor(); break;
                    default: Response.Write("{\"ok\":false,\"msg\":\"Unknown action.\"}"); break;
                }
            }
        }
        catch (System.Threading.ThreadAbortException) { }
        catch (InvalidOperationException ex)
        {
            // Our own concurrency guard (issue_contract) is written for users; anything else stays generic.
            Response.Write("{\"ok\":false,\"msg\":\"" + Js(ex.Message.StartsWith("The application changed") ? ex.Message
                : "The action could not be completed. Reload the page and try again.") + "\"}");
        }
        catch (Exception)
        {
            Response.Write("{\"ok\":false,\"msg\":\"The action could not be completed. Reload the page and try again.\"}");
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
        if (r == null) { Fail("Application not found."); return; }
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
        Ok("Checklist saved.");
    }

    // ── forward one ─────────────────────────────────────────────────────
    private void AjaxForward()
    {
        int id = SafeInt(F("id"));
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found."); return; }
        if (SafeStr(r["status"]) != "AWAITING_HR") { Fail("Only an application with HR can be forwarded."); return; }

        var ticks = TicksFromForm();
        string comments = F("hr_comments");
        bool allTicked = true;
        foreach (string k in CheckKeys) if (!ticks[k]) allTicked = false;
        if (!ticks["contract_ok"]) { Fail("Confirm that the current contract details are correct before forwarding."); return; }
        if (!allTicked && comments == "") { Fail("Some checklist items are not ticked. Explain them in the HR comments before forwarding."); return; }

        string sitting = F("council_sitting");
        if (sitting == "") sitting = SafeStr(r["round_sitting"]).Trim();
        if (sitting == "") { Fail("Enter the Council sitting."); return; }
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
        Ok("Forwarded to the Governance Council (" + sitting + ").");
    }

    // ── forward many (from the console) ─────────────────────────────────
    private void AjaxBulkForward()
    {
        List<int> ids = ParseIds(F("ids"));
        if (ids.Count == 0) { Fail("Select at least one application."); return; }
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
            if (SafeStr(r["status"]) != "AWAITING_HR") { skipped.Add(refNo + ": not with HR (status " + StatusLabel(SafeStr(r["status"])) + ")"); continue; }

            var auto = AutoChecks(r);
            List<string> missing = new List<string>();
            for (int i = 0; i < CheckKeys.Length; i++)
                if (CheckKeys[i] != "contract_ok" && !auto[CheckKeys[i]]) missing.Add(MissingLabels[i]);
            if (onlyComplete && missing.Count > 0) { skipped.Add(refNo + ": missing " + string.Join(", ", missing.ToArray())); continue; }

            string sitting = sittingIn != "" ? sittingIn : SafeStr(r["round_sitting"]).Trim();
            if (sitting == "") { skipped.Add(refNo + ": no Council sitting given"); continue; }
            if (sitting.Length > 100) sitting = sitting.Substring(0, 100);

            var ticks = new Dictionary<string, bool>(auto);
            ticks["contract_ok"] = true;
            string json = ChecklistJson(ticks, auto);
            string rowComments = comments;
            if (missing.Count > 0)
                rowComments = (comments == "" ? "" : comments + " ") + "Forwarded in bulk. Missing: " + string.Join(", ", missing.ToArray()) + ".";

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
        if (reason.Length < 5) { Fail("Enter what the employee must correct."); return; }
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found."); return; }
        string st = SafeStr(r["status"]);
        if (st != "AWAITING_HR" && st != "FORWARDED") { Fail("Only an application with HR or at Council can be returned."); return; }

        int n = X(@"UPDATE hr_contract_renewals
                       SET status = 'RETURNED', returned_by = 'HR', return_reason = @rs, returned_at = NOW(), hr_actor = @a, updated_at = NOW()
                     WHERE renewal_id = @id AND status = @st",
            new MySqlParameter("@rs", reason), new MySqlParameter("@a", ActorTag),
            new MySqlParameter("@id", id), new MySqlParameter("@st", st));
        if (n == 0) { Fail("The application changed while you were working on it. Reload the page."); return; }

        string mailNote;
        string mail = SendEmployeeEmail(r,
            "Contract renewal application " + SafeStr(r["ref_no"]) + " returned for correction",
            EmailP("Human Resource has reviewed your contract renewal application " + Enc(SafeStr(r["ref_no"])) +
                   " and returned it to you for correction. Please make the corrections below in the staff portal and resubmit it" + DeadlineText(r) + ".") +
            EmailQuote(reason),
            out mailNote);
        WriteAudit(id, "HR_RETURNED", st, "RETURNED", "{\"reason\":\"" + Js(reason) + "\",\"email\":\"" + Js(mailNote) + "\"}");
        Ok("Returned to the employee. " + mail);
    }

    // ── Council decision ────────────────────────────────────────────────
    private class DecisionPlan { public string Decision; public int Term; public DateTime? Start; public DateTime? End; public string Error; }

    private DecisionPlan PlanDecision(DataRow r, string decision, int term, string startIn, string endIn)
    {
        DecisionPlan p = new DecisionPlan { Decision = decision };
        if (decision != "APPROVED" && decision != "NOT_APPROVED" && decision != "DEFERRED") { p.Error = "Choose the Council decision."; return p; }
        if (decision != "APPROVED") return p;
        if (term < 1 || term > 120) { p.Error = "The approved term must be between 1 and 120 months."; return p; }
        p.Term = term;
        DateTime s;
        if (startIn != "" && DateTime.TryParseExact(startIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out s)) p.Start = s;
        else
        {
            DateTime? curEnd = D(r["cur_end"]);
            if (!curEnd.HasValue) { p.Error = "The current contract has no end date. Enter the new contract start date."; return p; }
            p.Start = curEnd.Value.AddDays(1);
        }
        DateTime en;
        if (endIn != "" && DateTime.TryParseExact(endIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out en)) p.End = en;
        else p.End = p.Start.Value.AddMonths(term).AddDays(-1);
        if (p.End.Value <= p.Start.Value) { p.Error = "The new contract must end after it starts."; return p; }
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
        string sitting = SafeStr(r["council_sitting"]).Trim();
        string text = "The Governance Council" + (sitting != "" ? ", at its " + Enc(sitting) + " sitting," : "") +
                      " has considered your contract renewal application " + Enc(SafeStr(r["ref_no"])) + ". ";
        if (p.Decision == "APPROVED")
            text += "Your contract has been approved for renewal for " + p.Term + " months, from " +
                    p.Start.Value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + " to " + p.End.Value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) +
                    ". Human Resource will contact you about signing the new contract.";
        else if (p.Decision == "DEFERRED")
            text += "The Council deferred its decision. Human Resource will advise you on the next steps.";
        else
            text += "The Council did not approve the renewal of your contract. Please contact Human Resource for guidance.";
        return EmailP(text) + (notes != "" ? EmailP("Council notes:") + EmailQuote(notes) : "");
    }

    private void AjaxDecision()
    {
        int id = SafeInt(F("id"));
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found."); return; }
        if (SafeStr(r["status"]) != "FORWARDED") { Fail("A Council decision can be recorded only for an application at Council."); return; }
        string notes = F("notes");
        DecisionPlan p = PlanDecision(r, F("decision").ToUpper(), SafeInt(F("term_months")), F("start"), F("end"));
        if (p.Error != null) { Fail(p.Error); return; }
        if (p.Decision != "APPROVED" && notes == "") { Fail("Add the Council notes for this decision."); return; }
        string err;
        if (!ApplyDecision(r, p, notes, false, out err)) { Fail("The application was changed by someone else. Reload the page."); return; }

        string mailNote;
        string mail = SendEmployeeEmail(r, "Council decision on contract renewal application " + SafeStr(r["ref_no"]),
            DecisionEmailBody(r, p, notes), out mailNote);
        WriteAudit(id, "EMAIL_DECISION", p.Decision, p.Decision, "{\"email\":\"" + Js(mailNote) + "\"}");
        Ok("Decision recorded: " + DecisionLabel(p.Decision) + ". " + mail);
    }

    private void AjaxBulkDecision()
    {
        List<int> ids = ParseIds(F("ids"));
        if (ids.Count == 0) { Fail("Select at least one application."); return; }
        string decision = F("decision").ToUpper();
        string notes = F("notes");
        bool useRequested = F("term_mode") != "fixed";
        int fixedTerm = SafeInt(F("term_months"));
        if (decision != "APPROVED" && notes == "") { Fail("Add the Council notes for this decision."); return; }

        int done = 0;
        List<string> skipped = new List<string>();
        List<string[]> mails = new List<string[]>();   // to, subject, html, renewal_id
        foreach (int id in ids)
        {
            DataRow r = LoadRenewal(id);
            if (r == null) continue;
            string refNo = SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : ("#" + id);
            if (SafeStr(r["status"]) != "FORWARDED") { skipped.Add(refNo + ": not at Council (status " + StatusLabel(SafeStr(r["status"])) + ")"); continue; }
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
                to, "Council decision on contract renewal application " + SafeStr(r["ref_no"]),
                BuildEmailHtml(SafeStr(r["emp_name"]), DecisionEmailBody(r, p, notes)), id.ToString(), p.Decision });
        }
        SendInBackground(mails, "EMAIL_DECISION");
        WriteBulkResult(done, "recorded as " + DecisionLabel(decision) + (mails.Count > 0 ? ". Outcome emails are being sent" : ""), skipped);
    }

    // ── issue the new contract ──────────────────────────────────────────
    private void AjaxIssueContract()
    {
        int id = SafeInt(F("id"));
        if (F("confirm") != "1") { Fail("Tick the confirmation before issuing the contract."); return; }
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found."); return; }
        if (SafeStr(r["status"]) != "APPROVED") { Fail("A contract can be issued only for an approved application."); return; }
        if (SafeInt(r["new_contract_id"]) > 0) { Fail("A contract has already been issued for this application."); return; }

        DateTime start, end;
        if (!DateTime.TryParseExact(F("start"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) { Fail("Enter the contract start date."); return; }
        if (!DateTime.TryParseExact(F("end"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)) { Fail("Enter the contract end date."); return; }
        if (end <= start) { Fail("The contract must end after it starts."); return; }
        int jobId = SafeInt(F("job_id")), deptId = SafeInt(F("dept_id")), scale = SafeInt(F("payscale"));
        string ctype = F("contract_type").ToUpper();
        if (ctype != "FULL TIME" && ctype != "PART TIME") { Fail("Choose full time or part time."); return; }
        double fixedAmt;
        if (!double.TryParse(F("fixedamount").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out fixedAmt) || fixedAmt < 0) fixedAmt = 0;
        if (jobId <= 0 || Q("SELECT 1 FROM hrm_jobs WHERE ID = @j", new MySqlParameter("@j", jobId)).Rows.Count == 0) { Fail("Choose a position."); return; }
        if (deptId <= 0 || Q("SELECT 1 FROM hrm_departments WHERE ID = @d", new MySqlParameter("@d", deptId)).Rows.Count == 0) { Fail("Choose a department."); return; }
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
        Response.Write("{\"ok\":true,\"msg\":\"New contract issued: " + start.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + " to " +
                       end.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + ".\",\"contract_id\":" + newId + "}");
    }

    // ── skip supervisor ─────────────────────────────────────────────────
    private void AjaxSkipSupervisor()
    {
        int id = SafeInt(F("id"));
        string reason = F("reason");
        if (reason.Length < 5) { Fail("Enter the reason for skipping the supervisor."); return; }
        DataRow r = LoadRenewal(id);
        if (r == null) { Fail("Application not found."); return; }
        if (SafeStr(r["status"]) != "AWAITING_SUPERVISOR") { Fail("Only an application with the supervisor can be moved to HR."); return; }
        int n = X(@"UPDATE hr_contract_renewals SET status = 'AWAITING_HR', hr_actor = @a, updated_at = NOW()
                    WHERE renewal_id = @id AND status = 'AWAITING_SUPERVISOR'",
            new MySqlParameter("@a", ActorTag), new MySqlParameter("@id", id));
        if (n == 0) { Fail("The application changed while you were working on it. Reload the page."); return; }
        WriteAudit(id, "HR_SKIPPED_SUPERVISOR", "AWAITING_SUPERVISOR", "AWAITING_HR", "{\"reason\":\"" + Js(reason) + "\"}");
        Ok("Moved to HR without a supervisor recommendation.");
    }

    private void WriteBulkResult(int done, string what, List<string> skipped)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("{{\"ok\":{0},\"done\":{1},\"msg\":\"{2}\",\"skipped\":[", done > 0 ? "true" : "false", done,
            Js(done + " application(s) " + what + "." + (skipped.Count > 0 ? " " + skipped.Count + " skipped." : "")));
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
        return d.HasValue ? " by " + d.Value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) : "";
    }

    /// <summary>
    /// Synchronous single send; never throws. Returns a short sentence for the user and, in
    /// auditNote, the technical result for hr_renewal_audit (SENT / FAILED with the sender's reply).
    /// </summary>
    private string SendEmployeeEmail(DataRow r, string subject, string bodyHtml, out string auditNote)
    {
        string to = RecipientOf(r);
        if (to == "" || !to.Contains("@")) { auditNote = "NO_EMAIL"; return "There is no email address on record, so inform the employee directly."; }
        try
        {
            string res = EmailSenderProtocol.SendHtmlEmail(BuildEmailHtml(SafeStr(r["emp_name"]), bodyHtml), to, subject, "MRU Human Resource");
            if (res != null && res.StartsWith("Email sent", StringComparison.OrdinalIgnoreCase))
            {
                auditNote = "SENT to " + to;
                return "Email sent to " + to + ".";
            }
            auditNote = "FAILED " + to + ": " + (res ?? "unknown error");
        }
        catch (Exception ex) { auditNote = "FAILED " + to + ": " + ex.Message; }
        if (auditNote.Length > 250) auditNote = auditNote.Substring(0, 250);
        return "The email to " + to + " could not be sent, so inform the employee directly.";
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

    /// <summary>
    /// The one email layout for contract renewal notices: navy band with the university name,
    /// greeting, the body paragraphs, one button to My Contracts, sign-off from the HR Office.
    /// </summary>
    public static string BuildEmailHtml(string empName, string bodyHtml)
    {
        string portal = (ConfigurationManager.AppSettings["PORTAL_BASE_URL"] ?? "https://eportal.mru.ac.ug").TrimEnd('/');
        string link = portal + "/MyContracts.aspx";
        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>" +
               "<body style=\"margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;\">" +
               "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#f5f7fa;padding:24px 12px;\"><tr><td align=\"center\">" +
               "<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border:1px solid #e0e5ed;\">" +
               "<tr><td style=\"background:#05275C;padding:16px 28px;color:#ffffff;font-size:17px;font-weight:700;\">Muteesa I Royal University</td></tr>" +
               "<tr><td style=\"padding:24px 28px;font-size:14px;color:#1a1a2e;line-height:1.6;\">" +
               EmailP("Dear " + HttpUtility.HtmlEncode(empName) + ",") +
               bodyHtml +
               "<p style=\"margin:20px 0;\"><a href=\"" + HttpUtility.HtmlAttributeEncode(link) + "\" style=\"display:inline-block;background:#05275C;color:#ffffff;text-decoration:none;padding:10px 22px;font-weight:700;font-size:13px;\">Open My Contracts</a></p>" +
               "<p style=\"margin:0;\">Human Resource Office</p>" +
               "</td></tr></table></td></tr></table></body></html>";
    }

    /// <summary>One email paragraph (content already encoded).</summary>
    public static string EmailP(string html) { return "<p style=\"margin:0 0 14px;\">" + html + "</p>"; }

    /// <summary>Quoted free text (reason, notes): plain, indented, line breaks kept.</summary>
    public static string EmailQuote(string text)
    {
        return "<p style=\"margin:0 0 14px;padding-left:12px;border-left:3px solid #e0e5ed;\">" +
               HttpUtility.HtmlEncode(HrExport.Clean(text ?? "")).Replace("\r\n", "\n").Replace("\n", "<br/>") + "</p>";
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
            litError.Text = "<div class='hr-notice hr-notice--bad'>Application not found. <a href='ContractRenewals.aspx'>Back to contract renewals</a></div>";
            return;
        }
        string st = SafeStr(r["status"]);
        string refNo = SafeStr(r["ref_no"]) != "" ? SafeStr(r["ref_no"]) : "#" + id;
        hfId.Value = id.ToString();
        hfStatus.Value = st;
        Page.Title = refNo + " " + SafeStr(r["emp_name"]) + " - Campus Dynamics";

        // ── header: ref, name, status ──
        litName.Text = Enc(SafeStr(r["emp_name"]));
        litStatus.Text = StatusBadge(st);
        litHeaderMeta.Text = Enc(refNo) + (D(r["submitted_at"]).HasValue ? ", submitted " + FmtD(r["submitted_at"]) : ", not yet submitted");
        litPrintLink.Text = "<a class='hr-btn hr-btn--inverse' href='ContractRenewalPrint.aspx?id=" + id + "' target='_blank'>" + IconPrint() + " Print pack</a>";

        // ── one-line notices ──
        StringBuilder al = new StringBuilder();
        if (st == "RETURNED")
            al.AppendFormat("<div class='hr-notice hr-notice--warn'>Returned by {0} on {1}: {2}</div>",
                SafeStr(r["returned_by"]) == "HR" ? "HR" : "the supervisor", FmtDT(r["returned_at"]), Nl(SafeStr(r["return_reason"])));
        if (st == "WITHDRAWN")
            al.AppendFormat("<div class='hr-notice'>Withdrawn by the employee on {0}{1}</div>",
                FmtDT(r["withdrawn_at"]), SafeStr(r["withdraw_reason"]) != "" ? ": " + Nl(SafeStr(r["withdraw_reason"])) : ".");
        if (SafeInt(r["is_late"]) == 1)
            al.AppendFormat("<div class='hr-notice hr-notice--warn'>Submitted late: {0} days before the contract ends, less than the three months required.</div>",
                SafeInt(r["days_to_expiry_at_submit"]));
        DateTime? curEnd = D(r["cur_end"]);
        if (curEnd.HasValue && st != "CONTRACT_ISSUED" && st != "NOT_APPROVED" && st != "WITHDRAWN")
        {
            int left = (int)(curEnd.Value.Date - DateTime.Today).TotalDays;
            if (left < 0) al.AppendFormat("<div class='hr-notice hr-notice--bad'>The current contract ended {0} ago.</div>", Days(-left));
        }
        litAlerts.Text = al.ToString();

        // ── workflow position ──
        litStages.Text = StagesHtml(st);

        // ── applicant, current contract and appraisal summary ──
        int days = curEnd.HasValue ? (int)(curEnd.Value.Date - DateTime.Today).TotalDays : 0;
        StringBuilder a = new StringBuilder("<dl class='hr-dl hr-dl--2'>");
        Dl(a, "Employee", Enc(SafeStr(r["emp_name"])) + (SafeStr(r["emp_code"]) != "" ? " <span class='hr-code'>" + Enc(SafeStr(r["emp_code"])) + "</span>" : ""));
        Dl(a, "Category", Enc(CategoryLabel(SafeStr(r["staff_category"]))));
        Dl(a, "Position", Enc(SafeStr(r["cur_job"])));
        Dl(a, "Department", Enc(SafeStr(r["cur_department"])));
        Dl(a, "Contract", Enc(JoinNonEmpty(", ", Words(SafeStr(r["cur_type"])), Words(SafeStr(r["cur_contract_status"])))));
        Dl(a, "Period", Range(r["cur_start"], r["cur_end"]) + (curEnd.HasValue && st != "CONTRACT_ISSUED" ? " " + DaysLeftHtml(days) : ""));
        Dl(a, "Supervisor", Enc(SafeStr(r["reviewer_live_name"])));
        Dl(a, "Contact", Enc(JoinNonEmpty(", ", SafeStr(r["contact_phone"]), SafeStr(r["contact_email"]))));
        a.Append("</dl>");
        DateTime? liveEnd = D(r["live_end"]);
        if (liveEnd.HasValue && curEnd.HasValue && liveEnd.Value.Date != curEnd.Value.Date)
            a.AppendFormat("<div class='hr-hint' style='margin-top:8px;'>The contract record now ends {0}, not {1} as at application. Check before issuing.</div>", FmtD(r["live_end"]), FmtD(r["cur_end"]));

        DataTable ap = Q(@"SELECT ar.record_id, ar.status, ar.final_percentage, ar.classification, ar.employee_submitted_at, ar.created_at,
                                  IFNULL(s.session_title,'') AS session_title
                           FROM appraisal_records ar LEFT JOIN appraisal_sessions s ON s.session_id = ar.session_id
                           WHERE ar.employee_id = @e
                           ORDER BY COALESCE(ar.employee_submitted_at, ar.created_at) DESC, ar.record_id DESC",
            new MySqlParameter("@e", SafeInt(r["employee_id"])));
        a.Append("<div class='cr-sec'><div class='hr-label cr-sec__t'>Performance appraisals</div>");
        if (ap.Rows.Count == 0) a.Append("<div class='hr-muted'>No appraisal records.</div>");
        else
        {
            a.Append("<div class='hr-table-wrap'><table class='hr-table cr-mini'><thead><tr><th>Session</th><th class='hr-num'>Score (%)</th><th>Classification</th><th>Status</th><th></th></tr></thead><tbody>");
            foreach (DataRow x in ap.Rows)
            {
                int recId = SafeInt(x["record_id"]);
                string pct = x["final_percentage"] == DBNull.Value ? "" : Convert.ToDecimal(x["final_percentage"]).ToString("0.0", CultureInfo.InvariantCulture);
                a.AppendFormat("<tr><td>{0}</td><td class='hr-num'>{1}</td><td>{2}</td><td>{3}</td><td class='hr-right cr-nowrap'><a href='AppraisalView.aspx?rid={4}'>View</a></td></tr>",
                    Enc(SafeStr(x["session_title"])), pct, Enc(SafeStr(x["classification"])), Enc(AppraisalStatusLabel(SafeStr(x["status"]))), recId);
            }
            a.Append("</tbody></table></div>");
        }
        a.Append("</div>");
        litApplicant.Text = a.ToString();

        // ── request ──
        StringBuilder q = new StringBuilder("<dl class='hr-dl hr-dl--2'>");
        Dl(q, "Requested term", SafeInt(r["requested_term_months"]) > 0 ? SafeInt(r["requested_term_months"]) + " months" : "");
        Dl(q, "Requested period", Range(r["requested_start"], r["requested_end"]));
        Dl(q, "Contract type", Enc(Words(SafeStr(r["requested_type"]))));
        Dl(q, "Position", Enc(SafeStr(r["requested_position"])));
        Dl(q, "Round", Enc(SafeStr(r["round_title"])));
        Dl(q, "Signed by employee", SafeStr(r["employee_sign_name"]) != "" ? Enc(SafeStr(r["employee_sign_name"])) + ", " + FmtDT(r["employee_signed_at"]) : "");
        q.Append("</dl>");
        q.Append(Para("Why the contract should be renewed", SafeStr(r["justification"])));
        q.Append(Para("Plans for the next contract", SafeStr(r["future_plans"])));
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
            t.AppendFormat("<tr><td class='hr-num'>{0}</td><td><strong>{1}</strong></td><td>{2}</td><td>{3}</td><td>{4}{5}</td><td>{6}</td></tr>",
                i, Nl(SafeStr(x["kpa"])), Nl(SafeStr(x["expected_standard"])), Nl(SafeStr(x["achievement"])),
                Nl(SafeStr(x["evidence"])), files.Length > 0 ? "<div class='cr-files'>" + files + "</div>" : "",
                Nl(SafeStr(x["reviewer_comment"])));
        }
        if (ach.Rows.Count == 0) t.Append("<tr><td colspan='6' class='hr-empty'>No rows entered.</td></tr>");
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
                foreach (DataRow x in ach.Rows) { k++; if (SafeInt(x["item_id"]) == SafeInt(d["item_id"])) { forItem = " for row " + k; break; } }
            }
            dl.AppendFormat("<tr><td>{0}{1}</td><td>{2}</td><td class='hr-num'>{3}</td><td class='cr-nowrap'>{4}</td></tr>",
                Enc(DocTypeLabel(type)), forItem, DocLink(d), SizeLabel(SafeInt(d["size_bytes"])), FmtDT(d["uploaded_at"]));
        }
        if (docs.Rows.Count == 0) dl.Append("<tr><td colspan='4' class='hr-empty'>No documents uploaded.</td></tr>");
        litDocs.Text = dl.ToString();

        // ── supervisor ──
        StringBuilder sv = new StringBuilder();
        if (SafeStr(r["sup_recommendation"]) == "")
            sv.Append("<div class='hr-muted'>" + (st == "AWAITING_SUPERVISOR" ? "Awaiting the supervisor's recommendation." : "Not recorded.") + "</div>");
        else
        {
            sv.Append("<dl class='hr-dl hr-dl--2'>");
            Dl(sv, "Recommendation", RecBadge(SafeStr(r["sup_recommendation"])));
            Dl(sv, "Suggested term", SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]) + " months" : "");
            Dl(sv, "Supervisor", Enc(JoinNonEmpty(", ", SafeStr(r["sup_name"]), SafeStr(r["sup_title"]))));
            Dl(sv, "Signed", FmtDT(r["sup_signed_at"]));
            sv.Append("</dl>");
            sv.Append(Para("Comments", SafeStr(r["sup_comments"])));
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
            string autoTag = key == "contract_ok" ? "<span class='hr-badge hr-badge--neutral'>Manual check</span>"
                           : (auto[key] ? "<span class='hr-badge hr-badge--ok'>Found</span>" : "<span class='hr-badge hr-badge--bad'>Not found</span>");
            hv.AppendFormat("<label class='cr-check__row'><input type='checkbox' id='chk_{0}' {1}{2}/> <span>{3}</span>{4}</label>",
                key, isTicked ? "checked " : "", editable ? "" : "disabled ", Enc(CheckLabels[k]), autoTag);
        }
        hv.Append("</div>");
        hv.AppendFormat("<div class='hr-field' style='margin-bottom:12px;'><label class='hr-label' for='hrComments'>HR remarks for Council</label><textarea id='hrComments' class='hr-textarea' {0}>{1}</textarea></div>",
            editable ? "" : "disabled", Enc(SafeStr(r["hr_comments"])));
        if (st == "AWAITING_HR")
            hv.AppendFormat("<div class='hr-field' style='margin-bottom:12px;'><label class='hr-label' for='councilSitting'>Council sitting</label><input type='text' id='councilSitting' class='hr-input' maxlength='100' value='{0}' placeholder='e.g. November 2026' /></div>",
                Enc(SafeStr(r["council_sitting"]) != "" ? SafeStr(r["council_sitting"]) : SafeStr(r["round_sitting"])));
        if (SafeStr(r["hr_verified_at"]) != "")
            hv.AppendFormat("<div class='hr-hint'>Verified by {0} on {1}{2}.</div>", Enc(Actor(SafeStr(r["hr_actor"]))), FmtDT(r["hr_verified_at"]),
                SafeStr(r["forwarded_at"]) != "" ? ", forwarded to the " + Enc(SafeStr(r["council_sitting"])) + " Council sitting on " + FmtDT(r["forwarded_at"]) : "");
        litHrVerify.Text = hv.ToString();

        // ── Council decision and new contract ──
        StringBuilder dc = new StringBuilder();
        if (SafeStr(r["decision"]) == "")
            dc.Append("<div class='hr-muted'>" + (st == "FORWARDED" ? "Awaiting the decision of the " + Enc(SafeStr(r["council_sitting"])) + " sitting." : "Not recorded.") + "</div>");
        else
        {
            dc.Append("<dl class='hr-dl hr-dl--2'>");
            Dl(dc, "Decision", StatusBadge(SafeStr(r["decision"])));
            Dl(dc, "Council sitting", Enc(SafeStr(r["council_sitting"])));
            if (SafeStr(r["decision"]) == "APPROVED")
            {
                Dl(dc, "Approved term", SafeInt(r["decision_term_months"]) + " months");
                Dl(dc, "New contract period", Range(r["decision_start"], r["decision_end"]));
            }
            Dl(dc, "Recorded by", Enc(Actor(SafeStr(r["decision_recorded_by"]))));
            Dl(dc, "Recorded on", FmtDT(r["decision_at"]));
            dc.Append("</dl>");
            if (SafeStr(r["decision_notes"]) != "") dc.Append(Para("Council notes", SafeStr(r["decision_notes"])));
        }
        if (SafeInt(r["new_contract_id"]) > 0)
        {
            DataTable nc = Q(@"SELECT c.ID, c.contractStart, c.contractEnd, c.contractStatus, c.contract_type, IFNULL(j.jobname,'') AS jobname, IFNULL(d.dept_name,'') AS dept_name
                               FROM hrm_emp_contracts c LEFT JOIN hrm_jobs j ON j.ID = c.jobID LEFT JOIN hrm_departments d ON d.ID = c.departmentID WHERE c.ID = @c",
                new MySqlParameter("@c", SafeInt(r["new_contract_id"])));
            dc.Append("<div class='cr-sec'><div class='hr-label cr-sec__t'>New contract</div>");
            if (nc.Rows.Count > 0)
            {
                DataRow c = nc.Rows[0];
                dc.Append("<dl class='hr-dl hr-dl--2'>");
                Dl(dc, "Period", Range(c["contractStart"], c["contractEnd"]));
                Dl(dc, "Type", Enc(JoinNonEmpty(", ", Words(SafeStr(c["contract_type"])), Words(SafeStr(c["contractStatus"])))));
                Dl(dc, "Position", Enc(SafeStr(c["jobname"])));
                Dl(dc, "Department", Enc(SafeStr(c["dept_name"])));
                dc.Append("</dl>");
            }
            else dc.Append("<div class='hr-muted'>The contract record is no longer available.</div>");
            dc.AppendFormat("<div class='hr-hint' style='margin-top:8px;'>Issued by {0} on {1}. <a href='HRContracts.aspx'>Open contracts</a></div></div>",
                Enc(Actor(SafeStr(r["contract_issued_by"]))), FmtDT(r["contract_issued_at"]));
        }
        litDecision.Text = dc.ToString();

        // ── action bar ──
        StringBuilder ac = new StringBuilder();
        switch (st)
        {
            case "AWAITING_SUPERVISOR":
                ac.Append("<button type='button' class='hr-btn hr-btn--secondary' onclick=\"openModal('skipModal')\">Skip supervisor</button>");
                break;
            case "AWAITING_HR":
                ac.Append("<button type='button' class='hr-btn hr-btn--secondary' onclick='saveChecklist()'>Save checklist</button>");
                ac.Append("<button type='button' class='hr-btn hr-btn--danger' onclick=\"openModal('returnModal')\">Return to employee</button>");
                ac.Append("<button type='button' class='hr-btn hr-btn--primary' onclick='forwardApp()'>" + IconSend() + " Forward to Council</button>");
                break;
            case "FORWARDED":
                ac.Append("<button type='button' class='hr-btn hr-btn--secondary' onclick='saveChecklist()'>Save checklist</button>");
                ac.Append("<button type='button' class='hr-btn hr-btn--danger' onclick=\"openModal('returnModal')\">Return to employee</button>");
                ac.Append("<button type='button' class='hr-btn hr-btn--primary' onclick=\"openModal('decisionModal')\">Record Council decision</button>");
                break;
            case "APPROVED":
                ac.Append("<button type='button' class='hr-btn hr-btn--primary' onclick=\"openModal('issueModal')\">Issue new contract</button>");
                break;
        }
        litActions.Text = ac.Length > 0 ? ac.ToString() : "<span class='hr-muted'>No HR action at this stage.</span>";

        // decision modal defaults
        int defTerm = SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]) : SafeInt(r["requested_term_months"]);
        if (defTerm <= 0) defTerm = 24;
        DateTime defStart = curEnd.HasValue ? curEnd.Value.AddDays(1) : DateTime.Today;
        hfDefTerm.Value = defTerm.ToString();
        hfDefStart.Value = defStart.ToString("yyyy-MM-dd");
        hfCurEnd.Value = curEnd.HasValue ? curEnd.Value.ToString("yyyy-MM-dd") : "";

        if (st == "APPROVED") RenderIssueModal(r);

        // ── history ──
        DataTable au = Q(@"SELECT action, old_status, new_status, actor_username, payload_json, created_at
                           FROM hr_renewal_audit WHERE renewal_id = @id ORDER BY audit_id DESC", new MySqlParameter("@id", id));
        StringBuilder tl = new StringBuilder();
        foreach (DataRow x in au.Rows)
        {
            string detail = AuditDetail(SafeStr(x["payload_json"]));
            tl.AppendFormat("<li><div class='cr-tl__when'>{0}</div><div><strong>{1}</strong> <span class='hr-muted'>by {2}</span>{3}</div></li>",
                FmtDT(x["created_at"]), Enc(AuditLabel(SafeStr(x["action"]))), Enc(Actor(SafeStr(x["actor_username"]))),
                detail != "" ? "<div class='cr-tl__d'>" + detail + "</div>" : "");
        }
        if (au.Rows.Count == 0) tl.Append("<li><div class='hr-muted'>No history yet.</div></li>");
        litAudit.Text = tl.ToString();
    }

    /// <summary>Workflow position: Application, Supervisor, HR, Council, Contract.</summary>
    private static string StagesHtml(string st)
    {
        string[] names = { "Application", "Supervisor", "HR verification", "Council", "New contract" };
        int now; bool stop = false;
        switch (st)
        {
            case "DRAFT": case "RETURNED": now = 0; break;
            case "WITHDRAWN": now = 0; stop = true; break;
            case "AWAITING_SUPERVISOR": now = 1; break;
            case "AWAITING_HR": now = 2; break;
            case "FORWARDED": case "DEFERRED": now = 3; break;
            case "NOT_APPROVED": now = 3; stop = true; break;
            case "APPROVED": now = 4; break;
            case "CONTRACT_ISSUED": now = 5; break;
            default: now = 0; break;
        }
        StringBuilder sb = new StringBuilder("<ol class='hr-stages'>");
        for (int i = 0; i < names.Length; i++)
        {
            string cls = i < now ? "is-done" : (i == now ? (stop ? "is-stop" : "is-now") : "");
            sb.AppendFormat("<li class='{0}'><span></span>{1}</li>", cls, names[i]);
        }
        return sb.Append("</ol>").ToString();
    }

    private void RenderIssueModal(DataRow r)
    {
        DateTime? ds = D(r["decision_start"]), de = D(r["decision_end"]);
        int jobId = SafeInt(r["live_job_id"]) > 0 ? SafeInt(r["live_job_id"]) : SafeInt(r["cur_job_id"]);
        int deptId = SafeInt(r["live_dept_id"]) > 0 ? SafeInt(r["live_dept_id"]) : SafeInt(r["cur_department_id"]);
        int scale = SafeInt(r["live_payscale"]);
        string ctype = SafeStr(r["live_type"]) != "" ? SafeStr(r["live_type"]) : SafeStr(r["cur_type"]);
        double fixedAmt = r["live_fixed"] == DBNull.Value ? 0 : Convert.ToDouble(r["live_fixed"]);

        StringBuilder sb = new StringBuilder("<div class='hr-form'>");
        sb.AppendFormat("<div class='hr-field'><label class='hr-label' for='icStart'>Contract start <span class='hr-req'>*</span></label><input type='date' id='icStart' class='hr-input' value='{0}' /></div>", ds.HasValue ? ds.Value.ToString("yyyy-MM-dd") : "");
        sb.AppendFormat("<div class='hr-field'><label class='hr-label' for='icEnd'>Contract end <span class='hr-req'>*</span></label><input type='date' id='icEnd' class='hr-input' value='{0}' /></div>", de.HasValue ? de.Value.ToString("yyyy-MM-dd") : "");
        sb.Append("<div class='hr-field'><label class='hr-label' for='icJob'>Position <span class='hr-req'>*</span></label><select id='icJob' class='hr-select'>");
        foreach (DataRow j in Q("SELECT ID, jobname FROM hrm_jobs ORDER BY jobname").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", SafeInt(j["ID"]), SafeInt(j["ID"]) == jobId ? " selected" : "", Enc(SafeStr(j["jobname"])));
        sb.Append("</select></div>");
        sb.Append("<div class='hr-field'><label class='hr-label' for='icDept'>Department <span class='hr-req'>*</span></label><select id='icDept' class='hr-select'>");
        foreach (DataRow d in Q("SELECT ID, dept_name FROM hrm_departments ORDER BY dept_name").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", SafeInt(d["ID"]), SafeInt(d["ID"]) == deptId ? " selected" : "", Enc(SafeStr(d["dept_name"])));
        sb.Append("</select></div>");
        sb.AppendFormat("<div class='hr-field'><label class='hr-label' for='icType'>Contract type <span class='hr-req'>*</span></label><select id='icType' class='hr-select'><option value='FULL TIME'{0}>Full time</option><option value='PART TIME'{1}>Part time</option></select></div>",
            ctype == "PART TIME" ? "" : " selected", ctype == "PART TIME" ? " selected" : "");
        sb.Append("<div class='hr-field'><label class='hr-label' for='icScale'>Pay scale</label><select id='icScale' class='hr-select'><option value='0'>None</option>");
        foreach (DataRow p in Q("SELECT ID, scale_name, basicpay FROM hrm_payscales ORDER BY scale_name").Rows)
            sb.AppendFormat("<option value='{0}'{1}>{2} (UGX {3})</option>", SafeInt(p["ID"]), SafeInt(p["ID"]) == scale ? " selected" : "",
                Enc(SafeStr(p["scale_name"])), p["basicpay"] == DBNull.Value ? "0" : Convert.ToDouble(p["basicpay"]).ToString("#,##0", CultureInfo.InvariantCulture));
        sb.Append("</select></div>");
        sb.AppendFormat("<div class='hr-field'><label class='hr-label' for='icFixed'>Fixed amount (UGX)</label><input type='number' id='icFixed' class='hr-input' min='0' step='1' value='{0}' /><span class='hr-hint'>Copied from the current contract.</span></div>",
            fixedAmt.ToString("0", CultureInfo.InvariantCulture));
        sb.Append("</div>");
        sb.Append("<label class='cr-confirm'><input type='checkbox' id='icConfirm' /> <span>I confirm the Governance Council approved this renewal and the contract details above are correct.</span></label>");
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

    /// <summary>Readable detail line for a history entry (only the keys people need).</summary>
    private static string AuditDetail(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return "";
        string[][] keys = {
            new[] { "reason", "Reason" }, new[] { "council_sitting", "Council sitting" }, new[] { "decision", "Decision" },
            new[] { "term_months", "Term (months)" }, new[] { "start", "Start" }, new[] { "end", "End" }, new[] { "notes", "Notes" },
            new[] { "comments", "Comments" }, new[] { "type", "Document" }, new[] { "file", "File" }, new[] { "email", "Email" } };
        List<string> parts = new List<string>();
        foreach (string[] k in keys)
        {
            string v = JsonValue(payload, k[0]);
            if (v == null || v.Trim() == "" || v == "0") continue;
            if (k[0] == "decision") v = StatusLabel(v);
            else if (k[0] == "type") v = DocTypeLabel(v);
            else if (k[0] == "start" || k[0] == "end") { DateTime? d = D(v); if (d.HasValue) v = d.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture); }
            else if (k[0] == "email") v = EmailOutcome(v);
            if (v.Length > 300) v = v.Substring(0, 300) + "...";
            parts.Add(HttpUtility.HtmlEncode(k[1]) + ": " + HttpUtility.HtmlEncode(HrExport.Clean(v)));
        }
        return string.Join("; ", parts.ToArray());
    }

    /// <summary>Audit email notes (SENT to x / FAILED x: error / NO_EMAIL / older free text) as a short phrase, no raw errors.</summary>
    private static string EmailOutcome(string v)
    {
        string s = (v ?? "").Trim();
        if (s.StartsWith("SENT", StringComparison.OrdinalIgnoreCase))
        {
            string rest = s.Substring(4).Trim().TrimEnd('.');
            if (rest.StartsWith("to ")) rest = rest.Substring(3);
            return rest == "" ? "sent" : "sent to " + rest;
        }
        if (s.StartsWith("Email sent", StringComparison.OrdinalIgnoreCase)) return "sent" + s.Substring(10).TrimEnd('.');
        if (s.StartsWith("NO_EMAIL", StringComparison.OrdinalIgnoreCase) || s.StartsWith("No email", StringComparison.OrdinalIgnoreCase)) return "no email address on record";
        if (s.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase) || s.Contains("failed"))
        {
            int at = s.IndexOf('@');
            if (at > 0)
            {
                int st = s.LastIndexOf(' ', at) + 1, en = s.IndexOfAny(new[] { ' ', ':' }, at);
                return "not sent to " + (en > st ? s.Substring(st, en - st) : s.Substring(st));
            }
            return "not sent";
        }
        return s;
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
            case "FORWARDED": return "At Council";
            case "APPROVED": return "Approved";
            case "NOT_APPROVED": return "Not approved";
            case "DEFERRED": return "Deferred";
            case "CONTRACT_ISSUED": return "Contract issued";
            case "WITHDRAWN": return "Withdrawn";
            default: return Words(s);
        }
    }

    private static string DecisionLabel(string d) { return StatusLabel(d).ToLower(); }

    /// <summary>Five badge kinds only (hr.css): neutral, info, ok, warn, bad.</summary>
    public static string StatusBadge(string s)
    {
        string cls;
        switch (s)
        {
            case "AWAITING_SUPERVISOR": case "FORWARDED": cls = "info"; break;
            case "AWAITING_HR": case "RETURNED": case "DEFERRED": cls = "warn"; break;
            case "APPROVED": case "CONTRACT_ISSUED": cls = "ok"; break;
            case "NOT_APPROVED": cls = "bad"; break;
            default: cls = "neutral"; break;
        }
        return "<span class='hr-badge hr-badge--" + cls + "'>" + HttpUtility.HtmlEncode(StatusLabel(s)) + "</span>";
    }

    public static string RecBadge(string r)
    {
        switch (r)
        {
            case "RECOMMEND": return "<span class='hr-badge hr-badge--ok'>Recommended</span>";
            case "RECOMMEND_WITH_CONDITIONS": return "<span class='hr-badge hr-badge--warn'>With conditions</span>";
            case "NOT_RECOMMENDED": return "<span class='hr-badge hr-badge--bad'>Not recommended</span>";
            case "": case null: return "";
            default: return HttpUtility.HtmlEncode(Words(r));
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
            default: return Words(t);
        }
    }

    private static string AppraisalStatusLabel(string s)
    {
        switch (s)
        {
            case "PENDING": return "Not started";
            case "EMPLOYEE_IN_PROGRESS": return "In progress";
            case "EMPLOYEE_SUBMITTED": case "SUPERVISOR_IN_PROGRESS": return "With supervisor";
            case "COMPLETED": return "Awaiting HR";
            case "HR_REVIEWED": return "Reviewed by HR";
            case "RETURNED": return "Returned";
            default: return Words(s);
        }
    }

    private static string AuditLabel(string a)
    {
        switch (a)
        {
            case "CREATED": return "Application started";
            case "SUBMITTED": return "Submitted";
            case "RESUBMITTED": return "Resubmitted";
            case "WITHDRAWN": return "Withdrawn";
            case "DOCUMENT_UPLOADED": return "Document uploaded";
            case "DOCUMENT_REMOVED": return "Document removed";
            case "SUPERVISOR_RECOMMENDED": return "Supervisor recommendation";
            case "RETURNED_BY_SUPERVISOR": return "Supervisor returned the application";
            case "HR_SKIPPED_SUPERVISOR": return "Supervisor stage skipped";
            case "HR_CHECKLIST_SAVED": return "HR checklist saved";
            case "HR_FORWARDED": return "Forwarded to Council";
            case "HR_RETURNED": return "Returned to the employee";
            case "COUNCIL_DECISION": return "Council decision recorded";
            case "EMAIL_DECISION": return "Decision email";
            case "REMINDER_EMAIL": return "Reminder email";
            case "CONTRACT_ISSUED": return "New contract issued";
            default: return Words(a);
        }
    }

    /// <summary>"FULL_TIME" / "FULL TIME" / "VALID" become "Full time" / "Valid".</summary>
    public static string Words(string code)
    {
        string s = (code ?? "").Replace("_", " ").Trim();
        if (s == "") return "";
        s = s.ToLowerInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>Who did it: "eadmin:jdoe" becomes "jdoe".</summary>
    private static string Actor(string tag)
    {
        string s = (tag ?? "").Trim();
        int p = s.IndexOf(':');
        return p > 0 && p < 12 ? s.Substring(p + 1) : s;
    }

    private static string JoinNonEmpty(string sep, params string[] parts)
    {
        List<string> keep = new List<string>();
        foreach (string p in parts) if (!string.IsNullOrEmpty((p ?? "").Trim())) keep.Add(p.Trim());
        return string.Join(sep, keep.ToArray());
    }

    /// <summary>"1 Jan 2025 to 31 Dec 2026" (dates already safe).</summary>
    private static string Range(object from, object to)
    {
        string a = FmtD(from), b = FmtD(to);
        if (a == "" && b == "") return "";
        return (a == "" ? "Not recorded" : a) + " to " + (b == "" ? "Not recorded" : b);
    }

    private static string Days(int n) { return n == 1 ? "1 day" : n + " days"; }

    private static string Para(string label, string text)
    {
        return "<div class='cr-para'><div class='hr-label'>" + HttpUtility.HtmlEncode(label) + "</div><div class='cr-para__txt'>" +
               (string.IsNullOrEmpty((text ?? "").Trim()) ? "<span class='hr-muted'>Not recorded</span>" : Nl(text)) + "</div></div>";
    }

    private static string DocLink(DataRow d)
    {
        return "<a class='cr-file' href='ContractRenewalView.aspx?ajax=doc&amp;doc=" + SafeInt(d["doc_id"]) + "' target='_blank'>" +
               "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/></svg>" +
               HttpUtility.HtmlEncode(SafeStr(d["original_name"])) + "</a>";
    }

    private static string SizeLabel(int b)
    {
        if (b >= 1048576) return (b / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        if (b >= 1024) return (b / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
        return b + " B";
    }

    /// <summary>Days to the contract end as a badge: ended or 30 days or less = bad, 90 or less = warn.</summary>
    public static string DaysLeftHtml(int days)
    {
        string cls = days <= 30 ? "bad" : (days <= 90 ? "warn" : "neutral");
        string txt = days < 0 ? "Ended " + Days(-days) + " ago" : (days == 0 ? "Ends today" : Days(days) + " left");
        return "<span class='hr-badge hr-badge--" + cls + "'>" + txt + "</span>";
    }

    private static void Dl(StringBuilder sb, string label, string html)
    {
        sb.AppendFormat("<div><dt>{0}</dt><dd>{1}</dd></div>", HttpUtility.HtmlEncode(label), html ?? "");
    }

    private static string Nl(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")).Replace("\r\n", "\n").Replace("\n", "<br/>"); }

    private static string IconPrint()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><polyline points='6 9 6 2 18 2 18 9'/><path d='M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2'/><rect x='6' y='14' width='12' height='8'/></svg>";
    }

    private static string IconSend()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><line x1='22' y1='2' x2='11' y2='13'/><polygon points='22 2 15 22 11 13 2 9 22 2'/></svg>";
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

    public static string FmtD(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : ""; }
    public static string FmtDT(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : ""; }

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
