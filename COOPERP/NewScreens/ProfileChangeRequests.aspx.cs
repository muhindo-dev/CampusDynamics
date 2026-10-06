using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Profile change requests. Staff ask in the portal (MyProfile.aspx) to correct details that drive
/// payroll, contracts or appraisal; HR reviews them here. One row in hrm_profile_change_requests per
/// request, changes_json = [{field,label,old,old_text,new,new_text}].
/// Approve applies the ticked fields in one transaction. A field whose record value no longer equals
/// the "old" value the staff member saw is shown as changed since the request, is not ticked by
/// default, and is applied only when HR ticks it (the client sends it in "ack"; the server rechecks).
/// ?ajax=detail (GET) returns one request; ?ajax=approve / ?ajax=reject (POST, JSON body, CSRF header).
/// </summary>
public partial class COOPERP_NewScreens_ProfileChangeRequests : System.Web.UI.Page
{
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    private string ConnStr
    {
        get { return System.Configuration.ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ─── Field rules ───────────────────────────────────────────────────────────
    private enum FKind { Text, Date, Gender, Bank, Dept, Job, EmpType }

    private class FieldDef
    {
        public string Key, Column, Label; public FKind Kind; public int MaxLen; public bool AllowEmpty;
        public FieldDef(string key, string column, string label, FKind kind, int maxLen, bool allowEmpty)
        { Key = key; Column = column; Label = label; Kind = kind; MaxLen = maxLen; AllowEmpty = allowEmpty; }
    }

    private static readonly Dictionary<string, FieldDef> Fields = BuildFields();
    private static Dictionary<string, FieldDef> BuildFields()
    {
        Dictionary<string, FieldDef> d = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase);
        d["emp_name"]        = new FieldDef("emp_name", "emp_name", "Name", FKind.Text, 450, false);
        d["emp_birthdate"]   = new FieldDef("emp_birthdate", "emp_birthdate", "Date of birth", FKind.Date, 0, false);
        d["gender"]          = new FieldDef("gender", "gender", "Gender", FKind.Gender, 20, false);
        d["emp_nationality"] = new FieldDef("emp_nationality", "emp_nationality", "Nationality", FKind.Text, 45, true);
        d["nin"]             = new FieldDef("nin", "nin", "NIN", FKind.Text, 50, true);
        d["tin"]             = new FieldDef("tin", "tin", "TIN", FKind.Text, 45, true);
        d["nssf_no"]         = new FieldDef("nssf_no", "nssf_no", "NSSF number", FKind.Text, 45, true);
        d["bankAccount"]     = new FieldDef("bankAccount", "bankAccount", "Bank account", FKind.Text, 45, true);
        d["bankID"]          = new FieldDef("bankID", "bankID", "Bank", FKind.Bank, 0, false);
        d["dept_id"]         = new FieldDef("dept_id", "dept_id", "Department", FKind.Dept, 0, false);
        d["job_id"]          = new FieldDef("job_id", "jobID", "Position", FKind.Job, 0, false);
        d["EmpType"]         = new FieldDef("EmpType", "EmpType", "Staff category", FKind.EmpType, 25, false);
        return d;
    }

    private static readonly string[] Statuses = { "PENDING", "APPROVED", "PARTLY_APPROVED", "REJECTED", "WITHDRAWN" };

    // ─── Query string ──────────────────────────────────────────────────────────
    private int    QsPage   { get { int v; return int.TryParse(Request.QueryString["page"] ?? "1", out v) && v > 0 ? v : 1; } }
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }
    private string QsStatus { get { string s = (Request.QueryString["status"] ?? "").Trim().ToUpperInvariant(); return Array.IndexOf(Statuses, s) >= 0 ? s : ""; } }
    private const int PageSize = 50;

    protected string SearchValue = "";

    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLowerInvariant();
        if (ajax != "")
        {
            if (!HrAccess.RequireHr(true)) return;
            HandleAjax(ajax);
            return;
        }

        if (!HrAccess.RequireHr(false)) return;

        string export = (Request.QueryString["export"] ?? "").Trim().ToLowerInvariant();
        if (export == "xlsx" || export == "csv") { SendExport(export); return; }

        SearchValue = QsSearch;
        LoadStatusOptions();
        LoadKpis();
        BindGrid();
        ShowFlashMessage();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  List
    // ═══════════════════════════════════════════════════════════════════════════
    private void LoadStatusOptions()
    {
        StringBuilder sb = new StringBuilder();
        Opt(sb, "", "All statuses", QsStatus == "");
        foreach (string s in Statuses) Opt(sb, s, StatusWord(s), QsStatus == s);
        litStatusOptions.Text = sb.ToString();
    }

    private static void Opt(StringBuilder sb, string value, string text, bool selected)
    {
        sb.Append("<option value=\"").Append(HttpUtility.HtmlAttributeEncode(value)).Append("\"")
          .Append(selected ? " selected=\"selected\"" : "").Append(">")
          .Append(HttpUtility.HtmlEncode(text)).Append("</option>");
    }

