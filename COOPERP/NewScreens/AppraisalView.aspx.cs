using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

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
    private string QsReviewer { get { return (Request.QueryString["rev"] ?? "").Trim().ToLower(); } }   // "none" = no reviewer assigned
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
        // Handle AJAX. This runs in the content page's Page_Load, i.e. BEFORE
        // SidebarMaster's login redirect, so every ?ajax= path gates itself here.
        string ajax = (Request.QueryString["ajax"] ?? "").Trim();
        if (!string.IsNullOrEmpty(ajax))
        {
            if (ajax == "evidence")
            {
                ServeEvidence();   // binary stream, has its own login check
                return;
            }

            Response.Clear();
            Response.ContentType = "application/json";
            if (!IsCallerAuthenticated())
            {
                // 200 + ok:false, never 401: forms auth would turn a 401 into a 302 to the
                // login page and the caller would get HTML instead of a readable error.
                Response.Write("{\"ok\":false,\"error\":\"Your session has expired. Please sign in again, then retry.\"}");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }
            HandleAjax(ajax);
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
            return;
        }

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
    //  AUTH / ACCESS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Forms ticket OR Session["username"] - the same two signals the other fixed
    /// eadmin screens accept (different entry points set one or the other).
    /// </summary>
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
    /// Who may perform HR appraisal administration (HR final review, return, cancel,
    /// re-open, change reviewer). Granted when ANY of these holds - a strict superset of
    /// everyone who has actually done this work (AdamN, muhindo, the HR desk):
    ///   1. RBAC admin wildcard in session (RoleAccessService.IsAdmin)
    ///   2. an active sys_user_roles role of 'admin' or 'hr_manager' (the role that is
    ///      granted the hr.appraisal.* menu slugs)
    ///   3. a legacy my_aspnet role: Administrator, System Admin, Human Resource,
    ///      Human Resource Manager
    /// Everyone else who is merely logged in can still VIEW records, but not change them.
    /// </summary>
    private bool HasHrAppraisalAccess()
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
                    DataTable dt = ExecuteQuery(
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
        if (!IsCallerAuthenticated())
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

        // stored_name must be a bare file name - no directories, no traversal.
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
        Response.Write("Evidence file not found.");
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX HANDLER
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        bool isWrite =
            action == "admin_return" || action == "admin_cancel" || action == "admin_reopen" || action == "hr_input" ||
            action == "batch_return" || action == "batch_reopen" || action == "batch_cancel" || action == "batch_hr_input" ||
            action == "admin_change_supervisor" || action == "batch_change_supervisor";

        if (isWrite)
        {
            if (!MarksAntiForgeryService.ValidateRequest())
            {
                Response.Write("{\"ok\":false,\"error\":\"Security validation failed. Please refresh and try again.\"}");
                return;
            }
            if (!HasHrAppraisalAccess())
            {
                Response.Write("{\"ok\":false,\"error\":\"Access denied. HR or administrator access is required for this action.\"}");
                return;
            }
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
                Response.Write("{\"ok\":false,\"error\":\"Unknown action\"}");
                break;
        }
    }

    private void AjaxGetRecordDetail()
    {
        int rid;
        if (!int.TryParse(Request.QueryString["rid"], out rid))
        { Response.Write("{\"error\":\"Invalid record ID\"}"); return; }

        string deptExpr = GetDepartmentSelectExpression("e");
        string empDesignationExpr = GetDesignationSelectExpression("e");
        string reviewerDesignationExpr = GetDesignationSelectExpression("rev");

        // NB: ar.* now carries its own reviewer_name snapshot column, so the live
        // reviewer is aliased live_reviewer_name to avoid a duplicate column.
        string detailSql = string.Format(
            @"SELECT ar.*,
                e.emp_name, e.EMP_CODE, e.EmpType, {0} AS department, {1} AS designation,
                     e.date_joined, e.employment_status,
                     IFNULL(rev.emp_name,'Unassigned') AS live_reviewer_name,
                {2} AS reviewer_designation,
                     s.session_title, s.period_start, s.period_end, s.deadline
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
            WHERE ar.record_id = @rid", deptExpr, empDesignationExpr, reviewerDesignationExpr);

        DataTable dtRec = ExecuteQuery(detailSql, new MySqlParameter("@rid", rid));

        if (dtRec.Rows.Count == 0) { Response.Write("{\"error\":\"Record not found\"}"); return; }
        DataRow rec = dtRec.Rows[0];

        DataTable dtB = ExecuteQuery(
            @"SELECT slot_number, agreed_output, performance_indicators, result_areas, evidence_text,
                     is_na, na_reason, self_rating, supervisor_rating, comments
              FROM appraisal_section_b WHERE record_id = @rid ORDER BY slot_number",
            new MySqlParameter("@rid", rid));

        DataTable dtC = ExecuteQuery(
            @"SELECT competency_code, competency_name, category_name, rating, is_na, comment
              FROM appraisal_section_c WHERE record_id = @rid ORDER BY entry_id",
            new MySqlParameter("@rid", rid));

        DataTable dtD = ExecuteQuery(
            @"SELECT performance_gap, agreed_action, time_frame
              FROM appraisal_section_d WHERE record_id = @rid ORDER BY entry_id",
            new MySqlParameter("@rid", rid));

        DataTable dtE = ExecuteQuery(
            @"SELECT question_number, question_text, response
              FROM appraisal_section_e WHERE record_id = @rid ORDER BY question_number",
            new MySqlParameter("@rid", rid));

        StringBuilder sb = new StringBuilder("{");
        sb.AppendFormat("\"record_id\":{0},", SafeInt(rec["record_id"]));
        sb.AppendFormat("\"status\":\"{0}\",", EscapeJson(rec["status"]));
        sb.AppendFormat("\"staff_category\":\"{0}\",", EscapeJson(rec["staff_category"]));
        sb.AppendFormat("\"emp_name\":\"{0}\",", EscapeJson(rec["emp_name"]));
        sb.AppendFormat("\"EMP_CODE\":\"{0}\",", EscapeJson(rec["EMP_CODE"]));
        sb.AppendFormat("\"EmpType\":\"{0}\",", EscapeJson(rec["EmpType"]));
        sb.AppendFormat("\"department\":\"{0}\",", EscapeJson(rec["department"]));
        sb.AppendFormat("\"designation\":\"{0}\",", EscapeJson(rec["designation"]));
        sb.AppendFormat("\"date_joined\":\"{0}\",", FormatDate(rec["date_joined"]));
        sb.AppendFormat("\"employment_status\":\"{0}\",", EscapeJson(rec["employment_status"]));
        sb.AppendFormat("\"reviewer_name\":\"{0}\",", EscapeJson(rec["live_reviewer_name"]));
        sb.AppendFormat("\"reviewer_designation\":\"{0}\",", EscapeJson(rec["reviewer_designation"]));
        sb.AppendFormat("\"session_title\":\"{0}\",", EscapeJson(rec["session_title"]));
        sb.AppendFormat("\"period_start\":\"{0}\",", FormatDate(rec["period_start"]));
        sb.AppendFormat("\"period_end\":\"{0}\",", FormatDate(rec["period_end"]));
        sb.AppendFormat("\"deadline\":\"{0}\",", FormatDate(rec["deadline"]));
        sb.AppendFormat("\"employee_submitted_at\":\"{0}\",", FormatDateTime(rec["employee_submitted_at"]));
        sb.AppendFormat("\"supervisor_submitted_at\":\"{0}\",", FormatDateTime(rec["supervisor_submitted_at"]));
        sb.AppendFormat("\"employee_ack\":\"{0}\",", EscapeJson(rec["employee_ack"]));
        sb.AppendFormat("\"employee_ack_comment\":\"{0}\",", EscapeJson(rec["employee_ack_comment"]));
        sb.AppendFormat("\"section_b_self_total\":\"{0}\",", SafeDecStr(rec["section_b_self_total"]));
        sb.AppendFormat("\"section_b_supervisor_total\":\"{0}\",", SafeDecStr(rec["section_b_supervisor_total"]));
        sb.AppendFormat("\"section_c_total\":\"{0}\",", SafeDecStr(rec["section_c_total"]));
        sb.AppendFormat("\"raw_score\":\"{0}\",", SafeDecStr(rec["raw_score"]));
        sb.AppendFormat("\"max_possible\":\"{0}\",", SafeDecStr(rec["max_possible"]));
        sb.AppendFormat("\"final_percentage\":\"{0}\",", SafeDecStr(rec["final_percentage"]));
        sb.AppendFormat("\"classification\":\"{0}\",", EscapeJson(Classify(rec["final_percentage"], SafeStr(rec["classification"]))));

        sb.Append("\"section_b\":[");
        for (int i = 0; i < dtB.Rows.Count; i++)
        {
            if (i > 0) sb.Append(",");
            DataRow b = dtB.Rows[i];
            sb.Append("{");
            sb.AppendFormat("\"slot\":{0},", SafeInt(b["slot_number"]));
            sb.AppendFormat("\"output\":\"{0}\",", EscapeJson(b["agreed_output"]));
            sb.AppendFormat("\"indicators\":\"{0}\",", EscapeJson(b["performance_indicators"]));
            sb.AppendFormat("\"result_areas\":\"{0}\",", EscapeJson(b["result_areas"]));
            sb.AppendFormat("\"evidence_text\":\"{0}\",", EscapeJson(b["evidence_text"]));
            sb.AppendFormat("\"is_na\":{0},", SafeInt(b["is_na"]));
            sb.AppendFormat("\"na_reason\":\"{0}\",", EscapeJson(b["na_reason"]));
            sb.AppendFormat("\"self_rating\":\"{0}\",", SafeDecStr(b["self_rating"]));
            sb.AppendFormat("\"sup_rating\":\"{0}\",", SafeDecStr(b["supervisor_rating"]));
            sb.AppendFormat("\"comments\":\"{0}\"", EscapeJson(b["comments"]));
            sb.Append("}");
        }
        sb.Append("],");

        sb.Append("\"section_c\":[");
        for (int i = 0; i < dtC.Rows.Count; i++)
        {
            if (i > 0) sb.Append(",");
            DataRow c = dtC.Rows[i];
            sb.Append("{");
            sb.AppendFormat("\"code\":\"{0}\",", EscapeJson(c["competency_code"]));
            sb.AppendFormat("\"name\":\"{0}\",", EscapeJson(c["competency_name"]));
            sb.AppendFormat("\"category\":\"{0}\",", EscapeJson(c["category_name"]));
            sb.AppendFormat("\"rating\":\"{0}\",", SafeDecStr(c["rating"]));
            sb.AppendFormat("\"is_na\":{0},", SafeInt(c["is_na"]));
            sb.AppendFormat("\"comment\":\"{0}\"", EscapeJson(c["comment"]));
            sb.Append("}");
        }
        sb.Append("],");

        sb.Append("\"section_d\":[");
        for (int i = 0; i < dtD.Rows.Count; i++)
        {
            if (i > 0) sb.Append(",");
            DataRow d = dtD.Rows[i];
            sb.Append("{");
            sb.AppendFormat("\"gap\":\"{0}\",", EscapeJson(d["performance_gap"]));
            sb.AppendFormat("\"action\":\"{0}\",", EscapeJson(d["agreed_action"]));
            sb.AppendFormat("\"timeframe\":\"{0}\"", EscapeJson(d["time_frame"]));
            sb.Append("}");
        }
        sb.Append("],");

        sb.Append("\"section_e\":[");
        for (int i = 0; i < dtE.Rows.Count; i++)
        {
            if (i > 0) sb.Append(",");
            DataRow qe = dtE.Rows[i];
            sb.Append("{");
            sb.AppendFormat("\"q_num\":{0},", SafeInt(qe["question_number"]));
            sb.AppendFormat("\"q_text\":\"{0}\",", EscapeJson(qe["question_text"]));
            sb.AppendFormat("\"response\":\"{0}\"", EscapeJson(qe["response"]));
            sb.Append("}");
        }
        sb.Append("]}");

        Response.Write(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REQUEST BODY HELPERS
    // ═══════════════════════════════════════════════════════════════════
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
        var out2 = new List<int>();
        if (data.ContainsKey("rids"))
        {
            var arr = data["rids"] as System.Collections.ArrayList;
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
    /// finished review is cleared (HR block, reviewer e-signature, employee acknowledgement)
    /// so nothing stale survives into the redo, then scores are recomputed by the one formula.
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
              : Session["username"]   != null ? Session["username"] : (object)"HR Officer").ToString();
    }

    /// <summary>HR final review. Writes ONLY hr_* fields - support_declaration is the employee's.</summary>
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
        if (dt.Rows.Count == 0) return "Appraisal record not found.";
        int empId = SafeInt(dt.Rows[0]["employee_id"]);
        int oldRev = SafeInt(dt.Rows[0]["reviewer_id"]);
        string st = SafeStr(dt.Rows[0]["status"]).ToUpper();
        if (empId == newReviewerId) return "An employee cannot be their own reviewer.";
        if (oldRev == newReviewerId) return "That person is already the reviewer.";

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
        Response.Write("{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: SINGLE ACTIONS
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxAdminReturn()
    {
        try
        {
            var data = ReadJsonBody();
            int rid = Int(data, "rid");
            string comment = Str(data, "comment");
            if (string.IsNullOrEmpty(comment))
            { Response.Write("{\"ok\":false,\"error\":\"Please provide a reason for returning.\"}"); return; }
            string st = RecordStatus(rid);
            if (st == null) { Response.Write("{\"ok\":false,\"error\":\"Record not found.\"}"); return; }
            if (!DoReturn(rid, comment))
            { Response.Write("{\"ok\":false,\"error\":\"Cannot return: status is " + EscapeJson(st) + "\"}"); return; }
            Response.Write("{\"ok\":true,\"message\":\"Appraisal returned to employee for revision.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxAdminCancel()
    {
        try
        {
            var data = ReadJsonBody();
            int rid = Int(data, "rid");
            string st = RecordStatus(rid);
            if (st == null) { Response.Write("{\"ok\":false,\"error\":\"Record not found.\"}"); return; }
            if (!DoCancel(rid, Str(data, "comment")))
            { Response.Write("{\"ok\":false,\"error\":\"Already cancelled.\"}"); return; }
            Response.Write("{\"ok\":true,\"message\":\"Appraisal record cancelled.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxAdminReopen()
    {
        try
        {
            var data = ReadJsonBody();
            int rid = Int(data, "rid");
            string st = RecordStatus(rid);
            if (st == null) { Response.Write("{\"ok\":false,\"error\":\"Record not found.\"}"); return; }
            if (!DoReopen(rid, Str(data, "comment")))
            { Response.Write("{\"ok\":false,\"error\":\"Only completed or HR-reviewed records can be re-opened.\"}"); return; }
            Response.Write("{\"ok\":true,\"message\":\"Appraisal re-opened for supervisor review. HR review, reviewer sign-off and employee acknowledgement were cleared.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxHrInput()
    {
        try
        {
            var data = ReadJsonBody();
            int rid = Int(data, "rid");
            int rating = Int(data, "rating");
            string recommendation = Str(data, "recommendation").ToUpper();
            string comments = Str(data, "comments");

            if (rating < 1 || rating > 5)
            { Response.Write("{\"ok\":false,\"error\":\"Overall rating must be between 1 and 5.\"}"); return; }
            if (!IsValidRecommendation(recommendation))
            { Response.Write("{\"ok\":false,\"error\":\"Invalid recommendation value.\"}"); return; }

            string st = RecordStatus(rid);
            if (st == null) { Response.Write("{\"ok\":false,\"error\":\"Record not found.\"}"); return; }
            if (!DoHrReview(rid, rating, recommendation, comments, HrOfficerName()))
            { Response.Write("{\"ok\":false,\"error\":\"HR input is only allowed on completed appraisals.\"}"); return; }
            Response.Write("{\"ok\":true,\"message\":\"HR review submitted successfully.\"}");
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
            var data = ReadJsonBody();
            var rids = RidsFrom(data);
            string comment = Str(data, "comment");
            if (string.IsNullOrEmpty(comment))
            { Response.Write("{\"ok\":false,\"error\":\"Please provide a reason.\"}"); return; }
            int count = 0;
            foreach (int rid in rids) if (DoReturn(rid, comment)) count++;
            Response.Write("{\"ok\":true,\"count\":" + count + ",\"message\":\"" + count + " record(s) returned to employee.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxBatchReopen()
    {
        try
        {
            var data = ReadJsonBody();
            var rids = RidsFrom(data);
            string comment = Str(data, "comment");
            int count = 0;
            foreach (int rid in rids) if (DoReopen(rid, comment)) count++;
            Response.Write("{\"ok\":true,\"count\":" + count + ",\"message\":\"" + count + " record(s) re-opened.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxBatchCancel()
    {
        try
        {
            var data = ReadJsonBody();
            var rids = RidsFrom(data);
            string comment = Str(data, "comment");
            int count = 0;
            foreach (int rid in rids) if (DoCancel(rid, comment)) count++;
            Response.Write("{\"ok\":true,\"count\":" + count + ",\"message\":\"" + count + " record(s) cancelled.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    private void AjaxBatchHrInput()
    {
        try
        {
            var data = ReadJsonBody();
            var rids = RidsFrom(data);
            int rating = Int(data, "rating");
            string recommendation = Str(data, "recommendation").ToUpper();
            string comments = Str(data, "comments");

            if (rating < 1 || rating > 5)
            { Response.Write("{\"ok\":false,\"error\":\"Rating must be 1-5.\"}"); return; }
            if (!IsValidRecommendation(recommendation))
            { Response.Write("{\"ok\":false,\"error\":\"Invalid recommendation value.\"}"); return; }

            string officer = HrOfficerName();
            int count = 0;
            foreach (int rid in rids) if (DoHrReview(rid, rating, recommendation, comments, officer)) count++;
            Response.Write("{\"ok\":true,\"count\":" + count + ",\"message\":\"" + count + " record(s) HR-reviewed.\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SESSION FILTER
    // ═══════════════════════════════════════════════════════════════════
    private void LoadSessionFilter()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT session_id, session_title, status
              FROM appraisal_sessions
              ORDER BY FIELD(status,'ACTIVE','DRAFT','CLOSED','ARCHIVED'), created_at DESC");

        StringBuilder sb = new StringBuilder();
        sb.Append("<option value='0'>All Sessions</option>");
        foreach (DataRow r in dt.Rows)
        {
            int sid = Convert.ToInt32(r["session_id"]);
            sb.AppendFormat("<option value='{0}'{1}>{2} ({3})</option>",
                sid,
                sid == QsSession ? " selected" : "",
                HttpUtility.HtmlEncode(r["session_title"].ToString()),
                r["status"].ToString());
        }
        litSessionOptions.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  QUICK STATS (list view header)
    // ═══════════════════════════════════════════════════════════════════
    private void LoadListStats()
    {
        try
        {
            DataTable dt = ExecuteQuery(
                "SELECT status, COUNT(*) AS cnt FROM appraisal_records GROUP BY status");

            int pending = 0, inProg = 0, returned = 0, awaitHr = 0, hrDone = 0, cancelled = 0;
            foreach (DataRow r in dt.Rows)
            {
                int cnt = Convert.ToInt32(r["cnt"]);
                switch (SafeStr(r["status"]).ToUpper())
                {
                    case "PENDING":                pending  += cnt; break;
                    case "EMPLOYEE_IN_PROGRESS":   inProg   += cnt; break;
                    case "RETURNED":               returned += cnt; break;
                    case "EMPLOYEE_SUBMITTED":     inProg   += cnt; break;
                    case "SUPERVISOR_IN_PROGRESS": inProg   += cnt; break;
                    case "COMPLETED":              awaitHr  += cnt; break;
                    case "HR_REVIEWED":            hrDone   += cnt; break;
                    case "CANCELLED":              cancelled+= cnt; break;
                }
            }

            int unassigned = 0;
            try
            {
                DataTable dtU = ExecuteQuery(
                    @"SELECT COUNT(*) AS n FROM appraisal_records
                      WHERE (reviewer_id IS NULL OR reviewer_id = 0)
                        AND status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED')" +
                    (QsSession > 0 ? " AND session_id = " + QsSession
                                   : " AND session_id IN (SELECT session_id FROM appraisal_sessions WHERE status = 'ACTIVE')"));
                unassigned = SafeInt(dtU.Rows[0]["n"]);
            }
            catch { }

            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='pa-list-stats'>");
            if (unassigned > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--norev' onclick=\"var r=document.getElementById('selReviewer');if(r){{r.value='none';}}applyFilter();\" title='Open records with no reviewer - HR must assign one'><strong>{0}</strong><span class='pa-list-stat__lbl'>No Reviewer</span></span>", unassigned);
            if (pending > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--pending' onclick=\"document.getElementById('selStatus').value='PENDING';applyFilter();\" title='Filter: Not Started'><strong>{0}</strong><span class='pa-list-stat__lbl'>Not Started</span></span>", pending);
            if (inProg > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--emp' title='In Progress (all stages)'><strong>{0}</strong><span class='pa-list-stat__lbl'>In Progress</span></span>", inProg);
            if (returned > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--returned' onclick=\"document.getElementById('selStatus').value='RETURNED';applyFilter();\" title='Filter: Returned'><strong>{0}</strong><span class='pa-list-stat__lbl'>Returned</span></span>", returned);
            if (awaitHr > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--awaiting' onclick=\"document.getElementById('selStatus').value='COMPLETED';applyFilter();\" title='Filter: Awaiting HR Review'><strong>{0}</strong><span class='pa-list-stat__lbl'>Awaiting HR</span></span>", awaitHr);
            if (hrDone > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--done' onclick=\"document.getElementById('selStatus').value='HR_REVIEWED';applyFilter();\" title='Filter: HR Reviewed'><strong>{0}</strong><span class='pa-list-stat__lbl'>HR Reviewed</span></span>", hrDone);
            if (cancelled > 0)
                sb.AppendFormat("<span class='pa-list-stat pa-list-stat--cancelled' onclick=\"document.getElementById('selStatus').value='CANCELLED';applyFilter();\" title='Filter: Cancelled'><strong>{0}</strong><span class='pa-list-stat__lbl'>Cancelled</span></span>", cancelled);
            sb.Append("</div>");
            litListStats.Text = sb.ToString();
        }
        catch { litListStats.Text = ""; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GRID (List mode)
    // ═══════════════════════════════════════════════════════════════════
    private void BindGrid()
    {
        StringBuilder where = new StringBuilder("WHERE 1=1");
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string deptExpr = GetDepartmentSelectExpression("e");
        string empDesignationExpr = GetDesignationSelectExpression("e");

        if (!string.IsNullOrEmpty(QsSearch))
        {
            where.Append(" AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }
        if (!string.IsNullOrEmpty(QsStatus))
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

        bool canAdmin = HasHrAppraisalAccess();

        // Count
        string countSql = "SELECT COUNT(*) FROM appraisal_records ar INNER JOIN hrm_employee e ON e.empID = ar.employee_id " + where.ToString();
        DataTable dtCount = ExecuteQuery(countSql, parms.ToArray());
        int totalRecords = Convert.ToInt32(dtCount.Rows[0][0]);

        int pageSize = QsPageSize;
        int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
        if (totalPages < 1) totalPages = 1;
        int currentPage = QsPage;
        if (currentPage > totalPages) currentPage = totalPages;
        int offset = (currentPage - 1) * pageSize;

                string dataSql = string.Format(
            @"SELECT ar.record_id, ar.session_id, ar.status, ar.staff_category,
                     ar.final_percentage, ar.raw_score, ar.classification, ar.updated_at,
                     ar.reviewer_id, ar.employee_ack, ar.employee_ack_comment,
                                         e.emp_name, e.EMP_CODE, {0} AS department, {1} AS designation,
                     IFNULL(rev.emp_name,'Unassigned') AS reviewer_name,
                     s.session_title
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
                            {2}
              ORDER BY ar.updated_at DESC
                            LIMIT {3} OFFSET {4}",
                        deptExpr, empDesignationExpr, where.ToString(), pageSize, offset);

        DataTable dtData = ExecuteQuery(dataSql, CloneParams(parms));

        // Render
        StringBuilder html = new StringBuilder();
        if (dtData.Rows.Count == 0)
        {
            html.Append("<tr><td colspan='11' class='pa-empty-state'>");
            html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.5'><path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/></svg>");
            html.Append("<p>No appraisal records found. Try adjusting your filters.</p></td></tr>");
        }
        else
        {
            int rowNum = 0;
            foreach (DataRow r in dtData.Rows)
            {
                rowNum++;
                int recId    = Convert.ToInt32(r["record_id"]);
                string status = SafeStr(r["status"]);
                string statusUp = status.ToUpper();
                string empName  = SafeStr(r["emp_name"]);
                string dept     = SafeStr(r["department"]);
                string session  = SafeStr(r["session_title"]);
                string rawScore = (r["raw_score"] != null && r["raw_score"] != DBNull.Value) ? SafeStr(r["raw_score"]) : "";
                string pctRaw   = (r["final_percentage"] != null && r["final_percentage"] != DBNull.Value)
                                  ? Convert.ToDecimal(r["final_percentage"]).ToString("F1") : "";
                string cls      = Classify(r["final_percentage"], SafeStr(r["classification"]));

                bool noReviewer = SafeInt(r["reviewer_id"]) <= 0;
                string reviewerName = noReviewer ? "Unassigned" : SafeStr(r["reviewer_name"]);
                string ackRaw = SafeStr(r["employee_ack"]).ToUpper();
                string ackLine = (ackRaw == "AGREE" || ackRaw == "DISAGREE")
                    ? (ackRaw == "AGREE" ? "Agrees" : "Disagrees") + (SafeStr(r["employee_ack_comment"]) != "" ? ": " + SafeStr(r["employee_ack_comment"]) : "")
                    : "Not yet acknowledged";

                // TR with data attributes for JS selection
                html.AppendFormat(
                    "<tr class='pa-row' data-rid='{0}' data-status='{1}' data-empname='{2}' data-dept='{3}' data-session='{4}' data-score='{5}' data-pct='{6}' data-cls='{7}' data-reviewer='{8}' data-ack='{9}'>",
                    recId,
                    HttpUtility.HtmlAttributeEncode(statusUp),
                    HttpUtility.HtmlAttributeEncode(empName),
                    HttpUtility.HtmlAttributeEncode(dept),
                    HttpUtility.HtmlAttributeEncode(session),
                    HttpUtility.HtmlAttributeEncode(rawScore),
                    HttpUtility.HtmlAttributeEncode(pctRaw),
                    HttpUtility.HtmlAttributeEncode(cls),
                    HttpUtility.HtmlAttributeEncode(reviewerName),
                    HttpUtility.HtmlAttributeEncode(ackLine));

                // Checkbox
                html.AppendFormat("<td class='pa-col-chk' onclick='event.stopPropagation();'><input type='checkbox' class='pa-row-chk' value='{0}' onclick='onRowCheck(this,event)'></td>", recId);

                // Row number
                html.AppendFormat("<td class='pa-col-num'>{0}</td>", offset + rowNum);

                // Employee (name is a link)
                html.AppendFormat("<td><a href='AppraisalView.aspx?rid={0}' class='pa-emp-link'>{1}</a><br/><span class='pa-emp-code'>{2}</span></td>",
                    recId,
                    HttpUtility.HtmlEncode(empName),
                    HttpUtility.HtmlEncode(SafeStr(r["EMP_CODE"])));

                // Department
                html.AppendFormat("<td style='font-size:11px;color:#444;'>{0}</td>", HttpUtility.HtmlEncode(dept));

                // Category chip
                string cat = SafeStr(r["staff_category"]);
                string catColor = cat == "ACADEMIC" ? "#174DA4" : cat == "ADMINISTRATIVE" ? "#856404" : "#155724";
                string catBg    = cat == "ACADEMIC" ? "#e8eef8" : cat == "ADMINISTRATIVE" ? "#fff3cd" : "#d4edda";
                html.AppendFormat("<td><span style='font-size:10px;font-weight:700;padding:2px 7px;border-radius:3px;background:{0};color:{1};'>{2}</span></td>",
                    catBg, catColor, HttpUtility.HtmlEncode(cat));

                // Session
                html.AppendFormat("<td style='font-size:11px;color:#444;'>{0}</td>", HttpUtility.HtmlEncode(session));

                // Reviewer
                if (noReviewer)
                    html.Append("<td><span class='pa-norev-badge'>No reviewer</span></td>");
                else
                    html.AppendFormat("<td style='font-size:11px;color:#666;'>{0}</td>", HttpUtility.HtmlEncode(reviewerName));

                // Status badge
                html.AppendFormat("<td><span class='pa-rec-badge pa-rec-badge--{0}'>{1}</span></td>",
                    GetRecordBadgeModifier(statusUp), FormatStatusLabel(statusUp));

                // Score
                string pctDisplay = pctRaw != "" ? pctRaw + "%" : "—";
                string pctColor = pctRaw != "" ? "color:" + ScoreColor(r["final_percentage"]) + ";font-weight:700;" : "";
                html.AppendFormat("<td class='pa-num' style='font-size:12px;{0}'>{1}</td>", pctColor, pctDisplay);

                // Classification
                html.AppendFormat("<td style='font-size:11px;color:#444;'>{0}</td>", HttpUtility.HtmlEncode(cls != "" ? cls : "—"));

                // ── Actions dropdown ──
                html.Append("<td class='pa-col-act' onclick='event.stopPropagation();'><div class='pa-act-menu'>");
                html.Append("<button type='button' class='pa-act-btn' onclick='toggleActMenu(this)'>");
                html.Append("Actions <svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5'><polyline points='6 9 12 15 18 9'/></svg>");
                html.Append("</button><div class='pa-act-drop'>");

                // View Details
                html.AppendFormat("<a href='AppraisalView.aspx?rid={0}' class='pa-act-item'>", recId);
                html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z'/><circle cx='12' cy='12' r='3'/></svg> View Details</a>");

                // HR Final Review
                if (canAdmin && (statusUp == "COMPLETED" || statusUp == "HR_REVIEWED"))
                {
                    html.Append("<button type='button' class='pa-act-item pa-act-item--hr' onclick='openHrWizardFromRowBtn(this)'>");
                    html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><polyline points='17 11 19 13 23 9'/></svg> HR Final Review</button>");
                }

                // Return to Employee
                if (canAdmin && (statusUp == "EMPLOYEE_SUBMITTED" || statusUp == "SUPERVISOR_IN_PROGRESS"))
                {
                    html.AppendFormat("<button type='button' class='pa-act-item' onclick='adminReturnToEmployee({0})'>", recId);
                    html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='1 4 1 10 7 10'/><path d='M3.51 15a9 9 0 1 0 2.13-9.36L1 10'/></svg> Return to Employee</button>");
                }

                // Re-open for Supervisor
                if (canAdmin && (statusUp == "COMPLETED" || statusUp == "HR_REVIEWED"))
                {
                    html.AppendFormat("<button type='button' class='pa-act-item' onclick='adminReopen({0})'>", recId);
                    html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M21 2v6h-6'/><path d='M21 13a9 9 0 1 1-3-7.7L21 8'/></svg> Re-open for Supervisor</button>");
                }

                // Print / PDF
                html.AppendFormat("<a href='AppraisalPrint.aspx?rid={0}&amp;autoprint=1' target='_blank' class='pa-act-item'>", recId);
                html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='6 9 6 2 18 2 18 9'/><path d='M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2'/><rect x='6' y='14' width='12' height='8'/></svg> Print / PDF</a>");

                // Change Supervisor
                if (canAdmin)
                {
                    html.Append("<button type='button' class='pa-act-item' onclick='changeSupervisorFromRow(this)'>");
                    html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M23 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/></svg> Change Supervisor</button>");
                }

                // Cancel (danger, with separator)
                if (canAdmin && statusUp != "CANCELLED")
                {
                    html.Append("<div class='pa-act-sep'></div>");
                    html.AppendFormat("<button type='button' class='pa-act-item pa-act-item--danger' onclick='adminCancel({0})'>", recId);
                    html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg> Cancel Record</button>");
                }

                html.Append("</div></div></td></tr>");
            }
        }
        litGridBody.Text = html.ToString() +
            (canAdmin ? "" : "<script type='text/javascript'>window.PA_READ_ONLY=true;</script>");

        // Stats
        litTotalCount.Text = totalRecords.ToString("N0");

        // Pager info
        litPagerInfo.Text = string.Format("Showing {0}–{1} of {2}",
            totalRecords == 0 ? 0 : offset + 1,
            Math.Min(offset + pageSize, totalRecords),
            totalRecords);

        BuildPager(currentPage, totalPages);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DETAIL VIEW (server-rendered for when ?rid= is present)
    // ═══════════════════════════════════════════════════════════════════
    private void LoadRecordDetail(int rid)
    {
        string deptExpr = GetDepartmentSelectExpression("e");
        string empDesignationExpr = GetDesignationSelectExpression("e");
        string reviewerDesignationExpr = GetDesignationSelectExpression("rev");

        // ar.* carries the snapshot reviewer_name/reviewer_title columns; the live
        // reviewer is aliased live_reviewer_name so the two never collide.
        string detailSql = string.Format(
            @"SELECT ar.*,
                     e.emp_name, e.EMP_CODE, e.EmpType, {0} AS department, {1} AS designation,
                     e.date_joined, e.employment_status,
                     IFNULL(rev.emp_name,'Unassigned') AS live_reviewer_name,
                     {2} AS reviewer_designation,
                     s.session_title, s.period_start, s.period_end, s.deadline
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE ar.record_id = @rid", deptExpr, empDesignationExpr, reviewerDesignationExpr);

        DataTable dtRec = ExecuteQuery(detailSql, new MySqlParameter("@rid", rid));
        if (dtRec.Rows.Count == 0)
        {
            litDetailContent.Text = "<div class='pa-alert--error'>Appraisal record not found.</div>";
            return;
        }

        DataRow rec = dtRec.Rows[0];
        string status        = SafeStr(rec["status"]);
        string staffCategory = SafeStr(rec["staff_category"]);
        bool canAdmin        = HasHrAppraisalAccess();

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

        StringBuilder html = new StringBuilder();

        // ── HR action alert banner (COMPLETED = awaiting HR) ──
        if (status == "COMPLETED")
        {
            html.Append("<div style='background:#fff5f5;border:1px solid #fca5a5;border-left:4px solid #dc3545;" +
                "border-radius:4px;padding:14px 18px;margin-bottom:16px;display:flex;align-items:center;gap:14px;'>");
            html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='22' height='22' viewBox='0 0 24 24' fill='none' stroke='#dc3545' stroke-width='2'>" +
                "<path d='M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z'/>" +
                "<line x1='12' y1='9' x2='12' y2='13'/><line x1='12' y1='17' x2='12.01' y2='17'/></svg>");
            html.Append("<div style='flex:1;'>" +
                "<div style='font-size:13px;font-weight:700;color:#991b1b;'>This appraisal is awaiting HR review &amp; sign-off</div>" +
                "<div style='font-size:12px;color:#b91c1c;margin-top:2px;'>Supervisor has completed their assessment. Scroll down to submit HR Final Input.</div>" +
                "</div>");
            if (canAdmin)
                html.Append("<button type='button' class='hr-btn hr-btn--primary' onclick='openHrWizardFromData()' style='white-space:nowrap;flex-shrink:0;'>" +
                    "HR Final Input &rarr;</button>");
            html.Append("</div>");
        }
        else if (status == "HR_REVIEWED")
        {
            html.Append("<div style='background:#f0fdf4;border:1px solid #86efac;border-left:4px solid #22c55e;" +
                "border-radius:4px;padding:12px 18px;margin-bottom:16px;display:flex;align-items:center;gap:10px;'>");
            html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='#22c55e' stroke-width='2'>" +
                "<path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'/><polyline points='22 4 12 14.01 9 11.01'/></svg>");
            html.Append("<div style='font-size:13px;font-weight:600;color:#166534;'>HR review completed. You may update it below if needed.</div>");
            html.Append("</div>");
        }
        if (SafeInt(rec["reviewer_id"]) <= 0 && status != "CANCELLED")
        {
            html.Append("<div style='background:#fffbeb;border:1px solid #fcd34d;border-left:4px solid #d97706;" +
                "border-radius:4px;padding:12px 18px;margin-bottom:16px;font-size:13px;color:#92400e;'>" +
                "<strong>No reviewer assigned.</strong> Nobody can review this appraisal until HR assigns one (Change Supervisor below).</div>");
        }

        // ── Header ──
        html.Append("<div class='pa-detail-header'>");
        html.Append("<div class='pa-detail-header__left'>");
        html.AppendFormat("<h2 class='pa-detail-header__name'>{0}</h2>", HttpUtility.HtmlEncode(SafeStr(rec["emp_name"])));
        html.AppendFormat("<div class='pa-detail-header__meta'>{0} &middot; {1} &middot; {2} &middot; <strong>{3}</strong></div>",
            HttpUtility.HtmlEncode(SafeStr(rec["EMP_CODE"])),
            HttpUtility.HtmlEncode(SafeStr(rec["department"])),
            HttpUtility.HtmlEncode(SafeStr(rec["designation"])),
            HttpUtility.HtmlEncode(staffCategory));
        html.Append("</div>");
        html.AppendFormat("<span class='pa-rec-badge pa-rec-badge--{0}' style='font-size:12px;padding:4px 12px;'>{1}</span>",
            GetRecordBadgeModifier(status), FormatStatusLabel(status));
        html.Append("</div>");

        // ── Section A: header in the Council format (snapshot first, live lookup as fallback) ──
        string liveReviewer = SafeStr(rec["live_reviewer_name"]);
        if (liveReviewer == "Unassigned") liveReviewer = "";
        string position   = FirstNonEmpty(SafeStr(rec["snap_position"]), SafeStr(rec["designation"]));
        string department = FirstNonEmpty(SafeStr(rec["snap_department"]), SafeStr(rec["department"]));
        string reportsTo  = FirstNonEmpty(SafeStr(rec["snap_reports_to"]), liveReviewer);
        string revName    = FirstNonEmpty(SafeStr(rec["reviewer_name"]), liveReviewer);
        string revTitle   = FirstNonEmpty(SafeStr(rec["reviewer_title"]), SafeStr(rec["reviewer_designation"]));
        object reviewDate = rec["reviewer_signed_at"] != DBNull.Value ? rec["reviewer_signed_at"] : rec["supervisor_submitted_at"];
        object submitDate = rec["employee_signed_at"] != DBNull.Value ? rec["employee_signed_at"] : rec["employee_submitted_at"];

        html.Append("<div class='pa-detail-section'>");
        html.Append("<div class='pa-detail-section__hdr'>Section A &mdash; Employee &amp; Review Details</div>");
        html.Append("<div class='pa-detail-section__body'><div class='pa-info-grid pa-info-grid--3'>");
        AppendInfoItem(html, "Name",              SafeStr(rec["emp_name"]));
        AppendInfoItem(html, "Employee No.",      SafeStr(rec["EMP_CODE"]));
        AppendInfoItem(html, "Date of Review",    reviewDate == DBNull.Value ? "" : FormatDateDisplay(reviewDate));
        AppendInfoItem(html, "Position Held",     position);
        AppendInfoItem(html, "Department",        department);
        AppendInfoItem(html, "Reports To",        reportsTo);
        AppendInfoItem(html, "Reviewer's Name",   revName);
        AppendInfoItem(html, "Reviewer's Title",  revTitle);
        AppendInfoItem(html, "Date Submitted",    submitDate == DBNull.Value ? "" : FormatDateDisplay(submitDate));
        AppendInfoItem(html, "Staff Category",    staffCategory);
        html.AppendFormat("<div class='pa-info-item'><span class='pa-info-label'>Appraisal Period</span>" +
            "<span class='pa-info-val'>{0} &mdash; {1}</span></div>",
            FormatDateDisplay(rec["period_start"]), FormatDateDisplay(rec["period_end"]));
        AppendInfoItem(html, "Session", SafeStr(rec["session_title"]));
        html.Append("</div></div></div>");

        // ── Rating scale legend ──
        html.AppendFormat(
            "<div style='background:#f0f4f8;border:1px solid #c5d3e8;border-radius:4px;padding:8px 14px;" +
            "margin-bottom:12px;font-size:11px;color:#555;'>" +
            "<strong style='color:#174DA4;'>Rating Scale ({0}):</strong> &nbsp; {1}</div>",
            HttpUtility.HtmlEncode(staffCategory), RatingLegend(staffCategory));

        // ── Section B: Responsibilities / Key Performance Areas ──
        // evidence files grouped by slot
        Dictionary<int, List<DataRow>> evBySlot = new Dictionary<int, List<DataRow>>();
        foreach (DataRow ev in dtEv.Rows)
        {
            int slot = SafeInt(ev["slot_number"]);
            if (!evBySlot.ContainsKey(slot)) evBySlot[slot] = new List<DataRow>();
            evBySlot[slot].Add(ev);
        }

        html.Append("<div class='pa-detail-section'>");
        html.Append("<div class='pa-detail-section__hdr' style='display:flex;align-items:center;'>Section B &mdash; Responsibilities / Key Performance Areas" +
            "<span style='margin-left:auto;font-size:10px;font-weight:400;color:#888;text-transform:none;letter-spacing:0;'>" +
            "Self &amp; Supervisor Ratings (scale 1&ndash;5)</span></div>");
        html.Append("<div class='pa-detail-section__body' style='padding:0;overflow-x:auto;'>");
        int bApplicable = 0;
        if (dtB.Rows.Count == 0)
        {
            html.Append("<p class='pa-detail-empty' style='padding:16px;'>No responsibilities / key performance areas recorded yet.</p>");
        }
        else
        {
            html.Append("<table class='pa-table pa-detail-table pa-b-table'><thead><tr>");
            html.Append("<th style='width:28px;text-align:center;'>#</th>");
            html.Append("<th>Responsibility / KPA</th><th>Expected Standard</th><th>Achievement</th><th>Evidence</th>");
            html.Append("<th style='text-align:center;min-width:80px;'>Self</th>");
            html.Append("<th style='text-align:center;min-width:90px;'>Supervisor</th>");
            html.Append("<th>Supervisor Comment</th>");
            html.Append("</tr></thead><tbody>");
            foreach (DataRow b in dtB.Rows)
            {
                int slot = SafeInt(b["slot_number"]);
                bool isNa = SafeInt(b["is_na"]) == 1;
                string kpa = SafeStr(b["agreed_output"]);
                if (!isNa && kpa.Trim() != "") bApplicable++;
                bool supRated = SafeInt(b["supervisor_rating"]) > 0;

                string rowStyle = isNa ? " class='pa-b-na'" : (supRated ? "" : " style='background:#fffbeb;'");
                html.AppendFormat("<tr{0}>", rowStyle);
                html.AppendFormat("<td style='text-align:center;color:#aaa;font-size:11px;'>{0}</td>", slot);
                html.AppendFormat("<td style='font-size:12px;'>{0}{1}</td>",
                    HttpUtility.HtmlEncode(kpa),
                    SafeInt(b["is_catalogue"]) == 1 ? " <span class='pa-tag'>Catalogue</span>" : "");
                html.AppendFormat("<td style='font-size:11px;color:#666;'>{0}</td>", MultiLine(SafeStr(b["performance_indicators"])));

                if (isNa)
                {
                    string reason = SafeStr(b["na_reason"]);
                    html.AppendFormat("<td colspan='2' style='font-size:11px;'><strong>Not applicable</strong>{0}</td>",
                        reason != "" ? " &mdash; " + HttpUtility.HtmlEncode(reason) : "");
                    html.Append("<td style='text-align:center;font-size:11px;'>N/A</td><td style='text-align:center;font-size:11px;'>N/A</td>");
                }
                else
                {
                    html.AppendFormat("<td style='font-size:11px;color:#444;'>{0}</td>", MultiLine(SafeStr(b["result_areas"])));
                    html.Append("<td style='font-size:11px;color:#444;'>");
                    string evText = SafeStr(b["evidence_text"]);
                    if (evText != "") html.AppendFormat("<div>{0}</div>", MultiLine(evText));
                    if (evBySlot.ContainsKey(slot))
                    {
                        html.Append("<div class='pa-ev-list'>");
                        foreach (DataRow ev in evBySlot[slot])
                            html.AppendFormat(
                                "<a class='pa-ev-file' href='AppraisalView.aspx?ajax=evidence&amp;id={0}' target='_blank' rel='noopener'>" +
                                "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48'/></svg>" +
                                "{1} <span style='color:#999;'>({2})</span></a>",
                                SafeInt(ev["evidence_id"]),
                                HttpUtility.HtmlEncode(SafeStr(ev["original_name"])),
                                FormatBytes(SafeInt(ev["size_bytes"])));
                        html.Append("</div>");
                    }
                    if (evText == "" && !evBySlot.ContainsKey(slot)) html.Append("<span style='color:#ccc;'>&mdash;</span>");
                    html.Append("</td>");
                    html.AppendFormat("<td style='text-align:center;'>{0}</td>", RatingBadge(b["self_rating"], staffCategory));
                    html.AppendFormat("<td style='text-align:center;'>{0}</td>", RatingBadge(b["supervisor_rating"], staffCategory));
                }
                string supCmt = SafeStr(b["comments"]);
                html.AppendFormat("<td style='font-size:11px;color:#555;'>{0}</td>",
                    supCmt != "" ? MultiLine(supCmt) : "<span style='color:#ccc;'>&mdash;</span>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
            string bSupTotal = SafeDecStr(rec["section_b_supervisor_total"]);
            string bSelfTotal = SafeDecStr(rec["section_b_self_total"]);
            html.AppendFormat(
                "<div style='padding:8px 14px;background:#f8f9fa;border-top:2px solid #e0e5ed;font-size:12px;display:flex;gap:20px;flex-wrap:wrap;'>" +
                "<span>Employee B Total: <strong>{0}</strong></span>" +
                "<span>Supervisor B Total: <strong style='color:#174DA4;'>{1}</strong></span>" +
                "<span style='color:#888;font-size:11px;margin-left:auto;'>({2} applicable row{3} &times; max 5 = {4} max possible; N/A rows excluded)</span></div>",
                bSelfTotal != "" ? bSelfTotal : "&mdash;",
                bSupTotal  != "" ? bSupTotal  : "&mdash;",
                bApplicable, bApplicable == 1 ? "" : "s", bApplicable * 5);
        }
        html.Append("</div></div>");

        // ── Section C: Competency Assessment ──
        html.Append("<div class='pa-detail-section'>");
        html.Append("<div class='pa-detail-section__hdr' style='display:flex;align-items:center;'>Section C &mdash; Competency Assessment" +
            "<span style='margin-left:auto;font-size:10px;font-weight:400;color:#888;text-transform:none;letter-spacing:0;'>" +
            "Both self &amp; supervisor ratings shown</span></div>");
        html.Append("<div class='pa-detail-section__body' style='padding:0;overflow-x:auto;'>");
        if (dtC.Rows.Count == 0)
        {
            html.Append("<p class='pa-detail-empty' style='padding:16px;'>No competency ratings recorded yet.</p>");
        }
        else
        {
            string lastCat2 = "";
            int cNonNa = 0;
            html.Append("<table class='pa-table pa-detail-table'><thead><tr>");
            html.Append("<th style='width:50px;'>Code</th><th>Competency</th>");
            html.Append("<th style='text-align:center;min-width:80px;'>Employee<br/>Self</th>");
            html.Append("<th style='text-align:center;min-width:90px;'>Supervisor<br/>Rating</th>");
            html.Append("<th style='text-align:center;width:40px;'>N/A</th>");
            html.Append("<th>Employee Comment</th>");
            html.Append("<th>Supervisor Comment</th>");
            html.Append("</tr></thead><tbody>");
            foreach (DataRow c in dtC.Rows)
            {
                string cat = SafeStr(c["category_name"]);
                if (cat != lastCat2)
                {
                    html.AppendFormat("<tr class='pa-cat-row'><td colspan='7'><strong>{0}</strong></td></tr>",
                        HttpUtility.HtmlEncode(cat));
                    lastCat2 = cat;
                }
                bool isNA = SafeInt(c["is_na"]) == 1;
                int selfRatC = SafeInt(c["self_rating"]);
                if (!isNA) cNonNa++;

                html.AppendFormat("<tr{0}>", isNA ? " class='pa-b-na'" : "");
                html.AppendFormat("<td style='color:#888;font-size:11px;'>{0}</td>", HttpUtility.HtmlEncode(SafeStr(c["competency_code"])));
                html.AppendFormat("<td style='font-size:12px;'>{0}</td>", HttpUtility.HtmlEncode(SafeStr(c["competency_name"])));
                html.AppendFormat("<td style='text-align:center;'>{0}</td>",
                    isNA ? "<span style='color:#999;font-size:11px;'>N/A</span>"
                         : (selfRatC > 0 ? RatingBadge(c["self_rating"], staffCategory) : "<span style='color:#ccc;'>&mdash;</span>"));
                html.AppendFormat("<td style='text-align:center;'>{0}</td>",
                    isNA ? "<span style='color:#999;font-size:11px;'>N/A</span>" : RatingBadge(c["rating"], staffCategory));
                html.AppendFormat("<td style='text-align:center;font-size:11px;font-weight:600;color:#888;'>{0}</td>",
                    isNA ? "Yes" : "");
                string empCmtC = SafeStr(c["comment"]);
                html.AppendFormat("<td style='font-size:11px;color:#555;'>{0}</td>",
                    empCmtC != "" ? HttpUtility.HtmlEncode(empCmtC) : "<span style='color:#ccc;'>&mdash;</span>");
                string supCmtC = SafeStr(c["supervisor_comment"]);
                html.AppendFormat("<td style='font-size:11px;color:#555;'>{0}</td>",
                    supCmtC != "" ? HttpUtility.HtmlEncode(supCmtC) : "<span style='color:#ccc;'>&mdash;</span>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
            string cTotal = SafeDecStr(rec["section_c_total"]);
            html.AppendFormat(
                "<div style='padding:8px 14px;background:#f8f9fa;border-top:2px solid #e0e5ed;font-size:12px;display:flex;gap:20px;flex-wrap:wrap;'>" +
                "<span>Section C Supervisor Total: <strong style='color:#174DA4;'>{0}</strong></span>" +
                "<span style='color:#888;font-size:11px;margin-left:auto;'>({1} applicable criteria &times; max 5 = {2} max possible)</span></div>",
                cTotal != "" ? cTotal : "&mdash;", cNonNa, cNonNa * 5);
        }
        html.Append("</div></div>");

        // ── Section D: Training & Development Plan ──
        html.Append("<div class='pa-detail-section'>");
        html.Append("<div class='pa-detail-section__hdr'>Section D &mdash; Training &amp; Development Plan</div>");
        html.Append("<div class='pa-detail-section__body' style='padding:0;'>");
        if (dtD.Rows.Count == 0)
        {
            html.Append("<p class='pa-detail-empty' style='padding:16px;'>No training plans recorded yet.</p>");
        }
        else
        {
            html.Append("<table class='pa-table pa-detail-table'><thead><tr>" +
                "<th style='width:28px;text-align:center;'>#</th>" +
                "<th>Performance Gap</th><th>Agreed Action</th><th style='width:100px;'>Time Frame</th>" +
                "</tr></thead><tbody>");
            int dIdx = 0;
            foreach (DataRow d in dtD.Rows)
            {
                dIdx++;
                html.Append("<tr>");
                html.AppendFormat("<td style='text-align:center;color:#aaa;font-size:11px;'>{0}</td>", dIdx);
                html.AppendFormat("<td style='font-size:12px;'>{0}</td>", HttpUtility.HtmlEncode(SafeStr(d["performance_gap"])));
                html.AppendFormat("<td style='font-size:12px;'>{0}</td>", HttpUtility.HtmlEncode(SafeStr(d["agreed_action"])));
                html.AppendFormat("<td style='font-size:11px;color:#666;'>{0}</td>", HttpUtility.HtmlEncode(SafeStr(d["time_frame"])));
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
        }
        html.Append("</div></div>");

        // ── Section E: Reflective Questions ──
        html.Append("<div class='pa-detail-section'>");
        html.Append("<div class='pa-detail-section__hdr'>Section E &mdash; Employee Reflective Comments</div>");
        html.Append("<div class='pa-detail-section__body'>");
        if (dtE.Rows.Count == 0)
        {
            html.Append("<p class='pa-detail-empty'>No responses recorded yet.</p>");
        }
        else
        {
            foreach (DataRow qe in dtE.Rows)
            {
                string resp = SafeStr(qe["response"]);
                html.Append("<div class='pa-comment-block'>");
                html.AppendFormat("<div class='pa-comment-block__q'>Q{0}. {1}</div>",
                    SafeInt(qe["question_number"]),
                    HttpUtility.HtmlEncode(SafeStr(qe["question_text"])));
                html.AppendFormat("<div class='pa-comment-block__a'>{0}</div>",
                    resp != "" ? HttpUtility.HtmlEncode(resp) : "<em style='color:#999;'>No response provided</em>");
                html.Append("</div>");
            }
        }
        html.Append("</div></div>");

        // ── Score Summary ──
        string finalPct    = SafeDecStr(rec["final_percentage"]);
        string rawScore    = SafeDecStr(rec["raw_score"]);
        string maxPoss     = SafeDecStr(rec["max_possible"]);
        string bSupTot     = SafeDecStr(rec["section_b_supervisor_total"]);
        string cTot        = SafeDecStr(rec["section_c_total"]);
        string classif     = Classify(rec["final_percentage"], SafeStr(rec["classification"]));
        string pctColor    = ScoreColor(rec["final_percentage"]);

        html.Append("<div class='pa-detail-section pa-detail-section--score'>");
        html.Append("<div class='pa-detail-section__hdr'>Score Summary</div>");
        html.Append("<div class='pa-detail-section__body'>");
        html.Append("<div class='pa-score-grid'>");
        html.AppendFormat("<div class='pa-score-item'><span class='pa-score-label'>Section B Supervisor</span><span class='pa-score-val' style='font-size:18px;'>{0}</span></div>",
            bSupTot != "" ? bSupTot : "&mdash;");
        html.AppendFormat("<div class='pa-score-item'><span class='pa-score-label'>Section C Supervisor</span><span class='pa-score-val' style='font-size:18px;'>{0}</span></div>",
            cTot != "" ? cTot : "&mdash;");
        html.AppendFormat("<div class='pa-score-item'><span class='pa-score-label'>Raw Score (B + C)</span><span class='pa-score-val' style='font-size:18px;'>{0}</span></div>",
            rawScore != "" ? rawScore : "&mdash;");
        html.AppendFormat("<div class='pa-score-item'><span class='pa-score-label'>Max Possible</span><span class='pa-score-val' style='font-size:18px;'>{0}</span></div>",
            maxPoss != "" ? maxPoss : "&mdash;");
        html.AppendFormat("<div class='pa-score-item pa-score-item--final'><span class='pa-score-label'>Final Percentage</span><span class='pa-score-val' style='color:{0};'>{1}</span></div>",
            pctColor, finalPct != "" ? finalPct + "%" : "&mdash;");
        html.AppendFormat("<div class='pa-score-item pa-score-item--class'><span class='pa-score-label'>Classification</span><span class='pa-score-val' style='font-size:14px;'>{0}</span></div>",
            classif != "" ? HttpUtility.HtmlEncode(classif) : "&mdash;");
        html.Append("</div>");
        if (rawScore != "" && maxPoss != "")
            html.AppendFormat(
                "<div style='font-size:11px;color:#888;margin-top:6px;'>Calculation: {0} raw &divide; {1} max &times; 100 = <strong>{2}%</strong> " +
                "&nbsp;&middot;&nbsp; Scale: Exceptional &ge;90 &middot; Above Expectations &ge;75 &middot; Satisfactory &ge;60 &middot; Development Needed &ge;50 &middot; Unsatisfactory &lt;50</div>",
                rawScore, maxPoss, finalPct != "" ? finalPct : "&mdash;");
        html.Append("</div></div>");

        // ── Sign-off: employee e-signature, reviewer e-signature, employee acknowledgement ──
        string empSignName = SafeStr(rec["employee_sign_name"]);
        string revSignName = SafeStr(rec["reviewer_sign_name"]);
        string ack         = SafeStr(rec["employee_ack"]).ToUpper();
        string ackComment  = SafeStr(rec["employee_ack_comment"]);

        html.Append("<div class='pa-detail-section'>");
        html.Append("<div class='pa-detail-section__hdr'>Sign-off</div>");
        html.Append("<div class='pa-detail-section__body'><div class='pa-sign-grid'>");

        html.Append("<div class='pa-sign-item'><div class='pa-info-label'>Employee (Appraisee)</div>");
        if (empSignName != "")
            html.AppendFormat("<div class='pa-sign-name'>{0}</div><div class='pa-sign-date'>E-signed {1}</div>",
                HttpUtility.HtmlEncode(empSignName), FormatDateTimeDisplay(rec["employee_signed_at"]));
        else
            html.AppendFormat("<div class='pa-sign-none'>Not e-signed</div>{0}",
                rec["employee_submitted_at"] != DBNull.Value
                    ? "<div class='pa-sign-date'>Submitted " + FormatDateTimeDisplay(rec["employee_submitted_at"]) + "</div>" : "");
        html.Append("</div>");

        html.Append("<div class='pa-sign-item'><div class='pa-info-label'>Reviewer (Appraiser)</div>");
        if (revSignName != "")
            html.AppendFormat("<div class='pa-sign-name'>{0}</div><div class='pa-sign-date'>E-signed {1}</div>",
                HttpUtility.HtmlEncode(revSignName), FormatDateTimeDisplay(rec["reviewer_signed_at"]));
        else
            html.AppendFormat("<div class='pa-sign-none'>Not e-signed</div>{0}",
                rec["supervisor_submitted_at"] != DBNull.Value
                    ? "<div class='pa-sign-date'>Completed " + FormatDateTimeDisplay(rec["supervisor_submitted_at"]) + "</div>" : "");
        html.Append("</div>");

        html.Append("<div class='pa-sign-item'><div class='pa-info-label'>Employee Acknowledgement of Rating</div>");
        if (ack == "AGREE" || ack == "DISAGREE")
        {
            html.AppendFormat("<div class='pa-ack pa-ack--{0}'>{1}</div>", ack.ToLower(), ack == "AGREE" ? "Agrees" : "Disagrees");
            if (ackComment != "")
                html.AppendFormat("<div class='pa-ack-comment'>{0}</div>", MultiLine(ackComment));
            html.AppendFormat("<div class='pa-sign-date'>{0}</div>", FormatDateTimeDisplay(rec["employee_ack_at"]));
        }
        else
        {
            html.Append("<div class='pa-sign-none'>Not yet acknowledged</div>");
        }
        html.Append("</div>");
        html.Append("</div></div></div>");

        // ── HR Review block ──
        bool hasHrReview = SafeStr(rec["hr_status"]) == "REVIEWED";
        if (hasHrReview)
        {
            string hrOfficer = SafeStr(rec["hr_officer_name"]);
            int    hrRating  = SafeInt(rec["hr_overall_rating"]);
            string hrRec2    = SafeStr(rec["hr_recommendation"]);
            string hrComm    = SafeStr(rec["hr_comments"]);
            string hrSubmAt  = FormatDateTimeDisplay(rec["hr_submitted_at"]);

            html.Append("<div class='pa-detail-section pa-detail-section--hr'>");
            html.Append("<div class='pa-detail-section__hdr'>HR Review &amp; Sign-off</div>");
            html.Append("<div class='pa-detail-section__body'><div class='hw-review-grid'>");
            html.AppendFormat("<div class='pa-info-item'><span class='pa-info-label'>HR Officer</span><span class='pa-info-val'>{0}</span></div>",
                HttpUtility.HtmlEncode(hrOfficer != "" ? hrOfficer : "-"));
            html.AppendFormat("<div class='pa-info-item'><span class='pa-info-label'>Submitted</span><span class='pa-info-val'>{0}</span></div>", hrSubmAt);
            html.AppendFormat("<div class='pa-info-item'><span class='pa-info-label'>HR Overall Rating</span><span class='pa-info-val'>{0}</span></div>",
                hrRating > 0 ? RatingBadge((object)hrRating, staffCategory) : "<span style='color:#999;'>&mdash;</span>");
            html.AppendFormat("<div class='pa-info-item'><span class='pa-info-label'>Recommendation</span><span class='pa-info-val'>{0}</span></div>",
                HttpUtility.HtmlEncode(FormatHrRecommendation(hrRec2)));
            html.Append("</div>");
            if (hrComm != "")
            {
                html.Append("<div style='margin-top:12px;padding-top:12px;border-top:1px solid #e0e5ed;'>");
                html.Append("<div class='pa-info-label' style='margin-bottom:6px;'>HR Comments</div>");
                html.AppendFormat("<div style='font-size:13px;color:#555;line-height:1.5;white-space:pre-wrap;'>{0}</div>",
                    HttpUtility.HtmlEncode(hrComm));
                html.Append("</div>");
            }
            html.Append("</div></div>");
        }

        // ── Admin Actions (HR / administrators only) ──
        html.Append("<div class='pa-actions'>");
        if (!canAdmin)
        {
            html.Append("<span style='font-size:12px;color:#888;'>Read-only: HR or administrator access is required to change this appraisal.</span>");
        }
        else
        {
            html.AppendFormat("<button type='button' class='hr-btn hr-btn--outline' data-rid='{0}' data-reviewer='{1}' onclick='changeSupervisorFromDetailBtn(this)'>",
                rid,
                HttpUtility.HtmlAttributeEncode(liveReviewer != "" ? liveReviewer : "Unassigned"));
            html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M23 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/></svg>");
            html.Append(" Change Supervisor</button>");

            if (status == "COMPLETED" || status == "HR_REVIEWED")
            {
                string hrBtnLabel = status == "HR_REVIEWED" ? "Update HR Review" : "Submit HR Final Input";
                string hrBtnClass = status == "HR_REVIEWED" ? "hr-btn--outline" : "hr-btn--primary";
                html.AppendFormat("<button type='button' class='hr-btn {0}' onclick='openHrWizardFromData()'>", hrBtnClass);
                html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>" +
                    "<path d='M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/>" +
                    "<path d='M23 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/></svg>");
                html.AppendFormat(" {0}</button>", hrBtnLabel);
            }
            if (status == "EMPLOYEE_SUBMITTED" || status == "SUPERVISOR_IN_PROGRESS")
            {
                html.AppendFormat("<button type='button' class='hr-btn hr-btn--warning' onclick='adminReturnToEmployee({0})'>", rid);
                html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='1 4 1 10 7 10'/><path d='M3.51 15a9 9 0 1 0 2.13-9.36L1 10'/></svg>");
                html.Append(" Return to Employee</button>");
            }
            if (status == "COMPLETED" || status == "HR_REVIEWED")
            {
                html.AppendFormat("<button type='button' class='hr-btn hr-btn--outline' onclick='adminReopen({0})'>", rid);
                html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M21 2v6h-6'/><path d='M21 13a9 9 0 1 1-3-7.7L21 8'/></svg>");
                html.Append(" Re-open for Supervisor</button>");
            }
            if (status != "CANCELLED")
            {
                html.AppendFormat("<button type='button' class='hr-btn hr-btn--danger hr-btn--sm' onclick='adminCancel({0})' style='margin-left:auto;'>", rid);
                html.Append("Cancel Record</button>");
            }
        }
        html.Append("</div>");

        // HR wizard JS data
        string jsOfficer = HrOfficerName();
        string ackLine = (ack == "AGREE" || ack == "DISAGREE")
            ? (ack == "AGREE" ? "Agrees" : "Disagrees") + (ackComment != "" ? ": " + ackComment : "")
            : "Not yet acknowledged";
        html.Append("<script type='text/javascript'>");
        html.AppendFormat("window.HR_WIZARD_RID={0};", rid);
        html.AppendFormat("window.HR_WIZARD_EMP=\"{0}\";", EscapeJs(SafeStr(rec["emp_name"])));
        html.AppendFormat("window.HR_WIZARD_META=\"{0}\";", EscapeJs(SafeStr(rec["department"]) + " - " + SafeStr(rec["session_title"])));
        html.AppendFormat("window.HR_WIZARD_SCORE=\"{0}\";", EscapeJs(rawScore));
        html.AppendFormat("window.HR_WIZARD_PCT=\"{0}\";", EscapeJs(finalPct));
        html.AppendFormat("window.HR_WIZARD_CLS=\"{0}\";", EscapeJs(classif));
        html.AppendFormat("window.HR_WIZARD_ACK=\"{0}\";", EscapeJs(ackLine));
        html.AppendFormat("window.HR_WIZARD_OFFICER=\"{0}\";", EscapeJs(jsOfficer));
        html.Append("</script>");

        litDetailContent.Text = html.ToString();
    }

    // ── detail helpers ─────────────────────────────────────────────────
    private static string FirstNonEmpty(string a, string b)
    {
        return !string.IsNullOrEmpty(a) && a.Trim() != "" ? a : (b ?? "");
    }

    private static string MultiLine(string s)
    {
        return HttpUtility.HtmlEncode(s ?? "").Replace("\r\n", "\n").Replace("\n", "<br/>");
    }

    private static string FormatBytes(int n)
    {
        if (n <= 0) return "0 KB";
        if (n < 1024 * 1024) return Math.Max(1, (int)Math.Round(n / 1024.0)) + " KB";
        return (n / 1048576.0).ToString("F1") + " MB";
    }

    /// <summary>JSON escape plus "&lt;/" so a value can never close the inline script block.</summary>
    private string EscapeJs(string s)
    {
        return EscapeJson(s ?? "").Replace("</", "<\\/");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGER
    // ═══════════════════════════════════════════════════════════════════
    private void BuildPager(int current, int totalPages)
    {
        if (totalPages <= 1) { litPager.Text = ""; return; }

        StringBuilder sb = new StringBuilder();
        if (current > 1)
            sb.AppendFormat("<a href='{0}' class='pa-pager__btn'>&laquo;</a>", BuildFilterUrl("page=" + (current - 1)));
        else
            sb.Append("<span class='pa-pager__btn pa-pager__btn--disabled'>&laquo;</span>");

        int startPage = Math.Max(1, current - 2);
        int endPage = Math.Min(totalPages, current + 2);
        for (int i = startPage; i <= endPage; i++)
        {
            if (i == current)
                sb.AppendFormat("<span class='pa-pager__btn pa-pager__btn--active'>{0}</span>", i);
            else
                sb.AppendFormat("<a href='{0}' class='pa-pager__btn'>{1}</a>", BuildFilterUrl("page=" + i), i);
        }

        if (current < totalPages)
            sb.AppendFormat("<a href='{0}' class='pa-pager__btn'>&raquo;</a>", BuildFilterUrl("page=" + (current + 1)));
        else
            sb.Append("<span class='pa-pager__btn pa-pager__btn--disabled'>&raquo;</span>");

        litPager.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private string BuildFilterUrl(string extra)
    {
        StringBuilder sb = new StringBuilder("AppraisalView.aspx?");
        if (!string.IsNullOrEmpty(QsSearch)) sb.AppendFormat("q={0}&", HttpUtility.UrlEncode(QsSearch));
        if (!string.IsNullOrEmpty(QsStatus)) sb.AppendFormat("status={0}&", HttpUtility.UrlEncode(QsStatus));
        if (!string.IsNullOrEmpty(QsCategory)) sb.AppendFormat("cat={0}&", HttpUtility.UrlEncode(QsCategory));
        if (QsSession > 0) sb.AppendFormat("sid={0}&", QsSession);
        if (QsReviewer == "none") sb.Append("rev=none&");
        if (QsPageSize != 25) sb.AppendFormat("ps={0}&", QsPageSize);
        if (!string.IsNullOrEmpty(extra)) sb.Append(extra);
        return sb.ToString().TrimEnd('&');
    }

    private string FormatHrRecommendation(string rec)
    {
        switch ((rec ?? "").ToUpper())
        {
            case "CONFIRM":          return "Confirm Appointment";
            case "EXTEND_PROBATION": return "Extend Probation";
            case "PIP":              return "Performance Improvement Plan";
            case "PROMOTE":          return "Promote";
            case "OTHER":            return "Other";
            default:                 return rec != "" ? rec : "—";
        }
    }

    private string RatingDisplay(object val)
    {
        if (val == null || val == DBNull.Value) return "<span style='color:#ccc;'>—</span>";
        int rating;
        if (!int.TryParse(val.ToString(), out rating)) return SafeStr(val);
        string color = rating >= 4 ? "#28a745" : rating >= 3 ? "#f59e0b" : rating >= 2 ? "#fd7e14" : "#dc3545";
        return string.Format("<span style='font-weight:700;color:{0};'>{1}</span>", color, rating);
    }

    private string RatingLabel(int rating, string staffCategory)
    {
        if ((staffCategory ?? "").ToUpper() == "ACADEMIC")
        {
            switch (rating)
            {
                case 1: return "Unsatisfactory";
                case 2: return "Development Needed";
                case 3: return "Satisfactory";
                case 4: return "Above Expectations";
                case 5: return "Exceptional";
                default: return "";
            }
        }
        else
        {
            switch (rating)
            {
                case 1: return "Poor";
                case 2: return "Fair";
                case 3: return "Good";
                case 4: return "Very Good";
                case 5: return "Excellent";
                default: return "";
            }
        }
    }

    /// <summary>Item rating scale: Academic vs Administrative &amp; Support.</summary>
    private string RatingLegend(string staffCategory)
    {
        return (staffCategory ?? "").ToUpper() == "ACADEMIC"
            ? "5 = Exceptional &nbsp;&nbsp; 4 = Above Expectations &nbsp;&nbsp; 3 = Satisfactory &nbsp;&nbsp; 2 = Development Needed &nbsp;&nbsp; 1 = Unsatisfactory"
            : "5 = Excellent &nbsp;&nbsp; 4 = Very Good &nbsp;&nbsp; 3 = Good &nbsp;&nbsp; 2 = Fair &nbsp;&nbsp; 1 = Poor";
    }

    /// <summary>
    /// The one overall classification scale - mirrors SQL appraisal_classify():
    /// 90 Exceptional, 75 Above Expectations, 60 Satisfactory, 50 Development Needed,
    /// else Unsatisfactory. Derived from the percentage so older records stored under
    /// retired labels display on the same scale.
    /// </summary>
    private static string Classify(object pct, string fallback)
    {
        if (pct == null || pct == DBNull.Value) return fallback ?? "";
        decimal d;
        if (!decimal.TryParse(pct.ToString(), out d)) return fallback ?? "";
        if (d >= 90) return "Exceptional";
        if (d >= 75) return "Above Expectations";
        if (d >= 60) return "Satisfactory";
        if (d >= 50) return "Development Needed";
        return "Unsatisfactory";
    }

    private static string ScoreColor(object pct)
    {
        if (pct == null || pct == DBNull.Value) return "#888";
        decimal d;
        if (!decimal.TryParse(pct.ToString(), out d)) return "#888";
        if (d >= 75) return "#155724";
        if (d >= 60) return "#174DA4";
        if (d >= 50) return "#856404";
        return "#721c24";
    }

    private string RatingBadge(object val, string staffCategory)
    {
        if (val == null || val == DBNull.Value) return "<span style='color:#ccc;'>—</span>";
        int rating;
        if (!int.TryParse(val.ToString(), out rating) || rating <= 0)
            return "<span style='color:#ccc;'>—</span>";
        string label = RatingLabel(rating, staffCategory);
        string[] colors = new string[] { "", "#dc3545", "#fd7e14", "#856404", "#0d6efd", "#28a745" };
        string[] bgs    = new string[] { "", "#f8d7da", "#ffd9b0", "#fff3cd", "#cce5ff", "#d4edda" };
        string color = (rating >= 1 && rating <= 5) ? colors[rating] : "#888";
        string bg    = (rating >= 1 && rating <= 5) ? bgs[rating]    : "#eee";
        return string.Format(
            "<span style='display:inline-flex;align-items:center;gap:4px;background:{0};color:{1};" +
            "padding:2px 8px;border-radius:3px;font-size:11px;font-weight:700;white-space:nowrap;'>" +
            "<strong>{2}</strong><span style='font-weight:400;font-size:10px;opacity:.85;'>{3}</span></span>",
            bg, color, rating, label);
    }

    private void AppendInfoItem(StringBuilder html, string label, string value)
    {
        html.AppendFormat(
            "<div class='pa-info-item'><span class='pa-info-label'>{0}</span>" +
            "<span class='pa-info-val'>{1}</span></div>",
            HttpUtility.HtmlEncode(label),
            HttpUtility.HtmlEncode(value != "" ? value : "—"));
    }

    private string FormatStatusLabel(string status)
    {
        switch (status.ToUpper())
        {
            case "PENDING":                return "Not Started";
            case "EMPLOYEE_IN_PROGRESS":   return "Employee In Progress";
            case "RETURNED":               return "Returned";
            case "EMPLOYEE_SUBMITTED":     return "Employee Submitted";
            case "SUPERVISOR_IN_PROGRESS": return "Supervisor Reviewing";
            case "COMPLETED":              return "Awaiting HR Review";
            case "HR_REVIEWED":            return "HR Reviewed";
            case "CANCELLED":              return "Cancelled";
            default:                       return status;
        }
    }

    private string GetRecordBadgeModifier(string status)
    {
        switch (status.ToUpper())
        {
            case "PENDING":                return "pending";
            case "EMPLOYEE_IN_PROGRESS":   return "emp-prog";
            case "EMPLOYEE_SUBMITTED":     return "emp-done";
            case "SUPERVISOR_IN_PROGRESS": return "sup-prog";
            case "COMPLETED":              return "completed";
            case "HR_REVIEWED":            return "hr-reviewed";
            case "RETURNED":               return "returned";
            case "CANCELLED":              return "cancelled";
            default:                       return "pending";
        }
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        // TINYINT(1) columns (is_na, is_active, is_catalogue) arrive as bool with this connection string.
        if (val is bool) return (bool)val ? 1 : 0;
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
        if (decimal.TryParse(val.ToString(), out d))
            return d.ToString("F1");
        return val.ToString();
    }

    private string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("yyyy-MM-dd");
        return val.ToString();
    }

    private string FormatDateTime(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        return val.ToString();
    }

    private string FormatDateDisplay(object val)
    {
        if (val == null || val == DBNull.Value) return "—";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("dd MMM yyyy");
        return val.ToString();
    }

    private string FormatDateTimeDisplay(object val)
    {
        if (val == null || val == DBNull.Value) return "—";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("dd MMM yyyy HH:mm");
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
        {
            return string.Format("IFNULL(NULLIF(TRIM({0}.department),''), 'Unassigned')", employeeAlias);
        }

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_departments") && ColumnExists(conn, "hrm_emp_contracts", "departmentID"))
        {
            string deptColumn = ColumnExists(conn, "hrm_departments", "dept_name")
                ? "dept_name"
                : (ColumnExists(conn, "hrm_departments", "department") ? "department" : "");

            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart"
                : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");

            if (!string.IsNullOrEmpty(deptColumn))
            {
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT d.{0} FROM hrm_emp_contracts c LEFT JOIN hrm_departments d ON c.departmentID = d.ID WHERE c.empID = {1}.empID ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC LIMIT 1)),''), 'Unassigned')",
                    deptColumn,
                    employeeAlias,
                    sortColumn);
            }
        }

        return "'Unassigned'";
    }

    private string BuildDesignationSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "designation"))
        {
            return string.Format("IFNULL(NULLIF(TRIM({0}.designation),''), '')", employeeAlias);
        }

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_jobs") && ColumnExists(conn, "hrm_emp_contracts", "jobID"))
        {
            string jobColumn = ColumnExists(conn, "hrm_jobs", "jobname")
                ? "jobname"
                : (ColumnExists(conn, "hrm_jobs", "job_name") ? "job_name" : "");

            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart"
                : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");

            if (!string.IsNullOrEmpty(jobColumn))
            {
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT j.{0} FROM hrm_emp_contracts c LEFT JOIN hrm_jobs j ON c.jobID = j.ID WHERE c.empID = {1}.empID ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC LIMIT 1)),''), '')",
                    jobColumn,
                    employeeAlias,
                    sortColumn);
            }
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

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: GET EMPLOYEES (for Change Supervisor dropdown)
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

            StringBuilder sb = new StringBuilder("{\"employees\":[");
            bool first = true;
            foreach (DataRow r in dt.Rows)
            {
                if (!first) sb.Append(",");
                sb.AppendFormat("{{\"id\":{0},\"name\":\"{1}\",\"code\":\"{2}\"}}",
                    SafeInt(r["empID"]),
                    EscapeJson(r["emp_name"]),
                    EscapeJson(r["EMP_CODE"]));
                first = false;
            }
            sb.Append("]}");
            Response.Write(sb.ToString());
        }
        catch (Exception ex)
        {
            Response.Write("{\"employees\":[],\"error\":\"" + EscapeJson(ex.Message) + "\"}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: CHANGE SUPERVISOR (single + bulk)
    // ═══════════════════════════════════════════════════════════════════
    private string ReviewerName(int empId)
    {
        DataTable dtEmp = ExecuteQuery("SELECT emp_name FROM hrm_employee WHERE empID = @eid", new MySqlParameter("@eid", empId));
        return dtEmp.Rows.Count == 0 ? null : SafeStr(dtEmp.Rows[0]["emp_name"]);
    }

    private void AjaxChangeSupervisor()
    {
        try
        {
            var data = ReadJsonBody();
            int rid = Int(data, "rid");
            int newReviewerId = Int(data, "new_reviewer_id");

            if (newReviewerId <= 0)
            { Response.Write("{\"ok\":false,\"error\":\"Please select a valid supervisor.\"}"); return; }

            string newName = ReviewerName(newReviewerId);
            if (newName == null)
            { Response.Write("{\"ok\":false,\"error\":\"Selected employee not found.\"}"); return; }

            string err = DoChangeReviewer(rid, newReviewerId, newName);
            if (err != "") { Response.Write("{\"ok\":false,\"error\":\"" + EscapeJson(err) + "\"}"); return; }

            Response.Write("{\"ok\":true,\"message\":\"Supervisor changed to " + EscapeJson(newName) + ".\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }

    /// <summary>Bulk "Assign reviewer" - used by HR to clear records that have no reviewer.</summary>
    private void AjaxBatchChangeSupervisor()
    {
        try
        {
            var data = ReadJsonBody();
            var rids = RidsFrom(data);
            int newReviewerId = Int(data, "new_reviewer_id");
            if (newReviewerId <= 0)
            { Response.Write("{\"ok\":false,\"error\":\"Please select a valid supervisor.\"}"); return; }
            if (rids.Count == 0)
            { Response.Write("{\"ok\":false,\"error\":\"No records selected.\"}"); return; }

            string newName = ReviewerName(newReviewerId);
            if (newName == null)
            { Response.Write("{\"ok\":false,\"error\":\"Selected employee not found.\"}"); return; }

            int count = 0, selfSkipped = 0, otherSkipped = 0;
            foreach (int rid in rids)
            {
                string err = DoChangeReviewer(rid, newReviewerId, newName);
                if (err == "") count++;
                else if (err.StartsWith("An employee cannot")) selfSkipped++;
                else otherSkipped++;
            }
            string msg = count + " record(s) assigned to " + newName + ".";
            if (selfSkipped > 0) msg += " " + selfSkipped + " skipped (would review themselves).";
            if (otherSkipped > 0) msg += " " + otherSkipped + " skipped (already assigned / not found).";
            Response.Write("{\"ok\":true,\"count\":" + count + ",\"message\":\"" + EscapeJson(msg) + "\"}");
        }
        catch (Exception ex) { WriteError(ex); }
    }
}
