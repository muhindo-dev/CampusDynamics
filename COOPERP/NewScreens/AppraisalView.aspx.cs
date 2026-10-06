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
/// Appraisals: list with filters and batch actions, and the record detail with HR review.
/// Signed-in staff may view (read-only, as before); every change and every ?ajax= call needs HR access.
/// Section headings are the same as on the printed form (AppraisalPrint).
/// </summary>
public partial class COOPERP_NewScreens_AppraisalView : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ─── Query-string helpers ──────────────────────────────────────────
    private int QsPage   { get { int v; return int.TryParse(Request.QueryString["page"] ?? "1", out v) && v > 0 ? v : 1; } }
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }
    private string QsStatus { get { return (Request.QueryString["status"] ?? "").Trim().ToUpper(); } }
    private string QsCategory { get { return (Request.QueryString["cat"] ?? "").Trim().ToUpper(); } }
    private int QsSession { get { int v; return int.TryParse(Request.QueryString["sid"] ?? "0", out v) && v > 0 ? v : 0; } }
    private int QsRecord  { get { int v; return int.TryParse(Request.QueryString["rid"] ?? "0", out v) && v > 0 ? v : 0; } }
    private string QsReviewer { get { return (Request.QueryString["rev"] ?? "").Trim().ToLower(); } }   // "none" = no supervisor
    private int QsPageSize
    {
        get
        {
            int v;
            if (!int.TryParse(Request.QueryString["ps"] ?? "25", out v)) v = 25;
            return (v == 50 || v == 100 || v == 200) ? v : 25;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim();
        if (!string.IsNullOrEmpty(ajax))
        {
            if (ajax == "evidence")
            {
                ServeEvidence();   // binary stream; signed-in viewers of the record may open it
                return;
            }
            if (!HrAccess.RequireHr(true)) return;
            Response.Clear();
            Response.ContentType = "application/json";
            HandleAjax(ajax);
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
            return;
        }

        // Signed-in staff may view (read-only); changes need HR access.
        if (!HrAccess.IsSignedIn()) { HrAccess.RequireHr(false); return; }

        if (!IsPostBack)
        {
            if (QsRecord > 0)
            {
                pnlList.Visible = false;
                pnlDetail.Visible = true;
                LoadRecordDetail(QsRecord);
            }
            else
            {
                pnlList.Visible = true;
                pnlDetail.Visible = false;
                LoadSessionFilter();
                LoadListStats();
                BindGrid();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH / AUDIT
    // ═══════════════════════════════════════════════════════════════════
    private string CurrentUsername() { return HrAccess.Username(); }

    private bool CanAdmin { get { return HrAccess.IsHr(); } }

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
                DataTable dt = ExecuteQuery("SELECT empID FROM hrm_employee WHERE usernames = @u LIMIT 1", new MySqlParameter("@u", u));
                if (dt.Rows.Count > 0) v = SafeInt(dt.Rows[0]["empID"]);
            }
        }
        catch { }
        _actorEmpId = v;
        return v;
    }

    /// <summary>One appraisal_record_audit row per admin action. Never throws.</summary>
    private void WriteAudit(int rid, string action, string oldStatus, string newStatus, string payloadJson)
    {
        try
        {
            int emp = ActorEmpId();
            ExecuteNonQuery(
                @"INSERT INTO appraisal_record_audit
                    (record_id, actor_empid, actor_username, action, old_status, new_status, payload_json, created_at)
                  VALUES (@rid, @emp, @usr, @act, @old, @new, @pl, NOW())",
                new MySqlParameter("@rid", rid),
                new MySqlParameter("@emp", emp > 0 ? (object)emp : DBNull.Value),
                new MySqlParameter("@usr", "eadmin:" + CurrentUsername()),
                new MySqlParameter("@act", action),
                new MySqlParameter("@old", (object)oldStatus ?? DBNull.Value),
                new MySqlParameter("@new", (object)newStatus ?? DBNull.Value),
                new MySqlParameter("@pl", (object)payloadJson ?? DBNull.Value));
        }
        catch { }
    }

    private string Jstr(string s) { return "\"" + EscapeJson(s ?? "") + "\""; }

    // ═══════════════════════════════════════════════════════════════════
    //  EVIDENCE DOWNLOAD (private folder, never a web path)
    // ═══════════════════════════════════════════════════════════════════
    private void ServeEvidence()
    {
        Response.Clear();
        if (!HrAccess.IsSignedIn())
        {
            Response.Redirect("~/Default.aspx?ReturnUrl=" + HttpUtility.UrlEncode(Request.RawUrl), true);
            return;
        }

        int id;
        if (!int.TryParse(Request.QueryString["id"], out id) || id <= 0) { EvidenceNotFound(); return; }

        DataTable dt = ExecuteQuery(
            "SELECT record_id, stored_name, original_name, content_type FROM appraisal_evidence WHERE evidence_id = @id",
            new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0) { EvidenceNotFound(); return; }

        int recordId     = SafeInt(dt.Rows[0]["record_id"]);
        string stored    = SafeStr(dt.Rows[0]["stored_name"]).Trim();
        string original  = SafeStr(dt.Rows[0]["original_name"]).Trim();
        string ctype     = SafeStr(dt.Rows[0]["content_type"]).Trim().ToLowerInvariant();

        if (stored.Length == 0 || stored != System.IO.Path.GetFileName(stored)
            || stored.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || stored.Contains(".."))
        { EvidenceNotFound(); return; }

        string root = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Server.MapPath("~/").TrimEnd('\\')).FullName,
            "Data_Private", "AppraisalEvidence");
        string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, recordId.ToString(), stored));
        if (!full.StartsWith(System.IO.Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
        { EvidenceNotFound(); return; }

        bool inline = ctype == "application/pdf" || ctype == "image/jpeg" || ctype == "image/png" || ctype == "image/gif";
        string safeName = System.Text.RegularExpressions.Regex.Replace(
            string.IsNullOrEmpty(original) ? stored : original, @"[^\w\.\- ]", "_");

        Response.ContentType = inline ? ctype : "application/octet-stream";
        Response.AddHeader("X-Content-Type-Options", "nosniff");
        Response.AddHeader("Cache-Control", "private, no-store");
        Response.AddHeader("Content-Disposition", (inline ? "inline" : "attachment") + "; filename=\"" + safeName + "\"");
        Response.TransmitFile(full);
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private void EvidenceNotFound()
    {
        Response.StatusCode = 404;
        Response.ContentType = "text/plain";
        Response.Write("The evidence file was not found.");
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX (caller already passed HrAccess.RequireHr)
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        bool isWrite =
            action == "admin_return" || action == "admin_cancel" || action == "admin_reopen" || action == "hr_input" ||
            action == "batch_return" || action == "batch_reopen" || action == "batch_cancel" || action == "batch_hr_input" ||
            action == "admin_change_supervisor" || action == "batch_change_supervisor";

        if (isWrite && !MarksAntiForgeryService.ValidateRequest())
        {
            Response.Write("{\"ok\":false,\"error\":\"The page has expired. Refresh it and try again.\"}");
            return;
        }

        switch (action)
        {
            case "get_record_detail":       AjaxGetRecordDetail(); break;
            case "admin_return":            AjaxAdminReturn(); break;
            case "admin_cancel":            AjaxAdminCancel(); break;
            case "admin_reopen":            AjaxAdminReopen(); break;
            case "hr_input":                AjaxHrInput(); break;
            case "batch_return":            AjaxBatchReturn(); break;
            case "batch_reopen":            AjaxBatchReopen(); break;
            case "batch_cancel":            AjaxBatchCancel(); break;
            case "batch_hr_input":          AjaxBatchHrInput(); break;
            case "get_employees":           AjaxGetEmployees(); break;
            case "admin_change_supervisor": AjaxChangeSupervisor(); break;
            case "batch_change_supervisor": AjaxBatchChangeSupervisor(); break;
            default:
                Response.Write("{\"ok\":false,\"error\":\"Unknown action.\"}");
                break;
        }
    }

    private void AjaxGetRecordDetail()
    {
        int rid;
        if (!int.TryParse(Request.QueryString["rid"], out rid))
        { Response.Write("{\"ok\":false,\"error\":\"Invalid record.\"}"); return; }

        DataTable dtRec = ExecuteQuery(
            @"SELECT ar.record_id, ar.status, ar.staff_category, ar.final_percentage, ar.classification,
                     e.emp_name, e.EMP_CODE, s.session_title
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE ar.record_id = @rid", new MySqlParameter("@rid", rid));
        if (dtRec.Rows.Count == 0) { Response.Write("{\"ok\":false,\"error\":\"Record not found.\"}"); return; }
        DataRow rec = dtRec.Rows[0];
        Response.Write("{\"ok\":true," +
            "\"record_id\":" + SafeInt(rec["record_id"]) + "," +
            "\"status\":" + Jstr(StatusWord(SafeStr(rec["status"]))) + "," +
            "\"emp_name\":" + Jstr(SafeStr(rec["emp_name"])) + "," +
            "\"EMP_CODE\":" + Jstr(SafeStr(rec["EMP_CODE"])) + "," +
            "\"session_title\":" + Jstr(SafeStr(rec["session_title"])) + "," +
            "\"final_percentage\":" + Jstr(SafeDecStr(rec["final_percentage"])) + "," +
            "\"classification\":" + Jstr(ClassWord(Classify(rec["final_percentage"], SafeStr(rec["classification"])))) + "}");
    }

    // ─── request body helpers ─────────────────────────────────────────
    private Dictionary<string, object> ReadJsonBody()
    {
        string body;
        using (System.IO.StreamReader sr = new System.IO.StreamReader(Request.InputStream)) { body = sr.ReadToEnd(); }
        System.Web.Script.Serialization.JavaScriptSerializer jss = new System.Web.Script.Serialization.JavaScriptSerializer();
        Dictionary<string, object> data = jss.Deserialize<Dictionary<string, object>>(string.IsNullOrEmpty(body) ? "{}" : body);
        return data ?? new Dictionary<string, object>();
    }

    private List<int> RidsFrom(Dictionary<string, object> data)
    {
        List<int> out2 = new List<int>();
        if (data.ContainsKey("rids"))
        {
            System.Collections.ArrayList arr = data["rids"] as System.Collections.ArrayList;
            if (arr != null)
                foreach (object v in arr)
                {
                    int n;
                    if (v != null && int.TryParse(v.ToString(), out n) && n > 0 && !out2.Contains(n)) out2.Add(n);
                }
        }
        return out2;
    }

    private string Str(Dictionary<string, object> data, string key)
    {
        return data.ContainsKey(key) && data[key] != null ? data[key].ToString().Trim() : "";
    }

    private int Int(Dictionary<string, object> data, string key)
    {
        int n;
        return data.ContainsKey(key) && data[key] != null && int.TryParse(data[key].ToString(), out n) ? n : 0;
    }

    private string RecordStatus(int rid)
    {
        DataTable dt = ExecuteQuery("SELECT status FROM appraisal_records WHERE record_id = @rid", new MySqlParameter("@rid", rid));
        return dt.Rows.Count == 0 ? null : SafeStr(dt.Rows[0]["status"]).ToUpper();
    }

    private static readonly string[] ValidHrRecommendations = new string[] { "CONFIRM", "EXTEND_PROBATION", "PIP", "PROMOTE", "OTHER" };

    private bool IsValidRecommendation(string rec)
    {
        foreach (string v in ValidHrRecommendations) if (rec == v) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SINGLE-RECORD STATE CHANGES (shared by single + batch)
    // ═══════════════════════════════════════════════════════════════════
    private bool DoReturn(int rid, string comment)
    {
        string st = RecordStatus(rid);
        if (st != "EMPLOYEE_SUBMITTED" && st != "SUPERVISOR_IN_PROGRESS") return false;
        ExecuteNonQuery(
            "UPDATE appraisal_records SET status = 'RETURNED', supervisor_return_comment = @c WHERE record_id = @rid",
            new MySqlParameter("@c", comment), new MySqlParameter("@rid", rid));
        WriteAudit(rid, "ADMIN_RETURN", st, "RETURNED", "{\"comment\":" + Jstr(comment) + "}");
        return true;
    }

    private bool DoCancel(int rid, string comment)
    {
        string st = RecordStatus(rid);
        if (st == null || st == "CANCELLED") return false;
        ExecuteNonQuery("UPDATE appraisal_records SET status = 'CANCELLED' WHERE record_id = @rid", new MySqlParameter("@rid", rid));
        WriteAudit(rid, "ADMIN_CANCEL", st, "CANCELLED", "{\"comment\":" + Jstr(comment) + "}");
        return true;
    }

    /// <summary>
    /// COMPLETED / HR_REVIEWED -> SUPERVISOR_IN_PROGRESS. Everything that belonged to the
    /// finished review is cleared (HR block, reviewer e-signature, employee acknowledgement),
    /// then scores are recomputed by the one formula.
    /// </summary>
    private bool DoReopen(int rid, string comment)
    {
        string st = RecordStatus(rid);
        if (st != "COMPLETED" && st != "HR_REVIEWED") return false;
        ExecuteNonQuery(
            @"UPDATE appraisal_records
                 SET status = 'SUPERVISOR_IN_PROGRESS',
                     supervisor_submitted_at = NULL,
                     hr_status = NULL, hr_officer_name = NULL, hr_overall_rating = NULL,
                     hr_recommendation = NULL, hr_comments = NULL, hr_submitted_at = NULL,
                     reviewer_signed_at = NULL, reviewer_sign_name = NULL,
                     employee_ack = NULL, employee_ack_comment = NULL, employee_ack_at = NULL
               WHERE record_id = @rid",
            new MySqlParameter("@rid", rid));
        ExecuteNonQuery("CALL appraisal_recalc(@rid)", new MySqlParameter("@rid", rid));
        WriteAudit(rid, "ADMIN_REOPEN", st, "SUPERVISOR_IN_PROGRESS",
            "{\"cleared\":\"hr_*,reviewer_signed_*,employee_ack*\",\"comment\":" + Jstr(comment) + "}");
        return true;
    }

    private string HrOfficerName()
    {
        return (Session["ScreenName"] != null ? Session["ScreenName"]
              : Session["username"]   != null ? Session["username"] : (object)"HR").ToString();
    }

    /// <summary>HR review. Writes ONLY hr_* fields - support_declaration is the employee's.</summary>
    private bool DoHrReview(int rid, int rating, string recommendation, string comments, string officer)
    {
        string st = RecordStatus(rid);
        if (st != "COMPLETED" && st != "HR_REVIEWED") return false;
        ExecuteNonQuery(
            @"UPDATE appraisal_records
              SET status            = 'HR_REVIEWED',
                  hr_status         = 'REVIEWED',
                  hr_officer_name   = @officer,
                  hr_overall_rating = @rating,
                  hr_recommendation = @rec,
                  hr_comments       = @comments,
                  hr_submitted_at   = NOW()
              WHERE record_id = @rid",
            new MySqlParameter("@officer", officer),
            new MySqlParameter("@rating",  rating),
            new MySqlParameter("@rec",     recommendation),
            new MySqlParameter("@comments", comments),
            new MySqlParameter("@rid",     rid));
        WriteAudit(rid, st == "HR_REVIEWED" ? "HR_REVIEW_UPDATED" : "HR_REVIEWED", st, "HR_REVIEWED",
            "{\"rating\":" + rating + ",\"recommendation\":" + Jstr(recommendation) + ",\"comments\":" + Jstr(comments) + "}");
        return true;
    }

    /// <summary>Returns "" on success, otherwise the reason it was refused.</summary>
    private string DoChangeReviewer(int rid, int newReviewerId, string newName)
    {
        DataTable dt = ExecuteQuery(
            "SELECT employee_id, reviewer_id, status FROM appraisal_records WHERE record_id = @rid",
            new MySqlParameter("@rid", rid));
        if (dt.Rows.Count == 0) return "The appraisal was not found.";
        int empId = SafeInt(dt.Rows[0]["employee_id"]);
        int oldRev = SafeInt(dt.Rows[0]["reviewer_id"]);
        string st = SafeStr(dt.Rows[0]["status"]).ToUpper();
        if (empId == newReviewerId) return "An employee cannot be their own supervisor.";
        if (oldRev == newReviewerId) return "That person is already the supervisor.";

        ExecuteNonQuery(
            "UPDATE appraisal_records SET reviewer_id = @newrev WHERE record_id = @rid",
            new MySqlParameter("@newrev", newReviewerId),
            new MySqlParameter("@rid", rid));
        WriteAudit(rid, "REVIEWER_CHANGED", st, st,
            "{\"old_reviewer_id\":" + (oldRev > 0 ? oldRev.ToString() : "null") +
            ",\"new_reviewer_id\":" + newReviewerId + ",\"new_reviewer_name\":" + Jstr(newName) + "}");
        return "";
    }

    private void WriteError(Exception ex)
    {
        System.Diagnostics.Trace.TraceError("AppraisalView: " + ex);
        Response.Write("{\"ok\":false,\"error\":\"The change could not be saved. Try again, or contact MIS if it keeps failing.\"}");
    }

    private void Fail(string message) { Response.Write("{\"ok\":false,\"error\":" + Jstr(message) + "}"); }
    private void Ok(string message, int count) { Response.Write("{\"ok\":true,\"count\":" + count + ",\"message\":" + Jstr(message) + "}"); }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: SINGLE ACTIONS
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxAdminReturn()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            int rid = Int(data, "rid");
            string comment = Str(data, "comment");
            if (string.IsNullOrEmpty(comment)) { Fail("Enter a reason."); return; }
            string st = RecordStatus(rid);
            if (st == null) { Fail("The appraisal was not found."); return; }
            if (!DoReturn(rid, comment)) { Fail("Only a submitted appraisal can be returned. Its status is " + StatusWord(st) + "."); return; }
            Ok("Returned to the employee.", 1);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxAdminCancel()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            int rid = Int(data, "rid");
            string st = RecordStatus(rid);
            if (st == null) { Fail("The appraisal was not found."); return; }
            if (!DoCancel(rid, Str(data, "comment"))) { Fail("The appraisal is already cancelled."); return; }
            Ok("Appraisal cancelled.", 1);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxAdminReopen()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            int rid = Int(data, "rid");
            string st = RecordStatus(rid);
            if (st == null) { Fail("The appraisal was not found."); return; }
            if (!DoReopen(rid, Str(data, "comment"))) { Fail("Only an appraisal awaiting HR or HR reviewed can be reopened."); return; }
            Ok("Reopened for the supervisor.", 1);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxHrInput()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            int rid = Int(data, "rid");
            int rating = Int(data, "rating");
            string recommendation = Str(data, "recommendation").ToUpper();
            string comments = Str(data, "comments");

            if (rating < 1 || rating > 5) { Fail("Choose an HR rating from 1 to 5."); return; }
            if (!IsValidRecommendation(recommendation)) { Fail("Choose an HR recommendation."); return; }

            string st = RecordStatus(rid);
            if (st == null) { Fail("The appraisal was not found."); return; }
            if (!DoHrReview(rid, rating, recommendation, comments, HrOfficerName()))
            { Fail("HR review is possible only after the supervisor has completed the appraisal."); return; }
            Ok("HR review saved.", 1);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: BATCH ACTIONS
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxBatchReturn()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            List<int> rids = RidsFrom(data);
            string comment = Str(data, "comment");
            if (string.IsNullOrEmpty(comment)) { Fail("Enter a reason."); return; }
            int count = 0;
            foreach (int rid in rids) if (DoReturn(rid, comment)) count++;
            Ok(count + " appraisal(s) returned to the employee.", count);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxBatchReopen()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            List<int> rids = RidsFrom(data);
            string comment = Str(data, "comment");
            int count = 0;
            foreach (int rid in rids) if (DoReopen(rid, comment)) count++;
            Ok(count + " appraisal(s) reopened for the supervisor.", count);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxBatchCancel()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            List<int> rids = RidsFrom(data);
            string comment = Str(data, "comment");
            int count = 0;
            foreach (int rid in rids) if (DoCancel(rid, comment)) count++;
            Ok(count + " appraisal(s) cancelled.", count);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxBatchHrInput()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            List<int> rids = RidsFrom(data);
            int rating = Int(data, "rating");
            string recommendation = Str(data, "recommendation").ToUpper();
            string comments = Str(data, "comments");

            if (rating < 1 || rating > 5) { Fail("Choose an HR rating from 1 to 5."); return; }
            if (!IsValidRecommendation(recommendation)) { Fail("Choose an HR recommendation."); return; }

            string officer = HrOfficerName();
            int count = 0;
            foreach (int rid in rids) if (DoHrReview(rid, rating, recommendation, comments, officer)) count++;
            Ok("HR review saved on " + count + " appraisal(s).", count);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: SUPERVISOR
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxGetEmployees()
    {
        try
        {
            DataTable dt = ExecuteQuery(
                @"SELECT empID, emp_name, IFNULL(EMP_CODE,'') AS EMP_CODE
                  FROM hrm_employee
                  WHERE LOWER(IFNULL(employment_status,'')) IN ('active','active employee','')
                     OR employment_status IS NULL
                  ORDER BY emp_name
                  LIMIT 2000");

            StringBuilder sb = new StringBuilder("{\"ok\":true,\"employees\":[");
            bool first = true;
            foreach (DataRow r in dt.Rows)
            {
                if (!first) sb.Append(",");
                sb.AppendFormat("{{\"id\":{0},\"name\":\"{1}\",\"code\":\"{2}\"}}",
                    SafeInt(r["empID"]), EscapeJson(r["emp_name"]), EscapeJson(r["EMP_CODE"]));
                first = false;
            }
            sb.Append("]}");
            Response.Write(sb.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("AppraisalView get_employees: " + ex);
            Response.Write("{\"ok\":false,\"employees\":[],\"error\":\"The staff list could not be loaded.\"}");
        }
    }

    private string ReviewerName(int empId)
    {
        DataTable dtEmp = ExecuteQuery("SELECT emp_name FROM hrm_employee WHERE empID = @eid", new MySqlParameter("@eid", empId));
        return dtEmp.Rows.Count == 0 ? null : SafeStr(dtEmp.Rows[0]["emp_name"]).Trim();
    }

    private void AjaxChangeSupervisor()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            int rid = Int(data, "rid");
            int newReviewerId = Int(data, "new_reviewer_id");
            if (newReviewerId <= 0) { Fail("Choose a supervisor from the list."); return; }
            string newName = ReviewerName(newReviewerId);
            if (newName == null) { Fail("The selected employee was not found."); return; }
            string err = DoChangeReviewer(rid, newReviewerId, newName);
            if (err != "") { Fail(err); return; }
            Ok("Supervisor changed to " + newName + ".", 1);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    /// <summary>Bulk "Assign supervisor" - used by HR to clear records that have no supervisor.</summary>
    private void AjaxBatchChangeSupervisor()
    {
        try
        {
            Dictionary<string, object> data = ReadJsonBody();
            List<int> rids = RidsFrom(data);
            int newReviewerId = Int(data, "new_reviewer_id");
            if (newReviewerId <= 0) { Fail("Choose a supervisor from the list."); return; }
            if (rids.Count == 0) { Fail("No appraisals are selected."); return; }
            string newName = ReviewerName(newReviewerId);
            if (newName == null) { Fail("The selected employee was not found."); return; }

            int count = 0, selfSkipped = 0, otherSkipped = 0;
            foreach (int rid in rids)
            {
                string err = DoChangeReviewer(rid, newReviewerId, newName);
                if (err == "") count++;
                else if (err.StartsWith("An employee cannot")) selfSkipped++;
                else otherSkipped++;
            }
            string msg = count + " appraisal(s) assigned to " + newName + ".";
            if (selfSkipped > 0) msg += " " + selfSkipped + " skipped because the person cannot supervise their own appraisal.";
            if (otherSkipped > 0) msg += " " + otherSkipped + " skipped because the supervisor was already assigned.";
            Ok(msg, count);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LIST
    // ═══════════════════════════════════════════════════════════════════
    private void LoadSessionFilter()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT session_id, session_title, status
              FROM appraisal_sessions
              ORDER BY FIELD(status,'ACTIVE','DRAFT','CLOSED','ARCHIVED'), created_at DESC");

        StringBuilder sb = new StringBuilder();
        sb.Append("<option value='0'>All sessions</option>");
        foreach (DataRow r in dt.Rows)
        {
            int sid = Convert.ToInt32(r["session_id"]);
            sb.AppendFormat("<option value='{0}'{1}>{2} ({3})</option>",
                sid, sid == QsSession ? " selected" : "",
                Enc(SafeStr(r["session_title"])), SessionStatusWord(SafeStr(r["status"])));
        }
        litSessionOptions.Text = sb.ToString();
    }

    private string KpiLink(string extra)
    {
        List<string> q = new List<string>();
        if (QsSession > 0) q.Add("sid=" + QsSession);
        if (!string.IsNullOrEmpty(extra)) q.Add(extra);
        return "AppraisalView.aspx" + (q.Count > 0 ? "?" + string.Join("&amp;", q.ToArray()) : "");
    }

    private void LoadListStats()
    {
        try
        {
            string sw = QsSession > 0 ? " WHERE session_id = " + QsSession : "";
            DataTable dt = ExecuteQuery(
                @"SELECT SUM(status = 'PENDING') AS pending,
                         SUM(status = 'EMPLOYEE_IN_PROGRESS') AS inprog,
                         SUM(status IN ('EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS')) AS sup,
                         SUM(status = 'COMPLETED') AS awaiting,
                         SUM(status = 'HR_REVIEWED') AS hrdone
                  FROM appraisal_records" + sw);
            DataRow r = dt.Rows[0];

            DataTable dtU = ExecuteQuery(
                @"SELECT COUNT(*) AS n FROM appraisal_records
                  WHERE (reviewer_id IS NULL OR reviewer_id = 0)
                    AND status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED')" +
                (QsSession > 0 ? " AND session_id = " + QsSession
                               : " AND session_id IN (SELECT session_id FROM appraisal_sessions WHERE status = 'ACTIVE')"));
            int unassigned = SafeInt(dtU.Rows[0]["n"]);

            StringBuilder sb = new StringBuilder("<div class='hr-kpis'>");
            Kpi(sb, "Not started", SafeInt(r["pending"]), KpiLink("status=PENDING"), false);
            Kpi(sb, "In progress", SafeInt(r["inprog"]), KpiLink("status=EMPLOYEE_IN_PROGRESS"), false);
            Kpi(sb, "With supervisor", SafeInt(r["sup"]), KpiLink("status=SUPERVISOR_STAGE"), false);
            Kpi(sb, "Awaiting HR", SafeInt(r["awaiting"]), KpiLink("status=COMPLETED"), false);
            Kpi(sb, "HR reviewed", SafeInt(r["hrdone"]), KpiLink("status=HR_REVIEWED"), false);
            Kpi(sb, "No supervisor", unassigned, KpiLink("rev=none"), unassigned > 0);
            sb.Append("</div>");
            litListStats.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("AppraisalView stats: " + ex);
            litListStats.Text = "";
        }
    }

    private static void Kpi(StringBuilder sb, string label, int value, string href, bool alert)
    {
        sb.AppendFormat("<a class='hr-kpi{0}' href='{1}'><div class='hr-kpi__label'>{2}</div><div class='hr-kpi__value'>{3}</div></a>",
            alert ? " hr-kpi--alert" : "", href, label, value.ToString("N0"));
    }

    private void BindGrid()
    {
        StringBuilder where = new StringBuilder("WHERE 1=1");
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string deptExpr = GetDepartmentSelectExpression("e");

        if (!string.IsNullOrEmpty(QsSearch))
        {
            where.Append(" AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }
        if (QsStatus == "SUPERVISOR_STAGE")
            where.Append(" AND ar.status IN ('EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS')");
        else if (!string.IsNullOrEmpty(QsStatus))
        {
            where.Append(" AND ar.status = @st");
            parms.Add(new MySqlParameter("@st", QsStatus));
        }
        if (!string.IsNullOrEmpty(QsCategory))
        {
            where.Append(" AND ar.staff_category = @cat");
            parms.Add(new MySqlParameter("@cat", QsCategory));
        }
        if (QsSession > 0)
        {
            where.Append(" AND ar.session_id = @sid");
            parms.Add(new MySqlParameter("@sid", QsSession));
        }
        if (QsReviewer == "none")
            where.Append(" AND (ar.reviewer_id IS NULL OR ar.reviewer_id = 0)");

        bool canAdmin = CanAdmin;

        DataTable dtCount = ExecuteQuery("SELECT COUNT(*) FROM appraisal_records ar LEFT JOIN hrm_employee e ON e.empID = ar.employee_id " + where, parms.ToArray());
        int totalRecords = Convert.ToInt32(dtCount.Rows[0][0]);

        int pageSize = QsPageSize;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRecords / pageSize));
        int currentPage = Math.Min(QsPage, totalPages);
        int offset = (currentPage - 1) * pageSize;

        string dataSql = string.Format(
            @"SELECT ar.record_id, ar.status, ar.staff_category,
                     ar.final_percentage, ar.classification,
                     ar.reviewer_id, ar.employee_ack, ar.employee_ack_comment,
                     IFNULL(e.emp_name, CONCAT('Staff record not found (ID ', ar.employee_id, ')')) AS emp_name, e.EMP_CODE, {0} AS department,
                     rev.emp_name AS reviewer_name,
                     s.session_title
              FROM appraisal_records ar
              LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              {1}
              ORDER BY ar.updated_at DESC
              LIMIT {2} OFFSET {3}",
            deptExpr, where, pageSize, offset);

        DataTable dtData = ExecuteQuery(dataSql, CloneParams(parms));

        StringBuilder html = new StringBuilder();
        if (dtData.Rows.Count == 0)
        {
            html.Append("<tr><td colspan='10' class='hr-empty'>No appraisals match the filters.</td></tr>");
        }
        else
        {
            foreach (DataRow r in dtData.Rows)
            {
                int recId = Convert.ToInt32(r["record_id"]);
                string st = SafeStr(r["status"]).ToUpper();
                string empName = SafeStr(r["emp_name"]);
                string dept = SafeStr(r["department"]);
                string session = SafeStr(r["session_title"]);
                string pct = SafeDecStr(r["final_percentage"]);
                string cls = ClassWord(Classify(r["final_percentage"], SafeStr(r["classification"])));
                bool noReviewer = SafeInt(r["reviewer_id"]) <= 0;
                string reviewerName = noReviewer ? "Not assigned" : SafeStr(r["reviewer_name"]);

                html.AppendFormat(
                    "<tr data-rid='{0}' data-status='{1}' data-empname='{2}' data-dept='{3}' data-session='{4}' data-pct='{5}' data-cls='{6}' data-reviewer='{7}' data-ack='{8}'>",
                    recId, Attr(st), Attr(empName), Attr(dept), Attr(session), Attr(pct), Attr(cls), Attr(reviewerName),
                    Attr(AckLine(SafeStr(r["employee_ack"]), SafeStr(r["employee_ack_comment"]))));
                html.AppendFormat("<td class='pa-chk'><input type='checkbox' class='pa-row-chk' value='{0}' onclick='onRowCheck(this)'></td>", recId);
                html.AppendFormat("<td><a href='AppraisalView.aspx?rid={0}'>{1}</a><span class='hr-sub'>{2}</span></td>",
                    recId, Enc(empName), Enc(SafeStr(r["EMP_CODE"])));
                html.AppendFormat("<td>{0}</td>", Enc(dept));
                html.AppendFormat("<td>{0}</td>", CategoryWord(SafeStr(r["staff_category"])));
                html.AppendFormat("<td>{0}</td>", Enc(session));
                html.AppendFormat("<td>{0}</td>", noReviewer ? "<span class='hr-badge hr-badge--warn'>Not assigned</span>" : Enc(reviewerName));
                html.AppendFormat("<td>{0}</td>", StatusBadge(st));
                html.AppendFormat("<td class='hr-num'>{0}</td>", pct);
                html.AppendFormat("<td>{0}</td>", ClassBadge(cls));
                html.Append("<td class='hr-right' style='white-space:nowrap;'>");
                if (canAdmin && st == "COMPLETED")
                    html.Append("<button type='button' class='hr-btn hr-btn--primary hr-btn--sm' onclick='openHrWizardFromRowBtn(this)'>HR review</button> ");
                html.AppendFormat("<a class='hr-btn hr-btn--secondary hr-btn--sm' href='AppraisalView.aspx?rid={0}'>Open</a>", recId);
                html.Append("</td></tr>");
            }
        }
        litGridBody.Text = html.ToString() + (canAdmin ? "" : "<script type='text/javascript'>window.PA_READ_ONLY=true;</script>");

        litTotalCount.Text = totalRecords.ToString("N0");
        litPagerInfo.Text = totalRecords == 0 ? "No records"
            : string.Format("Showing {0} to {1} of {2}", (offset + 1).ToString("N0"),
                Math.Min(offset + pageSize, totalRecords).ToString("N0"), totalRecords.ToString("N0"));
        BuildPager(currentPage, totalPages);
    }

    private void BuildPager(int current, int totalPages)
    {
        if (totalPages <= 1) { litPager.Text = ""; return; }
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("<button type='button' onclick='goPage({0})'{1}>Previous</button>", current - 1, current > 1 ? "" : " disabled");
        int startPage = Math.Max(1, current - 2);
        int endPage = Math.Min(totalPages, current + 2);
        for (int i = startPage; i <= endPage; i++)
            sb.AppendFormat("<button type='button' onclick='goPage({0})'{1}>{0}</button>", i,
                i == current ? " disabled style='background:var(--hr-navy);color:#fff;border-color:var(--hr-navy);opacity:1;'" : "");
        sb.AppendFormat("<button type='button' onclick='goPage({0})'{1}>Next</button>", current + 1, current < totalPages ? "" : " disabled");
        litPager.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DETAIL
    // ═══════════════════════════════════════════════════════════════════
    private void LoadRecordDetail(int rid)
    {
        string deptExpr = GetDepartmentSelectExpression("e");
        string empDesignationExpr = GetDesignationSelectExpression("e");
        string reviewerDesignationExpr = GetDesignationSelectExpression("rev");

        DataTable dtRec = ExecuteQuery(string.Format(
            @"SELECT ar.*,
                     IFNULL(e.emp_name, CONCAT('Staff record not found (ID ', ar.employee_id, ')')) AS emp_name, e.EMP_CODE, e.EmpType, {0} AS department, {1} AS designation,
                     rev.emp_name AS live_reviewer_name,
                     {2} AS reviewer_designation,
                     s.session_title, s.period_start, s.period_end, s.deadline
              FROM appraisal_records ar
              LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE ar.record_id = @rid", deptExpr, empDesignationExpr, reviewerDesignationExpr),
            new MySqlParameter("@rid", rid));

        litCrumb.Text = "<a class='hr-crumb' href='AppraisalView.aspx'>Appraisals</a>";
        if (dtRec.Rows.Count == 0)
        {
            litHeaderTitle.Text = "Appraisal not found";
            litHeaderSub.Text = "";
            litDetailContent.Text = "<div class='hr-card'><div class='hr-empty'>This appraisal record does not exist. <a href='AppraisalView.aspx'>Back to appraisals</a></div></div>";
            return;
        }

        DataRow rec = dtRec.Rows[0];
        string status = SafeStr(rec["status"]).ToUpper();
        string cat = SafeStr(rec["staff_category"]).ToUpper();
        bool canAdmin = CanAdmin;

        litHeaderTitle.Text = Enc(SafeStr(rec["emp_name"]));
        litHeaderSub.Text = Enc(SafeStr(rec["session_title"]));
        litHeaderActions.Text =
            "<a class='hr-btn hr-btn--inverse' href='AppraisalPrint.aspx?rid=" + rid + "' target='_blank' rel='noopener'>Print</a>" +
            "<a class='hr-btn hr-btn--inverse' href='AppraisalView.aspx'>Back to appraisals</a>";

        DataTable dtB = ExecuteQuery(
            @"SELECT slot_number, IFNULL(is_catalogue,0) AS is_catalogue,
                     agreed_output, performance_indicators, result_areas, evidence_text,
                     IFNULL(is_na,0) AS is_na, na_reason,
                     self_rating, supervisor_rating, comments
              FROM appraisal_section_b WHERE record_id = @rid ORDER BY slot_number",
            new MySqlParameter("@rid", rid));
        DataTable dtEv = ExecuteQuery(
            @"SELECT evidence_id, slot_number, original_name, size_bytes
              FROM appraisal_evidence WHERE record_id = @rid ORDER BY slot_number, evidence_id",
            new MySqlParameter("@rid", rid));
        DataTable dtC = ExecuteQuery(
            @"SELECT competency_code, competency_name, category_name, rating, is_na, comment,
                     IFNULL(self_rating,0) AS self_rating, IFNULL(supervisor_comment,'') AS supervisor_comment
              FROM appraisal_section_c WHERE record_id = @rid ORDER BY entry_id",
            new MySqlParameter("@rid", rid));
        DataTable dtD = ExecuteQuery(
            "SELECT performance_gap, agreed_action, time_frame FROM appraisal_section_d WHERE record_id = @rid ORDER BY entry_id",
            new MySqlParameter("@rid", rid));
        DataTable dtE = ExecuteQuery(
            "SELECT question_number, question_text, response FROM appraisal_section_e WHERE record_id = @rid ORDER BY question_number",
            new MySqlParameter("@rid", rid));

        string liveReviewer = SafeStr(rec["live_reviewer_name"]);
        string position   = FirstNonEmpty(SafeStr(rec["snap_position"]), SafeStr(rec["designation"]));
        string department = FirstNonEmpty(SafeStr(rec["snap_department"]), SafeStr(rec["department"]));
        string reportsTo  = FirstNonEmpty(SafeStr(rec["snap_reports_to"]), liveReviewer);
        string revName    = FirstNonEmpty(SafeStr(rec["reviewer_name"]), liveReviewer);
        string revTitle   = FirstNonEmpty(SafeStr(rec["reviewer_title"]), SafeStr(rec["reviewer_designation"]));
        object reviewDate = rec["reviewer_signed_at"] != DBNull.Value ? rec["reviewer_signed_at"] : rec["supervisor_submitted_at"];
        object submitDate = rec["employee_signed_at"] != DBNull.Value ? rec["employee_signed_at"] : rec["employee_submitted_at"];
        string finalPct   = SafeDecStr(rec["final_percentage"]);
        string classif    = ClassWord(Classify(rec["final_percentage"], SafeStr(rec["classification"])));
        string ack        = SafeStr(rec["employee_ack"]).ToUpper();
        string ackComment = SafeStr(rec["employee_ack_comment"]);

        StringBuilder html = new StringBuilder();

        // ── Summary card ──
        html.Append("<div class='hr-card'><div class='hr-card__head'>");
        html.AppendFormat("<div class='hr-card__title'>{0} <span class='hr-muted' style='font-weight:400;'>{1}</span></div>",
            Enc(SafeStr(rec["emp_name"])), Enc(SafeStr(rec["EMP_CODE"])));
        html.Append(StatusBadge(status));
        html.Append("</div><div class='hr-card__body'>");
        if (status != "CANCELLED") html.Append(Stages(status));
        html.Append("<dl class='hr-dl' style='margin-top:16px;'>");
        Dl(html, "Session", Enc(SafeStr(rec["session_title"])));
        Dl(html, "Appraisal period", D(rec["period_start"]) + " to " + D(rec["period_end"]));
        Dl(html, "Deadline", D(rec["deadline"]));
        Dl(html, "Supervisor", liveReviewer != "" ? Enc(liveReviewer) : "<span class='hr-badge hr-badge--warn'>Not assigned</span>");
        Dl(html, "Score %", finalPct != "" ? finalPct : "<span class='hr-muted'>Not scored</span>");
        Dl(html, "Classification", ClassBadge(classif));
        html.Append("</dl></div></div>");

        if (SafeInt(rec["reviewer_id"]) <= 0 && status != "CANCELLED")
            html.Append("<div class='hr-notice hr-notice--warn'>No supervisor is assigned. Assign one so the appraisal can be rated.</div>");

        // ── Actions ──
        if (canAdmin)
        {
            html.Append("<div class='pa-actions'>");
            if (status == "COMPLETED")
                html.Append("<button type='button' class='hr-btn hr-btn--primary' onclick='openHrWizardFromData()'>HR review</button>");
            if (status == "HR_REVIEWED")
                html.Append("<button type='button' class='hr-btn hr-btn--secondary' onclick='openHrWizardFromData()'>Update HR review</button>");
            html.AppendFormat("<button type='button' class='hr-btn hr-btn--secondary' data-rid='{0}' data-reviewer='{1}' onclick='changeSupervisorFromDetailBtn(this)'>Change supervisor</button>",
                rid, Attr(liveReviewer != "" ? liveReviewer : "Not assigned"));
            if (status == "EMPLOYEE_SUBMITTED" || status == "SUPERVISOR_IN_PROGRESS")
                html.AppendFormat("<button type='button' class='hr-btn hr-btn--secondary' onclick='adminReturnToEmployee({0})'>Return to employee</button>", rid);
            if (status == "COMPLETED" || status == "HR_REVIEWED")
                html.AppendFormat("<button type='button' class='hr-btn hr-btn--secondary' onclick='adminReopen({0})'>Reopen for supervisor</button>", rid);
            if (status != "CANCELLED")
                html.AppendFormat("<span class='hr-spacer'></span><button type='button' class='hr-btn hr-btn--danger' onclick='adminCancel({0})'>Cancel appraisal</button>", rid);
            html.Append("</div>");
        }

        // ── Section A ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Section A: Personal details</div></div><div class='hr-card__body'><dl class='hr-dl'>");
        Dl(html, "Name", Enc(SafeStr(rec["emp_name"])));
        Dl(html, "Staff number", Enc(SafeStr(rec["EMP_CODE"])));
        Dl(html, "Staff category", CategoryWord(cat));
        Dl(html, "Position held", OrNot(position));
        Dl(html, "Department", OrNot(department));
        Dl(html, "Reports to", OrNot(reportsTo));
        Dl(html, "Supervisor", OrNot(revName));
        Dl(html, "Supervisor's title", OrNot(revTitle));
        Dl(html, "Date submitted", submitDate == DBNull.Value ? "<span class='hr-muted'>Not submitted</span>" : D(submitDate));
        Dl(html, "Date of review", reviewDate == DBNull.Value ? "<span class='hr-muted'>Not reviewed</span>" : D(reviewDate));
        html.Append("</dl></div></div>");

        // ── Section B ──
        Dictionary<int, List<DataRow>> evBySlot = new Dictionary<int, List<DataRow>>();
        foreach (DataRow ev in dtEv.Rows)
        {
            int slot = SafeInt(ev["slot_number"]);
            if (!evBySlot.ContainsKey(slot)) evBySlot[slot] = new List<DataRow>();
            evBySlot[slot].Add(ev);
        }

        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Section B: Achievements of responsibilities and key performance areas</div>");
        html.AppendFormat("<div class='hr-card__meta'>Rating scale: {0}</div></div>", RatingLegend(cat));
        if (dtB.Rows.Count == 0)
        {
            html.Append("<div class='hr-empty'>No responsibilities recorded.</div>");
        }
        else
        {
            int bApplicable = 0, n = 0;
            html.Append("<div class='hr-table-wrap'><table class='hr-table pa-table-text'><thead><tr>");
            html.Append("<th class='hr-num'>No.</th><th>Responsibility or key performance area</th><th>Expected standard</th><th>Achievement</th><th>Evidence</th>");
            html.Append("<th>Employee rating</th><th>Supervisor rating</th><th>Supervisor comment</th></tr></thead><tbody>");
            foreach (DataRow b in dtB.Rows)
            {
                n++;
                int slot = SafeInt(b["slot_number"]);
                bool isNa = SafeInt(b["is_na"]) == 1;
                string kpa = SafeStr(b["agreed_output"]);
                if (!isNa && kpa.Trim() != "") bApplicable++;

                html.AppendFormat("<tr{0}>", isNa ? " class='pa-na'" : "");
                html.AppendFormat("<td class='hr-num'>{0}</td>", n);
                html.AppendFormat("<td>{0}</td>", Enc(kpa));
                html.AppendFormat("<td>{0}</td>", MultiLine(SafeStr(b["performance_indicators"])));
                if (isNa)
                {
                    string reason = SafeStr(b["na_reason"]).Trim();
                    html.AppendFormat("<td colspan='2'>Not applicable{0}</td>", reason != "" ? ": " + Enc(reason) : "");
                    html.Append("<td>Not applicable</td><td>Not applicable</td>");
                }
                else
                {
                    html.AppendFormat("<td>{0}</td>", MultiLine(SafeStr(b["result_areas"])));
                    html.Append("<td>");
                    string evText = SafeStr(b["evidence_text"]);
                    if (evText != "") html.Append(MultiLine(evText));
                    if (evBySlot.ContainsKey(slot))
                    {
                        html.Append("<div class='pa-files'>");
                        foreach (DataRow ev in evBySlot[slot])
                            html.AppendFormat("<a href='AppraisalView.aspx?ajax=evidence&amp;id={0}' target='_blank' rel='noopener'>{1} ({2})</a>",
                                SafeInt(ev["evidence_id"]), Enc(SafeStr(ev["original_name"])), FormatBytes(SafeInt(ev["size_bytes"])));
                        html.Append("</div>");
                    }
                    html.Append("</td>");
                    html.AppendFormat("<td class='pa-rating'>{0}</td>", Rating(b["self_rating"], cat));
                    html.AppendFormat("<td class='pa-rating'>{0}</td>", Rating(b["supervisor_rating"], cat));
                }
                html.AppendFormat("<td>{0}</td>", MultiLine(SafeStr(b["comments"])));
                html.Append("</tr>");
            }
            html.Append("</tbody></table></div>");
            html.AppendFormat("<div class='hr-card__foot'><span>Employee total {0}</span><span>Supervisor total {1} of {2}</span></div>",
                OrBlank(SafeDecStr(rec["section_b_self_total"])), OrBlank(SafeDecStr(rec["section_b_supervisor_total"])), bApplicable * 5);
        }
        html.Append("</div>");

        // ── Section C ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Section C: Competencies</div>");
        html.AppendFormat("<div class='hr-card__meta'>Rating scale: {0}</div></div>", RatingLegend(cat));
        if (dtC.Rows.Count == 0)
        {
            html.Append("<div class='hr-empty'>No competency ratings recorded.</div>");
        }
        else
        {
            string lastCat = "";
            int cApplicable = 0;
            html.Append("<div class='hr-table-wrap'><table class='hr-table pa-table-text'><thead><tr>");
            html.Append("<th>Code</th><th>Competency</th><th>Employee rating</th><th>Supervisor rating</th><th>Employee comment</th><th>Supervisor comment</th>");
            html.Append("</tr></thead><tbody>");
            foreach (DataRow c in dtC.Rows)
            {
                string group = SafeStr(c["category_name"]);
                if (group != lastCat)
                {
                    html.AppendFormat("<tr class='pa-group'><td colspan='6'>{0}</td></tr>", Enc(group));
                    lastCat = group;
                }
                bool isNA = SafeInt(c["is_na"]) == 1;
                if (!isNA) cApplicable++;
                html.AppendFormat("<tr{0}>", isNA ? " class='pa-na'" : "");
                html.AppendFormat("<td>{0}</td>", Enc(SafeStr(c["competency_code"])));
                html.AppendFormat("<td>{0}</td>", Enc(SafeStr(c["competency_name"])));
                html.AppendFormat("<td class='pa-rating'>{0}</td>", isNA ? "Not applicable" : Rating(c["self_rating"], cat));
                html.AppendFormat("<td class='pa-rating'>{0}</td>", isNA ? "Not applicable" : Rating(c["rating"], cat));
                html.AppendFormat("<td>{0}</td>", MultiLine(SafeStr(c["comment"])));
                html.AppendFormat("<td>{0}</td>", MultiLine(SafeStr(c["supervisor_comment"])));
                html.Append("</tr>");
            }
            html.Append("</tbody></table></div>");
            html.AppendFormat("<div class='hr-card__foot'><span>{0} applicable competencies</span><span>Supervisor total {1} of {2}</span></div>",
                cApplicable, OrBlank(SafeDecStr(rec["section_c_total"])), cApplicable * 5);
        }
        html.Append("</div>");

        // ── Section D ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Section D: Training and development plan</div></div>");
        if (dtD.Rows.Count == 0)
        {
            html.Append("<div class='hr-empty'>No training needs recorded.</div>");
        }
        else
        {
            html.Append("<div class='hr-table-wrap'><table class='hr-table pa-table-text'><thead><tr><th class='hr-num'>No.</th><th>Performance gap or training need</th><th>Agreed action</th><th>Time frame</th></tr></thead><tbody>");
            int i = 0;
            foreach (DataRow d in dtD.Rows)
            {
                i++;
                html.AppendFormat("<tr><td class='hr-num'>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>", i,
                    MultiLine(SafeStr(d["performance_gap"])), MultiLine(SafeStr(d["agreed_action"])), Enc(SafeStr(d["time_frame"])));
            }
            html.Append("</tbody></table></div>");
        }
        html.Append("</div>");

        // ── Section E ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Section E: Comments</div></div><div class='hr-card__body'>");
        if (dtE.Rows.Count == 0)
        {
            html.Append("<div class='hr-muted'>No responses recorded.</div>");
        }
        else
        {
            foreach (DataRow qe in dtE.Rows)
            {
                string resp = SafeStr(qe["response"]).Trim();
                html.AppendFormat("<div class='pa-q'>{0}. {1}</div>", SafeInt(qe["question_number"]), Enc(SafeStr(qe["question_text"])));
                html.AppendFormat("<div class='pa-a'>{0}</div>", resp != "" ? Enc(resp) : "<span class='hr-muted'>No response</span>");
            }
        }
        html.Append("</div></div>");

        // ── Score ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Score</div></div><div class='hr-card__body'><dl class='hr-dl'>");
        Dl(html, "Section B employee total", OrBlank(SafeDecStr(rec["section_b_self_total"])));
        Dl(html, "Section B supervisor total", OrBlank(SafeDecStr(rec["section_b_supervisor_total"])));
        Dl(html, "Section C total", OrBlank(SafeDecStr(rec["section_c_total"])));
        Dl(html, "Raw score", OrBlank(SafeDecStr(rec["raw_score"])));
        Dl(html, "Maximum", OrBlank(SafeDecStr(rec["max_possible"])));
        Dl(html, "Score %", finalPct != "" ? "<strong>" + finalPct + "</strong>" : "<span class='hr-muted'>Not scored</span>");
        Dl(html, "Classification", ClassBadge(classif));
        html.Append("</dl></div></div>");

        // ── Sign-off ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>Sign-off</div></div><div class='hr-card__body'><dl class='hr-dl'>");
        string empSign = SafeStr(rec["employee_sign_name"]);
        string revSign = SafeStr(rec["reviewer_sign_name"]);
        Dl(html, "Employee", empSign != ""
            ? Enc(empSign) + "<span class='hr-sub'>Signed electronically " + DT(rec["employee_signed_at"]) + "</span>"
            : (rec["employee_submitted_at"] != DBNull.Value ? "Submitted " + DT(rec["employee_submitted_at"]) : "<span class='hr-muted'>Not signed</span>"));
        Dl(html, "Supervisor", revSign != ""
            ? Enc(revSign) + "<span class='hr-sub'>Signed electronically " + DT(rec["reviewer_signed_at"]) + "</span>"
            : (rec["supervisor_submitted_at"] != DBNull.Value ? "Completed " + DT(rec["supervisor_submitted_at"]) : "<span class='hr-muted'>Not signed</span>"));
        Dl(html, "Employee acknowledgement", (ack == "AGREE" || ack == "DISAGREE")
            ? (ack == "AGREE" ? "Agrees with the rating" : "Disagrees with the rating") + "<span class='hr-sub'>" + DT(rec["employee_ack_at"]) + "</span>"
            : "<span class='hr-muted'>Not recorded</span>");
        if (ackComment.Trim() != "")
            html.AppendFormat("<div style='grid-column:1/-1;'><dt>Employee comment</dt><dd>{0}</dd></div>", MultiLine(ackComment));
        html.Append("</dl></div></div>");

        // ── HR review ──
        html.Append("<div class='hr-card'><div class='hr-card__head'><div class='hr-card__title'>HR review</div></div><div class='hr-card__body'>");
        bool hasHr = SafeStr(rec["hr_status"]) == "REVIEWED";
        if (hasHr)
        {
            int hrRating = SafeInt(rec["hr_overall_rating"]);
            html.Append("<dl class='hr-dl'>");
            Dl(html, "HR officer", OrNot(SafeStr(rec["hr_officer_name"])));
            Dl(html, "Date", DT(rec["hr_submitted_at"]));
            Dl(html, "HR rating", hrRating > 0 ? Rating((object)hrRating, cat) : "<span class='hr-muted'>Not recorded</span>");
            Dl(html, "HR recommendation", OrNot(RecommendationWord(SafeStr(rec["hr_recommendation"]))));
            string hrComm = SafeStr(rec["hr_comments"]);
            if (hrComm.Trim() != "")
                html.AppendFormat("<div style='grid-column:1/-1;'><dt>HR comments</dt><dd>{0}</dd></div>", MultiLine(hrComm));
            html.Append("</dl>");
        }
        else
        {
            html.Append("<div class='hr-muted'>Not yet reviewed by HR.</div>");
        }
        html.Append("</div></div>");

        // Data for the HR review dialog
        html.Append("<script type='text/javascript'>window.PA_DETAIL={");
        html.AppendFormat("rid:{0},", rid);
        html.AppendFormat("empName:\"{0}\",", EscapeJs(SafeStr(rec["emp_name"])));
        html.AppendFormat("session:\"{0}\",", EscapeJs(SafeStr(rec["session_title"])));
        html.AppendFormat("pct:\"{0}\",", EscapeJs(finalPct));
        html.AppendFormat("cls:\"{0}\",", EscapeJs(classif));
        html.AppendFormat("ack:\"{0}\",", EscapeJs(AckLine(ack, ackComment)));
        html.AppendFormat("rating:\"{0}\",", hasHr ? SafeInt(rec["hr_overall_rating"]).ToString() : "");
        html.AppendFormat("rec:\"{0}\",", hasHr ? EscapeJs(SafeStr(rec["hr_recommendation"])) : "");
        html.AppendFormat("comments:\"{0}\"", hasHr ? EscapeJs(SafeStr(rec["hr_comments"])) : "");
        html.Append("};</script>");

        litDetailContent.Text = html.ToString();
    }

    private static string Stages(string st)
    {
        int now; bool all = false;
        switch (st)
        {
            case "EMPLOYEE_SUBMITTED":
            case "SUPERVISOR_IN_PROGRESS": now = 1; break;
            case "COMPLETED": now = 2; break;
            case "HR_REVIEWED": now = 3; all = true; break;
            default: now = 0; break;
        }
        string[] names = { "Employee", "Supervisor", "HR" };
        StringBuilder sb = new StringBuilder("<ul class='hr-stages'>");
        for (int i = 0; i < names.Length; i++)
        {
            string cls = (all || i < now) ? "is-done" : (i == now ? "is-now" : "");
            sb.AppendFormat("<li class='{0}'><span></span>{1}</li>", cls, names[i]);
        }
        return sb.Append("</ul>").ToString();
    }

    private static void Dl(StringBuilder sb, string label, string valueHtml)
    {
        sb.AppendFormat("<div><dt>{0}</dt><dd>{1}</dd></div>", HttpUtility.HtmlEncode(label), valueHtml);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VOCABULARY (same words on every appraisal screen, export and print)
    // ═══════════════════════════════════════════════════════════════════
    internal static string StatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "PENDING":                return "Not started";
            case "EMPLOYEE_IN_PROGRESS":   return "In progress";
            case "EMPLOYEE_SUBMITTED":     return "Submitted";
            case "SUPERVISOR_IN_PROGRESS": return "With supervisor";
            case "RETURNED":               return "Returned";
            case "COMPLETED":              return "Awaiting HR";
            case "HR_REVIEWED":            return "HR reviewed";
            case "CANCELLED":              return "Cancelled";
            default:                       return s ?? "";
        }
    }

    private static string StatusBadge(string s)
    {
        string kind;
        switch ((s ?? "").ToUpperInvariant())
        {
            case "EMPLOYEE_IN_PROGRESS":
            case "EMPLOYEE_SUBMITTED":
            case "SUPERVISOR_IN_PROGRESS": kind = "info"; break;
            case "RETURNED":
            case "COMPLETED":              kind = "warn"; break;
            case "HR_REVIEWED":            kind = "ok"; break;
            default:                       kind = "neutral"; break;
        }
        return "<span class='hr-badge hr-badge--" + kind + "'>" + StatusWord(s) + "</span>";
    }

    private static string SessionStatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "ACTIVE": return "Active";
            case "DRAFT": return "Draft";
            case "CLOSED": return "Closed";
            case "ARCHIVED": return "Archived";
            default: return s;
        }
    }

    /// <summary>The one overall scale, mirrors SQL appraisal_classify(): 90, 75, 60, 50.</summary>
    private static string Classify(object pct, string fallback)
    {
        if (pct == null || pct == DBNull.Value) return fallback ?? "";
        decimal d;
        if (!decimal.TryParse(pct.ToString(), out d)) return fallback ?? "";
        if (d >= 90) return "Exceptional";
        if (d >= 75) return "Above expectations";
        if (d >= 60) return "Satisfactory";
        if (d >= 50) return "Development needed";
        return "Unsatisfactory";
    }

    private static string ClassWord(string c)
    {
        string k = (c ?? "").Trim().ToLowerInvariant();
        if (k == "") return "";
        if (k.StartsWith("exceptional")) return "Exceptional";
        if (k.StartsWith("above")) return "Above expectations";
        if (k.StartsWith("satisf")) return "Satisfactory";
        if (k.StartsWith("develop") || k.StartsWith("needs")) return "Development needed";
        if (k.StartsWith("unsatisf") || k.StartsWith("poor")) return "Unsatisfactory";
        return c;
    }

    private static string ClassBadge(string word)
    {
        if (string.IsNullOrEmpty(word)) return "<span class='hr-muted'>Not scored</span>";
        string kind = "neutral";
        switch (word)
        {
            case "Exceptional":
            case "Above expectations": kind = "ok"; break;
            case "Satisfactory": kind = "info"; break;
            case "Development needed": kind = "warn"; break;
            case "Unsatisfactory": kind = "bad"; break;
        }
        return "<span class='hr-badge hr-badge--" + kind + "'>" + HttpUtility.HtmlEncode(word) + "</span>";
    }

    private static string CategoryWord(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "ACADEMIC": return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "SUPPORT": return "Support";
            default: return HttpUtility.HtmlEncode(c ?? "");
        }
    }

    private static string RatingLabel(int rating, string cat)
    {
        string[] acad  = { "", "Unsatisfactory", "Development needed", "Satisfactory", "Above expectations", "Exceptional" };
        string[] other = { "", "Poor", "Fair", "Good", "Very good", "Excellent" };
        if (rating < 1 || rating > 5) return "";
        return (cat ?? "").ToUpperInvariant() == "ACADEMIC" ? acad[rating] : other[rating];
    }

    private static string RatingLegend(string cat)
    {
        return (cat ?? "").ToUpperInvariant() == "ACADEMIC"
            ? "5 Exceptional, 4 Above expectations, 3 Satisfactory, 2 Development needed, 1 Unsatisfactory"
            : "5 Excellent, 4 Very good, 3 Good, 2 Fair, 1 Poor";
    }

    private static string Rating(object val, string cat)
    {
        int r;
        if (val == null || val == DBNull.Value || !int.TryParse(val.ToString(), out r) || r <= 0)
            return "<span class='hr-muted'>Not rated</span>";
        return r + " <span class='hr-muted'>" + RatingLabel(r, cat) + "</span>";
    }

    private static string RecommendationWord(string rec)
    {
        switch ((rec ?? "").ToUpperInvariant())
        {
            case "CONFIRM":          return "Confirm appointment";
            case "EXTEND_PROBATION": return "Extend probation";
            case "PIP":              return "Performance improvement plan";
            case "PROMOTE":          return "Promote";
            case "OTHER":            return "Other";
            default:                 return rec ?? "";
        }
    }

    private static string AckLine(string ack, string comment)
    {
        string a = (ack ?? "").ToUpperInvariant();
        if (a != "AGREE" && a != "DISAGREE") return "Not recorded";
        return (a == "AGREE" ? "Agrees" : "Disagrees") + (string.IsNullOrEmpty(comment) ? "" : ": " + comment);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string FirstNonEmpty(string a, string b)
    {
        return !string.IsNullOrEmpty(a) && a.Trim() != "" ? a : (b ?? "");
    }

    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }
    private static string Attr(string s) { return HttpUtility.HtmlAttributeEncode(HrExport.Clean(s ?? "")); }
    private static string MultiLine(string s) { return Enc(s).Replace("\r\n", "\n").Replace("\n", "<br/>"); }
    private static string OrNot(string s) { return string.IsNullOrEmpty(s) || s.Trim() == "" ? "<span class='hr-muted'>Not recorded</span>" : Enc(s); }
    private static string OrBlank(string s) { return string.IsNullOrEmpty(s) ? "<span class='hr-muted'>Not recorded</span>" : s; }

    private static string FormatBytes(int n)
    {
        if (n <= 0) return "0 KB";
        if (n < 1024 * 1024) return Math.Max(1, (int)Math.Round(n / 1024.0)) + " KB";
        return (n / 1048576.0).ToString("F1", CultureInfo.InvariantCulture) + " MB";
    }

    private static string D(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        return DateTime.TryParse(val.ToString(), out dt) ? dt.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";
    }

    private static string DT(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        return DateTime.TryParse(val.ToString(), out dt) ? dt.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : "";
    }

    /// <summary>JSON escape plus "&lt;/" so a value can never close the inline script block.</summary>
    private string EscapeJs(string s) { return EscapeJson(s ?? "").Replace("</", "<\\/"); }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        if (val is bool) return (bool)val ? 1 : 0;   // TINYINT(1) arrives as bool
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    private string SafeDecStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        decimal d;
        if (decimal.TryParse(val.ToString(), out d)) return d.ToString("0.0", CultureInfo.InvariantCulture);
        return val.ToString();
    }

    private string EscapeJson(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        string s = val.ToString();
        StringBuilder sb = new StringBuilder(s.Length + 10);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private string GetDepartmentSelectExpression(string employeeAlias)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            return BuildDepartmentSqlExpression(conn, employeeAlias);
        }
    }

    private string GetDesignationSelectExpression(string employeeAlias)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            return BuildDesignationSqlExpression(conn, employeeAlias);
        }
    }

    private string BuildDepartmentSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "department"))
            return string.Format("IFNULL(NULLIF(TRIM({0}.department),''), 'Not recorded')", employeeAlias);

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_departments") && ColumnExists(conn, "hrm_emp_contracts", "departmentID"))
        {
            string deptColumn = ColumnExists(conn, "hrm_departments", "dept_name")
                ? "dept_name" : (ColumnExists(conn, "hrm_departments", "department") ? "department" : "");
            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart" : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");
            if (!string.IsNullOrEmpty(deptColumn))
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT d.{0} FROM hrm_emp_contracts c LEFT JOIN hrm_departments d ON c.departmentID = d.ID WHERE c.empID = {1}.empID ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC LIMIT 1)),''), 'Not recorded')",
                    deptColumn, employeeAlias, sortColumn);
        }
        return "'Not recorded'";
    }

    private string BuildDesignationSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "designation"))
            return string.Format("IFNULL(NULLIF(TRIM({0}.designation),''), '')", employeeAlias);

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_jobs") && ColumnExists(conn, "hrm_emp_contracts", "jobID"))
        {
            string jobColumn = ColumnExists(conn, "hrm_jobs", "jobname")
                ? "jobname" : (ColumnExists(conn, "hrm_jobs", "job_name") ? "job_name" : "");
            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart" : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");
            if (!string.IsNullOrEmpty(jobColumn))
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT j.{0} FROM hrm_emp_contracts c LEFT JOIN hrm_jobs j ON c.jobID = j.ID WHERE c.empID = {1}.empID ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC LIMIT 1)),''), '')",
                    jobColumn, employeeAlias, sortColumn);
        }
        return "''";
    }

    private bool TableExists(MySqlConnection conn, string tableName)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t", conn))
        {
            cmd.Parameters.AddWithValue("@t", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private bool ColumnExists(MySqlConnection conn, string tableName, string columnName)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c", conn))
        {
            cmd.Parameters.AddWithValue("@t", tableName);
            cmd.Parameters.AddWithValue("@c", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DATA ACCESS
    // ═══════════════════════════════════════════════════════════════════
    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }

    private MySqlParameter[] CloneParams(List<MySqlParameter> parms)
    {
        MySqlParameter[] clone = new MySqlParameter[parms.Count];
        for (int i = 0; i < parms.Count; i++)
            clone[i] = new MySqlParameter(parms[i].ParameterName, parms[i].Value);
        return clone;
    }

    private void ExecuteNonQuery(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