    private void LoadKpis()
    {
        DataTable dt = Q(
            @"SELECT SUM(status = 'PENDING') AS pending,
                     SUM(status IN ('APPROVED','PARTLY_APPROVED') AND reviewed_at >= DATE_FORMAT(CURDATE(), '%Y-%m-01')) AS approved_m,
                     SUM(status = 'REJECTED' AND reviewed_at >= DATE_FORMAT(CURDATE(), '%Y-%m-01')) AS rejected_m
              FROM hrm_profile_change_requests");
        DataRow r = dt.Rows.Count > 0 ? dt.Rows[0] : null;
        StringBuilder sb = new StringBuilder("<div class=\"hr-kpis\">");
        sb.Append("<a class=\"hr-kpi").Append(QsStatus == "PENDING" ? " pcr-kpi--on" : "").Append("\" href=\"ProfileChangeRequests.aspx?status=PENDING\">")
          .Append("<div class=\"hr-kpi__label\">Pending</div><div class=\"hr-kpi__value\">").Append(N(r == null ? null : r["pending"]))
          .Append("</div><div class=\"hr-kpi__sub\">Awaiting HR</div></a>");
        sb.Append("<div class=\"hr-kpi\"><div class=\"hr-kpi__label\">Approved this month</div><div class=\"hr-kpi__value\">").Append(N(r == null ? null : r["approved_m"]))
          .Append("</div><div class=\"hr-kpi__sub\">Fully or partly</div></div>");
        sb.Append("<div class=\"hr-kpi\"><div class=\"hr-kpi__label\">Rejected this month</div><div class=\"hr-kpi__value\">").Append(N(r == null ? null : r["rejected_m"]))
          .Append("</div><div class=\"hr-kpi__sub\">With a note to the staff member</div></div>");
        sb.Append("</div>");
        litKpis.Text = sb.ToString();
    }

    private const string ListFrom =
        @" FROM hrm_profile_change_requests r
           LEFT JOIN hrm_employee e ON e.empID = r.emp_id
           LEFT JOIN hrm_departments d ON d.ID = NULLIF(e.dept_id, 0)
           LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
           LEFT JOIN hrm_departments cd ON cd.ID = c.departmentID ";

    private const string ListCols =
        @"SELECT r.request_id, r.emp_id, r.status, r.changes_json, r.submitted_at, r.reviewed_by, r.reviewed_at,
                 r.review_note, r.applied_json, e.EMP_CODE, e.emp_name, COALESCE(d.dept_name, cd.dept_name) AS dept_name ";

    private string Where(List<MySqlParameter> parms)
    {
        StringBuilder w = new StringBuilder(" WHERE 1=1");
        if (QsStatus != "") { w.Append(" AND r.status = @st"); parms.Add(new MySqlParameter("@st", QsStatus)); }
        if (QsSearch != "")
        {
            w.Append(" AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }
        return w.ToString();
    }

    private const string ListOrder = " ORDER BY (r.status = 'PENDING') DESC, r.submitted_at DESC, r.request_id DESC";

    private void BindGrid()
    {
        int total = 0, page = QsPage;
        try
        {
            List<MySqlParameter> parms = new List<MySqlParameter>();
            string where = Where(parms);
            DataTable cnt = Q("SELECT COUNT(*)" + ListFrom + where, Clone(parms));
            total = Convert.ToInt32(cnt.Rows[0][0]);
            int pages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            if (page > pages) page = pages;
            int offset = (page - 1) * PageSize;

            List<MySqlParameter> p2 = Clone(parms);
            p2.Add(new MySqlParameter("@lim", PageSize));
            p2.Add(new MySqlParameter("@off", offset));
            DataTable dt = Q(ListCols + ListFrom + where + ListOrder + " LIMIT @lim OFFSET @off", p2);

            StringBuilder sb = new StringBuilder();
            foreach (DataRow r in dt.Rows)
            {
                List<Dictionary<string, object>> ch = ParseChanges(Str(r["changes_json"]));
                List<string> labels = new List<string>();
                foreach (Dictionary<string, object> c in ch) labels.Add(LabelOf(c));
                sb.Append("<tr>")
                  .Append("<td>").Append(Enc(Str(r["EMP_CODE"]) == "-" ? "" : Str(r["EMP_CODE"]))).Append("</td>")
                  .Append("<td>").Append(r["emp_name"] == DBNull.Value ? "Not recorded" : Enc(Str(r["emp_name"]))).Append("</td>")
                  .Append("<td>").Append(Enc(Str(r["dept_name"]))).Append("</td>")
                  .Append("<td>").Append(Enc(string.Join(", ", labels.ToArray()))).Append("</td>")
                  .Append("<td style='white-space:nowrap'>").Append(DT(r["submitted_at"])).Append("</td>")
                  .Append("<td>").Append(Badge(Str(r["status"]))).Append("</td>")
                  .Append("<td class='hr-right'><button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick='openRequest(")
                  .Append(Convert.ToInt32(r["request_id"])).Append(")'>Open</button></td>")
                  .Append("</tr>");
            }
            if (sb.Length == 0) sb.Append("<tr><td colspan='7' class='hr-empty'>No requests match these filters.</td></tr>");
            litGridBody.Text = sb.ToString();
            litPager.Text = Pager(page, pages);
            litPagerInfo.Text = total == 0 ? "No records" :
                string.Format("{0} to {1} of {2}", offset + 1, Math.Min(offset + PageSize, total), total.ToString("N0"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("ProfileChangeRequests grid: " + ex);
            litGridBody.Text = "<tr><td colspan='7' class='hr-empty'>Requests could not be loaded. Refresh the page, and contact MIS if it keeps happening.</td></tr>";
        }
        litTotalCount.Text = total == 1 ? "1 request" : total.ToString("N0") + " requests";
        litExport.Text =
            "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" + HttpUtility.HtmlAttributeEncode(Url("export=xlsx", false)) + "\">" + IconDownload + "Excel</a>" +
            "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" + HttpUtility.HtmlAttributeEncode(Url("export=csv", false)) + "\">CSV</a>";
    }

    private string Pager(int page, int pages)
    {
        if (pages <= 1) return "";
        StringBuilder sb = new StringBuilder("<div class='hr-pager'>");
        sb.AppendFormat("<span>Page {0} of {1}</span>", page, pages);
        sb.Append(PagerBtn("First", 1, page > 1)).Append(PagerBtn("Previous", page - 1, page > 1))
          .Append(PagerBtn("Next", page + 1, page < pages)).Append(PagerBtn("Last", pages, page < pages));
        return sb.Append("</div>").ToString();
    }

    private string PagerBtn(string label, int target, bool enabled)
    {
        if (!enabled) return "<button type='button' disabled='disabled'>" + label + "</button>";
        return "<button type='button' onclick=\"location.href='" + HttpUtility.JavaScriptStringEncode(Url("page=" + target, false)) + "'\">" + label + "</button>";
    }

    private string Url(string extra, bool keepPage)
    {
        List<string> p = new List<string>();
        if (QsStatus != "") p.Add("status=" + QsStatus);
        if (QsSearch != "") p.Add("q=" + HttpUtility.UrlEncode(QsSearch));
        if (keepPage && QsPage > 1) p.Add("page=" + QsPage);
        if (!string.IsNullOrEmpty(extra)) p.Add(extra);
        return "ProfileChangeRequests.aspx" + (p.Count > 0 ? "?" + string.Join("&", p.ToArray()) : "");
    }

    private void ShowFlashMessage()
    {
        string msg = (Request.QueryString["msg"] ?? "").Trim();
        if (msg != "")
        {
            bool ok = (Request.QueryString["ok"] ?? "") == "1";
            ScriptManager.RegisterStartupScript(this, GetType(), "flash",
                "showToast('" + HttpUtility.JavaScriptStringEncode(HrExport.Clean(msg)) + "'," + (ok ? "false" : "true") + ");", true);
        }
        int open;
        if (int.TryParse(Request.QueryString["id"], out open) && open > 0)
            ScriptManager.RegisterStartupScript(this, GetType(), "openReq", "openRequest(" + open + ");", true);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Export: one row per requested field
    // ═══════════════════════════════════════════════════════════════════════════
    private void SendExport(string fmt)
    {
        HrExport.Report rep = new HrExport.Report("Profile change requests", "profile-change-requests");
        rep.PreparedBy = HrAccess.Username();
        rep.AddScope("Status", QsStatus == "" ? "" : StatusWord(QsStatus));
        rep.AddScope("Search", QsSearch);

        HrExport.Sheet sh = rep.NewSheet("Requests");
        sh.Add("Staff no").Add("Name").Add("Field").Add("Current").Add("Requested").Add("Status")
          .Add("Submitted", HrExport.Kind.DateTime).Add("Reviewed by").Add("Reviewed on", HrExport.Kind.DateTime).Add("Note");

        List<MySqlParameter> parms = new List<MySqlParameter>();
        DataTable dt = Q(ListCols + ListFrom + Where(parms) + ListOrder, parms);
        foreach (DataRow r in dt.Rows)
        {
            Dictionary<string, string> appliedNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, object> a in ParseChanges(Str(r["applied_json"])))
            {
                string f = S(a, "field");
                bool applied = a.ContainsKey("applied") && a["applied"] is bool && (bool)a["applied"];
                if (f != "" && !applied) appliedNotes[f] = "Not applied: " + S(a, "note");
            }
            string code = Str(r["EMP_CODE"]) == "-" ? "" : Str(r["EMP_CODE"]);
            foreach (Dictionary<string, object> c in ParseChanges(Str(r["changes_json"])))
            {
                string note = Str(r["review_note"]);
                string fn;
                if (appliedNotes.TryGetValue(S(c, "field"), out fn)) note = note == "" ? fn : note + ". " + fn;
                sh.Row(code, Str(r["emp_name"]), LabelOf(c), OldText(c), NewText(c), StatusWord(Str(r["status"])),
                    r["submitted_at"], Str(r["reviewed_by"]), r["reviewed_at"], note);
            }
        }

        if (fmt == "csv") HrExport.SendCsv(Response, rep, 0);
        else HrExport.SendXlsx(Response, rep);
        Response.End();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  AJAX
    // ═══════════════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        string json;
        try
        {
            if (action == "detail") json = AjaxDetail();
            else if (action == "approve" || action == "reject")
            {
                if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) || !MarksAntiForgeryService.ValidateRequest())
                    json = Fail("The page has expired. Reload it and try again.");
                else
                {
                    Dictionary<string, object> body = ReadBody();
                    json = action == "approve" ? AjaxApprove(body) : AjaxReject(body);
                }
            }
            else json = Fail("Unknown action.");
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("ProfileChangeRequests " + action + ": " + ex);
            json = Fail("The action could not be completed. Reload the page and try again.");
        }
        Response.Write(json);
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private Dictionary<string, object> ReadBody()
    {
        string raw;
        using (StreamReader rd = new StreamReader(Request.InputStream, Encoding.UTF8)) raw = rd.ReadToEnd();
        try
        {
            Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(raw);
            return d ?? new Dictionary<string, object>();
        }
        catch { return new Dictionary<string, object>(); }
    }

    private static string Fail(string msg)
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = false; d["msg"] = msg;
        return new JavaScriptSerializer().Serialize(d);
    }

    // ── employee state ─────────────────────────────────────────────────────────
    private class EmpState
    {
        public bool Found; public int ContractId;
        public string Code = "", Name = "", DeptName = "", JobName = "", Category = "";
        public Dictionary<string, string> Raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private const string StateSql =
        @"SELECT e.empID, e.EMP_CODE, e.emp_name, e.emp_birthdate, e.gender, e.emp_nationality, e.nin, e.tin, e.nssf_no,
                 e.bankAccount, e.bankID, e.dept_id, e.EmpType,
                 c.ID AS contract_id, c.jobID, c.departmentID,
                 b.bank_name, d.dept_name AS emp_dept, cd.dept_name AS con_dept, j.jobname
          FROM hrm_employee e
          LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
          LEFT JOIN banks b ON b.bank_id = e.bankID
          LEFT JOIN hrm_departments d ON d.ID = NULLIF(e.dept_id, 0)
          LEFT JOIN hrm_departments cd ON cd.ID = c.departmentID
          LEFT JOIN hrm_jobs j ON j.ID = c.jobID
          WHERE e.empID = @e";

    private static EmpState ReadState(DataTable dt)
    {
        EmpState s = new EmpState();
        if (dt.Rows.Count == 0) return s;
        DataRow r = dt.Rows[0];
        s.Found = true;
        s.ContractId = r["contract_id"] == DBNull.Value ? 0 : Convert.ToInt32(r["contract_id"]);
        s.Code = Str(r["EMP_CODE"]) == "-" ? "" : Str(r["EMP_CODE"]);
        s.Name = Str(r["emp_name"]);
        s.Category = Str(r["EmpType"]);

        foreach (string k in new string[] { "emp_name", "gender", "emp_nationality", "nin", "tin", "nssf_no", "bankAccount", "EmpType" })
        {
            s.Raw[k] = Str(r[k]);
            s.Text[k] = Str(r[k]) == "-" ? "" : Str(r[k]);
        }
        DateTime bd;
        if (r["emp_birthdate"] != DBNull.Value && DateTime.TryParse(r["emp_birthdate"].ToString(), out bd) && bd.Year > 1900)
        { s.Raw["emp_birthdate"] = bd.ToString("yyyy-MM-dd", IC); s.Text["emp_birthdate"] = bd.ToString("d MMM yyyy", IC); }
        else { s.Raw["emp_birthdate"] = ""; s.Text["emp_birthdate"] = ""; }

        s.Raw["bankID"] = IdStr(r["bankID"]); s.Text["bankID"] = Str(r["bank_name"]);

        string empDept = IdStr(r["dept_id"]);
        if (empDept != "") { s.Raw["dept_id"] = empDept; s.DeptName = Str(r["emp_dept"]); }
        else { s.Raw["dept_id"] = IdStr(r["departmentID"]); s.DeptName = Str(r["con_dept"]); }
        s.Text["dept_id"] = s.DeptName;

        s.Raw["job_id"] = IdStr(r["jobID"]); s.JobName = Str(r["jobname"]); s.Text["job_id"] = s.JobName;
        return s;
    }

    private static string IdStr(object v)
    {
        long n;
        if (v == null || v == DBNull.Value || !long.TryParse(v.ToString().Trim(), out n) || n <= 0) return "";
        return n.ToString(IC);
    }

    /// <summary>Comparable form of a value: blanks and "-" are equal, ids lose zero, dates become yyyy-MM-dd.</summary>
    private static string Norm(string field, string v)
    {
        v = (v ?? "").Trim();
        FieldDef def;
        if (!Fields.TryGetValue(field, out def)) return v;
        switch (def.Kind)
        {
            case FKind.Bank: case FKind.Dept: case FKind.Job: return IdStr(v);
            case FKind.Date:
                DateTime d;
                if (v != "" && DateTime.TryParseExact(v.Length >= 10 ? v.Substring(0, 10) : v, "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) return d.ToString("yyyy-MM-dd", IC);
                if (v != "" && DateTime.TryParse(v, IC, DateTimeStyles.None, out d) && d.Year > 1900) return d.ToString("yyyy-MM-dd", IC);
                return "";
            case FKind.Gender: case FKind.EmpType: return v == "-" ? "" : v.ToUpperInvariant();
            default: return v == "-" ? "" : v;
        }
    }

    private static bool ChangedSince(EmpState st, Dictionary<string, object> c)
    {
        string f = S(c, "field");
        string cur;
        if (!st.Raw.TryGetValue(f, out cur)) cur = "";
        return Norm(f, cur) != Norm(f, S(c, "old"));
    }

    // ── detail ─────────────────────────────────────────────────────────────────
    private string AjaxDetail()
    {
        int id; int.TryParse(Request.QueryString["id"], out id);
        DataTable dt = Q("SELECT * FROM hrm_profile_change_requests WHERE request_id = @id", new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0) return Fail("This request was not found.");
        DataRow r = dt.Rows[0];
        int empId = Convert.ToInt32(r["emp_id"]);
        EmpState st = ReadState(Q(StateSql, new MySqlParameter("@e", empId)));
        string status = Str(r["status"]);

        Dictionary<string, string> appliedNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, bool> appliedFlags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (Dictionary<string, object> a in ParseChanges(Str(r["applied_json"])))
        {
            string f = S(a, "field");
            if (f == "") continue;
            appliedNotes[f] = S(a, "note");
            appliedFlags[f] = a.ContainsKey("applied") && a["applied"] is bool && (bool)a["applied"];
        }

        List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
        foreach (Dictionary<string, object> c in ParseChanges(Str(r["changes_json"])))
        {
            string f = S(c, "field");
            Dictionary<string, object> o = new Dictionary<string, object>();
            o["field"] = f;
            o["label"] = LabelOf(c);
            string cur;
            o["current"] = st.Text.TryGetValue(f, out cur) ? HrExport.Clean(cur) : "";
            o["old"] = HrExport.Clean(OldText(c));
            o["requested"] = HrExport.Clean(NewText(c));
            bool known = Fields.ContainsKey(f);
            bool changed = st.Found && known && ChangedSince(st, c);
            o["changed"] = changed;
            string block = "";
            if (!known) block = "This field cannot be changed here";
            else if (!st.Found) block = "Employee not found";
            else if (Fields[f].Kind == FKind.Job && st.ContractId == 0) block = "No contract on record";
            o["blocked"] = block;
            if (appliedFlags.ContainsKey(f)) { o["applied"] = appliedFlags[f]; o["applied_note"] = appliedNotes[f]; }
            rows.Add(o);
        }

        Dictionary<string, object> res = new Dictionary<string, object>();
        res["ok"] = true;
        res["id"] = Convert.ToInt32(r["request_id"]);
        res["status"] = status;
        res["status_word"] = StatusWord(status);
        res["status_kind"] = BadgeKind(status);
        res["pending"] = status == "PENDING";
        res["emp_name"] = st.Found ? HrExport.Clean(st.Name) : "Not recorded";
        res["emp_code"] = st.Code;
        res["dept"] = HrExport.Clean(st.DeptName);
        res["job"] = HrExport.Clean(st.JobName);
        res["category"] = st.Category;
        res["reason"] = HrExport.Clean(Str(r["reason"]));
        res["submitted"] = DT(r["submitted_at"]);
        res["reviewed_by"] = Str(r["reviewed_by"]);
        res["reviewed_at"] = DT(r["reviewed_at"]);
        res["review_note"] = HrExport.Clean(Str(r["review_note"]));
        res["rows"] = rows;
        return new JavaScriptSerializer().Serialize(res);
    }

    // ── approve ────────────────────────────────────────────────────────────────
    private string AjaxApprove(Dictionary<string, object> body)
    {
        int id = BodyInt(body, "id");
        HashSet<string> ticked = BodySet(body, "fields");
        HashSet<string> ack = BodySet(body, "ack");
        string note = HrExport.Clean(BodyStr(body, "note"));
        if (note.Length > 2000) note = note.Substring(0, 2000);
        if (ticked.Count == 0) return Fail("Tick at least one field to approve, or reject the request.");

        string user = HrAccess.Username();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                DataTable req = TQ(conn, tx, "SELECT request_id, emp_id, status, changes_json FROM hrm_profile_change_requests WHERE request_id = @id FOR UPDATE",
                    new MySqlParameter("@id", id));
                if (req.Rows.Count == 0) { tx.Rollback(); return Fail("This request was not found."); }
                if (Str(req.Rows[0]["status"]) != "PENDING") { tx.Rollback(); return Fail("This request has already been decided. Reload the page."); }
                int empId = Convert.ToInt32(req.Rows[0]["emp_id"]);
                EmpState st = ReadState(TQ(conn, tx, StateSql, new MySqlParameter("@e", empId)));
                if (!st.Found) { tx.Rollback(); return Fail("The employee on this request was not found."); }

                List<Dictionary<string, object>> changes = ParseChanges(Str(req.Rows[0]["changes_json"]));
                List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
                List<string> appliedLabels = new List<string>(), problems = new List<string>();
                int applied = 0;

                // Check every ticked field before writing anything.
                foreach (Dictionary<string, object> c in changes)
                {
                    string f = S(c, "field");
                    if (!ticked.Contains(f)) continue;
                    if (Fields.ContainsKey(f) && ChangedSince(st, c) && !ack.Contains(f))
                    {
                        tx.Rollback();
                        return Fail(LabelOf(c) + " has changed on the record since you opened this request. Open it again and check the fields.");
                    }
                }

                foreach (Dictionary<string, object> c in changes)
                {
                    string f = S(c, "field");
                    Dictionary<string, object> res = new Dictionary<string, object>();
                    res["field"] = f;
                    string outNote;
                    bool ok;
                    if (!ticked.Contains(f)) { ok = false; outNote = "Not selected"; }
                    else ok = ApplyField(conn, tx, empId, st, c, out outNote);
                    res["applied"] = ok;
                    res["note"] = outNote;
                    results.Add(res);
                    if (ok) { applied++; appliedLabels.Add(LabelOf(c)); }
                    else if (ticked.Contains(f)) problems.Add(LabelOf(c) + ": " + outNote);
                }

                if (applied == 0)
                {
                    tx.Rollback();
                    return Fail("None of the selected fields could be applied. " + string.Join(". ", problems.ToArray()) + ".");
                }

                string status = applied == changes.Count ? "APPROVED" : "PARTLY_APPROVED";
                TX(conn, tx,
                    @"UPDATE hrm_profile_change_requests
                      SET status = @st, reviewed_by = @u, reviewed_at = NOW(), review_note = @n, applied_json = @a
                      WHERE request_id = @id AND status = 'PENDING'",
                    new MySqlParameter("@st", status), new MySqlParameter("@u", Trunc(user, 100)),
                    new MySqlParameter("@n", note == "" ? (object)DBNull.Value : note),
                    new MySqlParameter("@a", new JavaScriptSerializer().Serialize(results)),
                    new MySqlParameter("@id", id));
                Log(conn, tx, "Request " + id + ", employee " + empId + " (" + st.Code + ")",
                    (status == "APPROVED" ? "Approved" : "Partly approved") + ": " + string.Join(", ", appliedLabels.ToArray()));
                tx.Commit();

                Dictionary<string, object> d = new Dictionary<string, object>();
                d["ok"] = true;
                d["msg"] = status == "APPROVED"
                    ? "Request approved. The record has been updated."
                    : "Request partly approved. " + applied + " of " + changes.Count + " fields applied.";
                return new JavaScriptSerializer().Serialize(d);
            }
        }
    }

    /// <summary>Applies one field inside the approval transaction. Returns false with a reason when it cannot be applied.</summary>
    private bool ApplyField(MySqlConnection conn, MySqlTransaction tx, int empId, EmpState st, Dictionary<string, object> c, out string note)
    {
        string f = S(c, "field");
        FieldDef def;
        if (!Fields.TryGetValue(f, out def)) { note = "This field cannot be changed here"; return false; }
        string v = (S(c, "new") ?? "").Trim();

        switch (def.Kind)
        {
            case FKind.Text:
                v = HrExport.Clean(v);
                if (v == "" && !def.AllowEmpty) { note = "The requested value is blank"; return false; }
                if (v.Length > def.MaxLen) { note = "Longer than " + def.MaxLen + " characters"; return false; }
                TX(conn, tx, "UPDATE hrm_employee SET `" + def.Column + "` = @v WHERE empID = @e", new MySqlParameter("@v", v), new MySqlParameter("@e", empId));
                note = "Applied"; return true;

            case FKind.Date:
            {
                string iso = Norm(f, v);
                DateTime d;
                if (iso == "" || !DateTime.TryParseExact(iso, "yyyy-MM-dd", IC, DateTimeStyles.None, out d)) { note = "Not a valid date"; return false; }
                if (d > DateTime.Today || d.Year < 1920) { note = "Not a valid date of birth"; return false; }
                TX(conn, tx, "UPDATE hrm_employee SET emp_birthdate = @v WHERE empID = @e", new MySqlParameter("@v", d), new MySqlParameter("@e", empId));
                note = "Applied"; return true;
            }

            case FKind.Gender:
            {
                string g = v.Equals("male", StringComparison.OrdinalIgnoreCase) ? "Male" : v.Equals("female", StringComparison.OrdinalIgnoreCase) ? "Female" : "";
                if (g == "") { note = "Gender must be Male or Female"; return false; }
                TX(conn, tx, "UPDATE hrm_employee SET gender = @v WHERE empID = @e", new MySqlParameter("@v", g), new MySqlParameter("@e", empId));
                note = "Applied"; return true;
            }

            case FKind.Bank:
            {
                string bid = IdStr(v);
                if (bid == "" || TQ(conn, tx, "SELECT bank_id FROM banks WHERE bank_id = @b", new MySqlParameter("@b", bid)).Rows.Count == 0)
                { note = "The bank is not in the bank list"; return false; }
                TX(conn, tx, "UPDATE hrm_employee SET bankID = @v WHERE empID = @e", new MySqlParameter("@v", Convert.ToInt32(bid)), new MySqlParameter("@e", empId));
                note = "Applied"; return true;
            }

            case FKind.Dept:
            {
                string did = IdStr(v);
                if (did == "" || TQ(conn, tx, "SELECT ID FROM hrm_departments WHERE ID = @d", new MySqlParameter("@d", did)).Rows.Count == 0)
                { note = "The department is not in the department list"; return false; }
                TX(conn, tx, "UPDATE hrm_employee SET dept_id = @v WHERE empID = @e", new MySqlParameter("@v", Convert.ToInt32(did)), new MySqlParameter("@e", empId));
                if (st.ContractId > 0)
                {
                    TX(conn, tx, "UPDATE hrm_emp_contracts SET departmentID = @v WHERE ID = @c", new MySqlParameter("@v", Convert.ToInt32(did)), new MySqlParameter("@c", st.ContractId));
                    note = "Applied to the employee record and the current contract";
                }
                else note = "Applied to the employee record. No contract on record";
                return true;
            }

            case FKind.Job:
            {
                if (st.ContractId == 0) { note = "No contract on record"; return false; }
                string jid = IdStr(v);
                if (jid == "" || TQ(conn, tx, "SELECT ID FROM hrm_jobs WHERE ID = @j", new MySqlParameter("@j", jid)).Rows.Count == 0)
                { note = "The position is not in the position list"; return false; }
                TX(conn, tx, "UPDATE hrm_emp_contracts SET jobID = @v WHERE ID = @c", new MySqlParameter("@v", Convert.ToInt32(jid)), new MySqlParameter("@c", st.ContractId));
                note = "Applied to the current contract"; return true;
            }

            case FKind.EmpType:
            {
                string t = v.Equals("academic", StringComparison.OrdinalIgnoreCase) ? "Academic"
                         : v.Equals("administrative", StringComparison.OrdinalIgnoreCase) ? "Administrative"
                         : v.Equals("support", StringComparison.OrdinalIgnoreCase) ? "Support" : "";
                if (t == "") { note = "Staff category must be Academic, Administrative or Support"; return false; }
                TX(conn, tx, "UPDATE hrm_employee SET EmpType = @v WHERE empID = @e", new MySqlParameter("@v", t), new MySqlParameter("@e", empId));
                int updated = TX(conn, tx,
                    @"UPDATE appraisal_records r JOIN appraisal_sessions s ON s.session_id = r.session_id
                      SET r.staff_category = @cat, r.standards_group_id = NULL
                      WHERE r.employee_id = @e AND s.status = 'ACTIVE' AND r.status = 'PENDING' AND r.preflight_confirmed = 0",
                    new MySqlParameter("@cat", t.ToUpperInvariant()), new MySqlParameter("@e", empId));
                DataTable started = TQ(conn, tx,
                    @"SELECT COUNT(*) FROM appraisal_records r JOIN appraisal_sessions s ON s.session_id = r.session_id
                      WHERE r.employee_id = @e AND s.status = 'ACTIVE' AND NOT (r.status = 'PENDING' AND r.preflight_confirmed = 0)
                        AND r.staff_category <> @cat",
                    new MySqlParameter("@e", empId), new MySqlParameter("@cat", t.ToUpperInvariant()));
                int left = Convert.ToInt32(started.Rows[0][0]);
                note = "Applied";
                if (updated > 0) note += ". " + updated + (updated == 1 ? " appraisal not yet started was" : " appraisals not yet started were") + " moved to the new category";
                if (left > 0) note += ". " + left + (left == 1 ? " appraisal already started was" : " appraisals already started were") + " left unchanged";
                return true;
            }
        }
        note = "This field cannot be changed here";
        return false;
    }

    // ── reject ─────────────────────────────────────────────────────────────────
    private string AjaxReject(Dictionary<string, object> body)
    {
        int id = BodyInt(body, "id");
        string note = HrExport.Clean(BodyStr(body, "note"));
        if (note == "") return Fail("Enter a note for the staff member saying why the request is rejected.");
        if (note.Length > 2000) note = note.Substring(0, 2000);
        string user = HrAccess.Username();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                DataTable req = TQ(conn, tx, "SELECT emp_id, status FROM hrm_profile_change_requests WHERE request_id = @id FOR UPDATE", new MySqlParameter("@id", id));
                if (req.Rows.Count == 0) { tx.Rollback(); return Fail("This request was not found."); }
                if (Str(req.Rows[0]["status"]) != "PENDING") { tx.Rollback(); return Fail("This request has already been decided. Reload the page."); }
                TX(conn, tx,
                    @"UPDATE hrm_profile_change_requests SET status = 'REJECTED', reviewed_by = @u, reviewed_at = NOW(), review_note = @n
                      WHERE request_id = @id AND status = 'PENDING'",
                    new MySqlParameter("@u", Trunc(user, 100)), new MySqlParameter("@n", note), new MySqlParameter("@id", id));
                Log(conn, tx, "Request " + id + ", employee " + req.Rows[0]["emp_id"], "Rejected: " + note);
                tx.Commit();
            }
        }
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = true; d["msg"] = "Request rejected.";
        return new JavaScriptSerializer().Serialize(d);
    }

    private void Log(MySqlConnection conn, MySqlTransaction tx, string par, string what)
    {
        try
        {
            TX(conn, tx, "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u, 'HR Profile requests', @p, @c, NOW())",
                new MySqlParameter("@u", Trunc(HrAccess.Username(), 100)), new MySqlParameter("@p", Trunc(par, 300)), new MySqlParameter("@c", Trunc(what, 200)));
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  JSON helpers
    // ═══════════════════════════════════════════════════════════════════════════
    private static List<Dictionary<string, object>> ParseChanges(string json)
    {
        List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
        if (string.IsNullOrEmpty(json)) return list;
        try
        {
            object o = new JavaScriptSerializer().DeserializeObject(json);
            IEnumerable arr = o as object[];
            if (arr == null) arr = o as ArrayList;
            if (arr == null) return list;
            foreach (object item in arr)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d != null) list.Add(new Dictionary<string, object>(d, StringComparer.OrdinalIgnoreCase));
            }
        }
        catch { }
        return list;
    }

    private static string S(Dictionary<string, object> d, string key)
    {
        object v;
        if (d == null || !d.TryGetValue(key, out v) || v == null) return "";
        return Convert.ToString(v, IC);
    }

    private static string LabelOf(Dictionary<string, object> c)
    {
        string l = S(c, "label").Trim();
        if (l != "") return HrExport.Clean(l);
        FieldDef def;
        return Fields.TryGetValue(S(c, "field"), out def) ? def.Label : S(c, "field");
    }
    private static string OldText(Dictionary<string, object> c)
    {
        string t = S(c, "old_text");
        if (t != "") return t;
        FieldDef def;
        if (Fields.TryGetValue(S(c, "field"), out def) && (def.Kind == FKind.Bank || def.Kind == FKind.Dept || def.Kind == FKind.Job)) return "";
        return S(c, "old") == "-" ? "" : S(c, "old");
    }
    private static string NewText(Dictionary<string, object> c) { string t = S(c, "new_text"); return t != "" ? t : S(c, "new"); }

    private static int BodyInt(Dictionary<string, object> b, string k)
    {
        object v; int i;
        return b.TryGetValue(k, out v) && v != null && int.TryParse(Convert.ToString(v, IC), out i) ? i : 0;
    }
    private static string BodyStr(Dictionary<string, object> b, string k)
    {
        object v;
        return b.TryGetValue(k, out v) && v != null ? Convert.ToString(v, IC).Trim() : "";
    }
    private static HashSet<string> BodySet(Dictionary<string, object> b, string k)
    {
        HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        object v;
        if (!b.TryGetValue(k, out v) || v == null) return set;
        IEnumerable arr = v as object[];
        if (arr == null) arr = v as ArrayList;
        if (arr == null) return set;
        foreach (object o in arr) if (o != null) set.Add(o.ToString());
        return set;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Words, badges, formatting
    // ═══════════════════════════════════════════════════════════════════════════
    private static string StatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "PENDING":         return "Pending";
            case "APPROVED":        return "Approved";
            case "PARTLY_APPROVED": return "Partly approved";
            case "REJECTED":        return "Rejected";
            case "WITHDRAWN":       return "Withdrawn";
            default:                return s ?? "";
        }
    }
    private static string BadgeKind(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "PENDING":         return "warn";
            case "APPROVED":        return "ok";
            case "PARTLY_APPROVED": return "info";
            case "REJECTED":        return "bad";
            default:                return "neutral";
        }
    }
    private static string Badge(string s) { return "<span class='hr-badge hr-badge--" + BadgeKind(s) + "'>" + HttpUtility.HtmlEncode(StatusWord(s)) + "</span>"; }

    private static string DT(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        if (v is DateTime) d = (DateTime)v; else if (!DateTime.TryParse(v.ToString(), out d)) return "";
        if (d.Year < 1900) return "";
        return d.ToString("d MMM yyyy, HH:mm", IC);
    }

    private const string IconDownload =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4\"/><polyline points=\"7 10 12 15 17 10\"/><line x1=\"12\" y1=\"15\" x2=\"12\" y2=\"3\"/></svg>";

    private static string N(object v) { long n; return v != null && v != DBNull.Value && long.TryParse(v.ToString(), out n) ? n.ToString("N0") : "0"; }
    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }
    private static string Trunc(string s, int n) { s = s ?? ""; return s.Length > n ? s.Substring(0, n) : s; }

    // ─── Data access ───────────────────────────────────────────────────────────
    private static List<MySqlParameter> Clone(List<MySqlParameter> src)
    {
        List<MySqlParameter> l = new List<MySqlParameter>();
        foreach (MySqlParameter p in src) l.Add(new MySqlParameter(p.ParameterName, p.Value));
        return l;
    }

    private DataTable Q(string sql, List<MySqlParameter> parms) { return Q(sql, parms.ToArray()); }

    private DataTable Q(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dt);
            }
        }
        return dt;
    }

    private static DataTable TQ(MySqlConnection conn, MySqlTransaction tx, string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
        {
            if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dt);
        }
        return dt;
    }

    private static int TX(MySqlConnection conn, MySqlTransaction tx, string sql, params MySqlParameter[] parms)
    {
        using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
        {
            if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            return cmd.ExecuteNonQuery();
        }
    }
}
