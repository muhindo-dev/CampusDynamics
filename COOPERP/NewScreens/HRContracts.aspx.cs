using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

/// <summary>
/// Contracts. Default view: each employee's current contract (hr_current_contract_id), so the
/// KPIs Valid + Ending in 90 days + Ended + No contract add up to the number of staff.
/// "All contracts" shows the full history. One status rule everywhere:
///   Valid  = VALID and ends after the next 90 days (or has no end date)
///   Ending = VALID and ends today or within 90 days
///   Ended  = VALID past its end date, or EXPIRED / TERMINATED / RESIGNED
/// Filters are GET parameters; changes are postbacks that redirect back with the filters.
/// </summary>
public partial class COOPERP_NewScreens_HRContracts : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return System.Configuration.ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private const string BucketSql =
        "CASE WHEN c.ID IS NULL THEN 'NONE' " +
        "WHEN c.contractStatus = 'VALID' AND (c.contractEnd IS NULL OR c.contractEnd > DATE_ADD(CURDATE(), INTERVAL 90 DAY)) THEN 'VALID' " +
        "WHEN c.contractStatus = 'VALID' AND c.contractEnd >= CURDATE() THEN 'ENDING' ELSE 'ENDED' END";

    // ─── Query-string helpers ──────────────────────────────────────────────────
    private int    QsPage   { get { int v; return int.TryParse(Request.QueryString["page"] ?? "1", out v) && v > 0 ? v : 1; } }
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }
    private bool   QsAll    { get { return string.Equals(Request.QueryString["view"], "all", StringComparison.OrdinalIgnoreCase); } }
    private string QsStatus
    {
        get
        {
            string s = (Request.QueryString["status"] ?? "").Trim().ToUpperInvariant();
            if (s == "EXPIRING") s = "ENDING";                     // older links
            if (s == "EXPIRED" || s == "TERMINATED" || s == "RESIGNED") s = "ENDED";
            if (s == "VALID" || s == "ENDING" || s == "ENDED") return s;
            if (s == "NONE" && !QsAll) return s;
            return "";
        }
    }
    private string QsType   { get { string t = (Request.QueryString["type"] ?? "").Trim().ToUpperInvariant(); return Array.IndexOf(ContractTypes, t) >= 0 ? t : ""; } }
    private string QsDept   { get { int d; return int.TryParse(Request.QueryString["dept"], out d) && d > 0 ? d.ToString() : ""; } }
    private string QsJob    { get { int d; return int.TryParse(Request.QueryString["job"], out d) && d > 0 ? d.ToString() : ""; } }
    private int    QsSize
    {
        get
        {
            int v;
            if (int.TryParse(Request.QueryString["sz"] ?? "50", out v) && (v == 25 || v == 50 || v == 100 || v == 200)) return v;
            return 50;
        }
    }

    // hrm_emp_contracts.contract_type is ENUM('FULL TIME','PART TIME'); other values are rejected by the database.
    private static readonly string[] ContractTypes = { "FULL TIME", "PART TIME" };
    private static readonly string[] ContractStatuses = { "VALID", "EXPIRED", "TERMINATED", "RESIGNED" };

    protected string SearchValue = "";

    protected void Page_Load(object sender, EventArgs e)
    {
        if (Request.QueryString["ajax"] == "search_emp")
        {
            if (!HrAccess.RequireHr(true)) return;
            HandleEmployeeSearch();
            return;
        }

        if (!HrAccess.RequireHr(false)) return;

        string export = (Request.QueryString["export"] ?? "").Trim().ToLowerInvariant();
        if (export == "xlsx" || export == "csv") { SendExport(export); return; }

        LoadFormDropdowns(); // must run every request so modal dropdowns survive postback
        if (!IsPostBack)
        {
            LoadFilterDropdowns();
            LoadStats();
            BindGrid();
            LoadEmployeePicker();
            EmitEmployeeAutoFillScript();
            ShowFlashMessage();
        }
    }

    // ─── AJAX: employee search (kept for other callers) ────────────────────────
    private void HandleEmployeeSearch()
    {
        Response.Clear();
        Response.ContentType = "application/json";
        string query = (Request.QueryString["q"] ?? "").Trim();
        StringBuilder sb = new StringBuilder("{\"results\":[");
        if (query.Length >= 1)
        {
            try
            {
                DataTable dt = ExecuteQuery(
                    @"SELECT e.empID, e.emp_name, e.EMP_CODE, IFNULL(j.jobname, 'Staff') AS emp_position
                      FROM hrm_employee e
                      LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
                      LEFT JOIN hrm_jobs j ON j.ID = c.jobID
                      WHERE e.emp_name LIKE @q OR e.EMP_CODE LIKE @q
                      ORDER BY e.emp_name LIMIT 15",
                    new MySqlParameter("@q", "%" + query + "%"));
                bool first = true;
                foreach (DataRow row in dt.Rows)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{\"empID\":\"").Append(EscapeJson(row["empID"]))
                      .Append("\",\"emp_name\":\"").Append(EscapeJson(row["emp_name"]))
                      .Append("\",\"EMP_CODE\":\"").Append(EscapeJson(row["EMP_CODE"]))
                      .Append("\",\"emp_position\":\"").Append(EscapeJson(row["emp_position"])).Append("\"}");
                }
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("HRContracts search: " + ex.Message); }
        }
        sb.Append("]}");
        Response.Write(sb.ToString());
        Response.End();
    }

    private static string EscapeJson(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    // ─── Filters ───────────────────────────────────────────────────────────────
    private void LoadFilterDropdowns()
    {
        SearchValue = QsSearch;

        StringBuilder sb = new StringBuilder();
        Opt(sb, "current", "Current contract per employee", !QsAll);
        Opt(sb, "all", "All contracts", QsAll);
        litViewOptions.Text = sb.ToString();

        sb.Length = 0;
        Opt(sb, "", "All statuses", QsStatus == "");
        Opt(sb, "VALID", "Valid", QsStatus == "VALID");
        Opt(sb, "ENDING", "Ending in 90 days", QsStatus == "ENDING");
        Opt(sb, "ENDED", "Ended", QsStatus == "ENDED");
        if (!QsAll) Opt(sb, "NONE", "No contract", QsStatus == "NONE");
        litStatusOptions.Text = sb.ToString();

        sb.Length = 0;
        Opt(sb, "", "All types", QsType == "");
        foreach (string t in ContractTypes) Opt(sb, t, TypeLabel(t), QsType == t);
        litTypeOptions.Text = sb.ToString();

        sb.Length = 0;
        Opt(sb, "", "All departments", QsDept == "");
        foreach (DataRow dr in ExecuteQuery("SELECT ID, dept_name FROM hrm_departments WHERE dept_name IS NOT NULL AND dept_name <> '' ORDER BY dept_name").Rows)
            Opt(sb, dr["ID"].ToString(), dr["dept_name"].ToString(), dr["ID"].ToString() == QsDept);
        litDeptOptions.Text = sb.ToString();

        sb.Length = 0;
        Opt(sb, "", "All positions", QsJob == "");
        foreach (DataRow dr in ExecuteQuery("SELECT ID, jobname FROM hrm_jobs WHERE jobname IS NOT NULL AND jobname <> '' ORDER BY jobname").Rows)
            Opt(sb, dr["ID"].ToString(), dr["jobname"].ToString(), dr["ID"].ToString() == QsJob);
        litJobOptions.Text = sb.ToString();

        sb.Length = 0;
        foreach (int sz in new int[] { 25, 50, 100, 200 }) Opt(sb, sz.ToString(), sz.ToString(), sz == QsSize);
        litSizeOptions.Text = sb.ToString();
    }

    private static void Opt(StringBuilder sb, string value, string text, bool selected)
    {
        sb.Append("<option value=\"").Append(HttpUtility.HtmlAttributeEncode(value)).Append("\"")
          .Append(selected ? " selected=\"selected\"" : "").Append(">")
          .Append(HttpUtility.HtmlEncode(HrExport.Clean(text))).Append("</option>");
    }

    // ─── Modal dropdowns (Add / Edit / Renew) ──────────────────────────────────
    private void LoadFormDropdowns()
    {
        DataTable dtJob = ExecuteQuery("SELECT ID AS jobID, jobname FROM hrm_jobs ORDER BY jobname");
        DataTable dtDept = ExecuteQuery("SELECT ID AS deptID, dept_name FROM hrm_departments ORDER BY dept_name");
        DataTable dtScale = ExecuteQuery(
            "SELECT ID AS scaleID, CONCAT(scale_name, ' (UGX ', FORMAT(IFNULL(basicpay,0),0), ')') AS scaleLabel FROM hrm_payscales ORDER BY scale_name");

        FillList(ddlContractJob, dtJob, "jobID", "jobname", "Select position");
        FillList(ddlEditJob, dtJob, "jobID", "jobname", "Select position");
        FillList(ddlContractDept, dtDept, "deptID", "dept_name", "Select department");
        FillList(ddlEditDept, dtDept, "deptID", "dept_name", "Select department");
        FillList(ddlContractScale, dtScale, "scaleID", "scaleLabel", "Select pay scale");
        FillList(ddlEditScale, dtScale, "scaleID", "scaleLabel", "Select pay scale");

        ddlEditStatus.Items.Clear();
        foreach (string s in ContractStatuses) ddlEditStatus.Items.Add(new ListItem(StatusWord(s), s));
        foreach (DropDownList d in new DropDownList[] { ddlEditType, ddlContractType, ddlRenewType })
        {
            d.Items.Clear();
            foreach (string t in ContractTypes) d.Items.Add(new ListItem(TypeLabel(t), t));
        }
    }

    private static void FillList(DropDownList ddl, DataTable dt, string valueCol, string textCol, string placeholder)
    {
        ddl.Items.Clear();
        ddl.Items.Add(new ListItem(placeholder, ""));
        foreach (DataRow dr in dt.Rows)
            ddl.Items.Add(new ListItem(HrExport.Clean(dr[textCol].ToString()), dr[valueCol].ToString()));
    }

    private void LoadEmployeePicker()
    {
        StringBuilder sb = new StringBuilder("<option value=\"\">Select employee</option>");
        foreach (DataRow r in ExecuteQuery("SELECT empID, emp_name, EMP_CODE FROM hrm_employee ORDER BY emp_name").Rows)
        {
            string code = Str(r["EMP_CODE"]);
            sb.Append("<option value=\"").Append(r["empID"]).Append("\">")
              .Append(Enc(Str(r["emp_name"]) + (code == "" || code == "-" ? "" : " (" + code + ")"))).Append("</option>");
        }
        litEmpOptions.Text = sb.ToString();
    }

    private void EmitEmployeeAutoFillScript()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT e.empID,
                     IFNULL(c.jobID, 0) AS lastJobID,
                     IFNULL(c.departmentID, 0) AS lastDeptID,
                     IFNULL(c.payscale, 0) AS lastScaleID
              FROM hrm_employee e
              LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)");

        StringBuilder sb = new StringBuilder("var employeeDefaults={");
        bool first = true;
        foreach (DataRow dr in dt.Rows)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.AppendFormat("{0}:{{j:{1},d:{2},s:{3}}}", dr["empID"], dr["lastJobID"], dr["lastDeptID"], dr["lastScaleID"]);
        }
        sb.Append("};");
        ScriptManager.RegisterStartupScript(this, GetType(), "empDefaults", sb.ToString(), true);
    }

    // ─── KPIs (current contract per employee; they add up to the staff total) ──
    private void LoadStats()
    {
        DataTable dt = ExecuteQuery(
            "SELECT COUNT(*) AS total, " +
            "SUM(b = 'VALID') AS valid_ok, SUM(b = 'ENDING') AS ending, SUM(b = 'ENDED') AS ended, SUM(b = 'NONE') AS none " +
            "FROM (SELECT " + BucketSql + " AS b FROM hrm_employee e LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)) x");
        if (dt.Rows.Count == 0) return;
        DataRow r = dt.Rows[0];
        StringBuilder sb = new StringBuilder("<div class=\"hr-kpis\">");
        Kpi(sb, "Valid", r["valid_ok"], "VALID", "More than 90 days left");
        Kpi(sb, "Ending in 90 days", r["ending"], "ENDING", "Renew or end");
        Kpi(sb, "Ended", r["ended"], "ENDED", "Past end date or closed");
        Kpi(sb, "No contract", r["none"], "NONE", "No contract recorded");
        sb.Append("</div>");
        litKpis.Text = sb.ToString();
        litStaffTotal.Text = N(r["total"]) + " staff";
    }

    private void Kpi(StringBuilder sb, string label, object value, string status, string sub)
    {
        bool on = !QsAll && QsStatus == status;
        sb.Append("<a class=\"hr-kpi").Append(on ? " ct-kpi--on" : "").Append("\" href=\"HRContracts.aspx?status=").Append(status).Append("\">")
          .Append("<div class=\"hr-kpi__label\">").Append(HttpUtility.HtmlEncode(label)).Append("</div>")
          .Append("<div class=\"hr-kpi__value\">").Append(N(value)).Append("</div>")
          .Append("<div class=\"hr-kpi__sub\">").Append(HttpUtility.HtmlEncode(sub)).Append("</div></a>");
    }

    // ─── List query (shared by the grid and the export) ────────────────────────
    private string FromClause(bool all)
    {
        return all
            ? @" FROM hrm_emp_contracts c
                 JOIN hrm_employee e ON e.empID = c.empID "
            : @" FROM hrm_employee e
                 LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID) ";
    }

    private const string Joins =
        @" LEFT JOIN hrm_jobs j ON j.ID = c.jobID
           LEFT JOIN hrm_departments d ON d.ID = c.departmentID
           LEFT JOIN hrm_payscales ps ON ps.ID = c.payscale ";

    private string WhereClause(List<MySqlParameter> parms, string status, bool all)
    {
        StringBuilder where = new StringBuilder(" WHERE 1=1");
        if (QsSearch != "")
        {
            where.Append(" AND (e.emp_name LIKE @search OR e.EMP_CODE LIKE @search OR c.comments LIKE @search)");
            parms.Add(new MySqlParameter("@search", "%" + QsSearch + "%"));
        }
        if (QsDept != "") { where.Append(" AND c.departmentID = @dept"); parms.Add(new MySqlParameter("@dept", QsDept)); }
        if (QsJob != "")  { where.Append(" AND c.jobID = @job"); parms.Add(new MySqlParameter("@job", QsJob)); }
        if (QsType != "") { where.Append(" AND c.contract_type = @type"); parms.Add(new MySqlParameter("@type", QsType)); }
        if (status != "" && !(all && status == "NONE"))
        {
            where.Append(" AND (" + BucketSql + ") = @bucket");
            parms.Add(new MySqlParameter("@bucket", status));
        }
        return where.ToString();
    }

    private const string SelectCols =
        @"SELECT c.ID AS contractID, c.contractStart, c.contractEnd, c.contractStatus, c.contract_type, c.comments,
                 c.fixedamount, c.payscale AS payscaleID,
                 e.empID, e.EMP_CODE, e.emp_name, e.EmpType,
                 j.jobname, j.ID AS jobIDVal, d.dept_name, d.ID AS deptIDVal,
                 ps.scale_name, ps.ID AS scaleIDVal,
                 IFNULL(ps.basicpay, c.fixedamount) AS basicpay,
                 DATEDIFF(c.contractEnd, CURDATE()) AS days_remaining ";

    // ─── Grid ──────────────────────────────────────────────────────────────────
    private void BindGrid()
    {
        int page = QsPage, sz = QsSize;
        bool all = QsAll;
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string from = FromClause(all) + Joins + WhereClause(parms, QsStatus, all);

        int totalRows = 0;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*)" + from, conn))
                {
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(CloneParam(p));
                    totalRows = Convert.ToInt32(cmd.ExecuteScalar());
                }

                int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRows / sz));
                if (page > totalPages) page = totalPages;
                int offset = (page - 1) * sz;

                using (MySqlCommand cmd = new MySqlCommand(SelectCols + from +
                    " ORDER BY e.emp_name ASC, c.contractEnd DESC LIMIT @limit OFFSET @offset", conn))
                {
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(CloneParam(p));
                    cmd.Parameters.AddWithValue("@limit", sz);
                    cmd.Parameters.AddWithValue("@offset", offset);

                    StringBuilder sb = new StringBuilder();
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read()) sb.Append(RowHtml(rdr));
                    }
                    if (sb.Length == 0)
                        sb.Append("<tr><td colspan='10' class='hr-empty'>No contracts match these filters.</td></tr>");
                    litGridBody.Text = sb.ToString();
                }

                litPager.Text = BuildPager(page, totalPages);
                litPagerInfo.Text = totalRows == 0 ? "No records" :
                    string.Format("{0} to {1} of {2}", offset + 1, Math.Min(offset + sz, totalRows), totalRows.ToString("N0"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HRContracts grid: " + ex);
            litGridBody.Text = "<tr><td colspan='10' class='hr-empty'>Contracts could not be loaded. Refresh the page, and contact MIS if it keeps happening.</td></tr>";
        }
        litTotalCount.Text = totalRows == 1 ? "1 record" : totalRows.ToString("N0") + " records";
        litExport.Text =
            "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" + HttpUtility.HtmlAttributeEncode(BuildFilterUrl("export=xlsx", false)) + "\">" + IconDownload + "Excel</a>" +
            "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" + HttpUtility.HtmlAttributeEncode(BuildFilterUrl("export=csv", false)) + "\">CSV</a>";
    }

    private string RowHtml(MySqlDataReader rdr)
    {
        bool hasContract = rdr["contractID"] != DBNull.Value;
        string empName = Str(rdr["emp_name"]);
        string nameJs = HttpUtility.JavaScriptStringEncode(empName);
        StringBuilder sb = new StringBuilder("<tr>");

        if (hasContract)
            sb.AppendFormat("<td><input type='checkbox' class='ct-row-check' value='{0}' onchange='updateBatchToolbar()' aria-label='Select' /></td>", rdr["contractID"]);
        else
            sb.Append("<td></td>");

        sb.Append("<td>").Append(Enc(empName)).Append("<span class='hr-sub'>").Append(Enc(Str(rdr["EMP_CODE"]))).Append("</span></td>");
        sb.Append("<td>").Append(Enc(Str(rdr["dept_name"]))).Append("</td>");
        sb.Append("<td>").Append(Enc(Str(rdr["jobname"]))).Append("</td>");
        sb.Append("<td>").Append(hasContract ? HttpUtility.HtmlEncode(TypeLabel(Str(rdr["contract_type"]))) : "").Append("</td>");
        sb.Append("<td>").Append(StatusBadge(rdr["contractStatus"], rdr["contractEnd"], hasContract)).Append("</td>");
        sb.Append("<td style='white-space:nowrap'>").Append(D(rdr["contractStart"])).Append("</td>");
        sb.Append("<td style='white-space:nowrap'>").Append(D(rdr["contractEnd"])).Append("</td>");
        sb.Append("<td class='hr-num'>").Append(hasContract ? FormatMoney(rdr["basicpay"]) : "")
          .Append(hasContract ? "<span class='hr-sub'>" + Enc(rdr["scale_name"] == DBNull.Value ? "Fixed amount" : Str(rdr["scale_name"])) + "</span>" : "")
          .Append("</td>");

        sb.Append("<td class='hr-right' style='white-space:nowrap'>");
        if (!hasContract)
        {
            sb.AppendFormat("<button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick=\"openAddModal({0})\">Add contract</button>", rdr["empID"]);
        }
        else
        {
            string status = Str(rdr["contractStatus"]).ToUpperInvariant();
            string type = Str(rdr["contract_type"]);
            string startDt = rdr["contractStart"] != DBNull.Value ? Convert.ToDateTime(rdr["contractStart"]).ToString("yyyy-MM-dd") : "";
            string endDt = rdr["contractEnd"] != DBNull.Value ? Convert.ToDateTime(rdr["contractEnd"]).ToString("yyyy-MM-dd") : "";
            string commentJs = HttpUtility.JavaScriptStringEncode(Str(rdr["comments"]));
            sb.AppendFormat("<button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick=\"openEditModal('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}')\">Edit</button>",
                rdr["contractID"], Str(rdr["jobIDVal"]), Str(rdr["deptIDVal"]), Str(rdr["scaleIDVal"]),
                HttpUtility.JavaScriptStringEncode(type), status, startDt, endDt,
                rdr["fixedamount"] == DBNull.Value ? "0" : rdr["fixedamount"].ToString(), commentJs, nameJs);
            if (status == "VALID" || status == "EXPIRED")
                sb.AppendFormat(" <button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick=\"openRenewModal('{0}','{1}','{2}','{3}','{4}')\">Renew</button>",
                    rdr["contractID"], rdr["empID"], nameJs, endDt, HttpUtility.JavaScriptStringEncode(type));
        }
        sb.Append("</td></tr>");
        return sb.ToString();
    }

    private string BuildPager(int page, int totalPages)
    {
        if (totalPages <= 1) return "";
        StringBuilder sb = new StringBuilder("<div class='hr-pager'>");
        sb.AppendFormat("<span>Page {0} of {1}</span>", page, totalPages);
        sb.Append(PagerButton("First", 1, page > 1));
        sb.Append(PagerButton("Previous", page - 1, page > 1));
        sb.Append(PagerButton("Next", page + 1, page < totalPages));
        sb.Append(PagerButton("Last", totalPages, page < totalPages));
        sb.Append("</div>");
        return sb.ToString();
    }

    private string PagerButton(string label, int target, bool enabled)
    {
        if (!enabled) return "<button type='button' disabled='disabled'>" + label + "</button>";
        return "<button type='button' onclick=\"location.href='" + HttpUtility.JavaScriptStringEncode(BuildFilterUrl("page=" + target, false)) + "'\">" + label + "</button>";
    }

    // ─── Export: contract register ─────────────────────────────────────────────
    private void SendExport(string fmt)
    {
        HrExport.Report r = new HrExport.Report("Contract register", "contract-register");
        r.PreparedBy = HrAccess.Username();
        r.AddScope("View", QsAll ? "All contracts" : "Current contract per employee");
        r.AddScope("Status", StatusFilterWord(QsStatus));
        r.AddScope("Type", QsType == "" ? "" : TypeLabel(QsType));
        if (QsDept != "") r.AddScope("Department", LookupName("SELECT dept_name FROM hrm_departments WHERE ID=@id", QsDept));
        if (QsJob != "") r.AddScope("Position", LookupName("SELECT jobname FROM hrm_jobs WHERE ID=@id", QsJob));
        r.AddScope("Search", QsSearch);

        FillRegisterSheet(r.NewSheet("Contracts"), QsAll, QsStatus);
        HrExport.Sheet ending = r.NewSheet("Ending in 90 days");
        ending.Scope = "Valid current contracts ending within 90 days";
        ending.Title = "Contract register: current contracts ending in 90 days";
        FillRegisterSheet(ending, false, "ENDING");

        if (fmt == "csv") HrExport.SendCsv(Response, r, 0);
        else HrExport.SendXlsx(Response, r);
        Response.End();
    }

    private void FillRegisterSheet(HrExport.Sheet sh, bool all, string status)
    {
        sh.Add("Staff No").Add("Name").Add("Category").Add("Department").Add("Position").Add("Contract type")
          .Add("Status").Add("Start", HrExport.Kind.Date).Add("End", HrExport.Kind.Date)
          .Add("Days left", HrExport.Kind.Number).Add("Pay scale").Add("Basic pay (UGX)", HrExport.Kind.Money, true)
          .Add("Comments");
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string order = status == "ENDING" ? " ORDER BY c.contractEnd ASC, e.emp_name ASC" : " ORDER BY e.emp_name ASC, c.contractEnd DESC";
        DataTable dt = ExecuteQuery(SelectCols + FromClause(all) + Joins + WhereClause(parms, status, all) + order, parms.ToArray());
        foreach (DataRow row in dt.Rows)
        {
            bool has = row["contractID"] != DBNull.Value;
            string st = Str(row["contractStatus"]).ToUpperInvariant();
            object daysLeft = has && st == "VALID" && row["days_remaining"] != DBNull.Value && Convert.ToInt32(row["days_remaining"]) >= 0
                ? row["days_remaining"] : null;
            sh.Row(Str(row["EMP_CODE"]), Str(row["emp_name"]), Str(row["EmpType"]), Str(row["dept_name"]), Str(row["jobname"]),
                has ? TypeLabel(Str(row["contract_type"])) : "", StatusWords(row["contractStatus"], row["contractEnd"], has),
                row["contractStart"], row["contractEnd"], daysLeft,
                has ? (row["scale_name"] == DBNull.Value ? "Fixed amount" : Str(row["scale_name"])) : "",
                has ? row["basicpay"] : null, Str(row["comments"]) == "-" ? "" : Str(row["comments"]));
        }
    }

    private string LookupName(string sql, string id)
    {
        DataTable dt = ExecuteQuery(sql, new MySqlParameter("@id", id));
        return dt.Rows.Count > 0 ? Str(dt.Rows[0][0]) : "";
    }

    // ─── Add Contract ──────────────────────────────────────────────────────────
    protected void btnAddContract_Click(object sender, EventArgs e)
    {
        int    empID   = SafeInt(Request.Form[hfSelectedEmpID.UniqueID]);
        int    jobID   = SafeInt(Request.Form[ddlContractJob.UniqueID]);
        int    deptID  = SafeInt(Request.Form[ddlContractDept.UniqueID]);
        int    scaleID = SafeInt(Request.Form[ddlContractScale.UniqueID]);
        string type    = Request.Form[ddlContractType.UniqueID] ?? "";
        string comment = txtContractComment.Text.Trim();
        string start   = txtContractStart.Text.Trim();
        string end     = txtContractEnd.Text.Trim();
        decimal fixed_ = 0;
        decimal.TryParse(txtContractFixed.Text.Trim(), out fixed_);

        if (empID <= 0)   { ShowResult("addResult", "addModal", "Choose an employee."); return; }
        if (jobID <= 0)   { ShowResult("addResult", "addModal", "Choose a position."); return; }
        if (deptID <= 0)  { ShowResult("addResult", "addModal", "Choose a department."); return; }
        if (scaleID <= 0) { ShowResult("addResult", "addModal", "Choose a pay scale."); return; }

        DateTime dtStart, dtEnd;
        if (!DateTime.TryParse(start, out dtStart) || !DateTime.TryParse(end, out dtEnd))
        { ShowResult("addResult", "addModal", "Enter valid start and end dates."); return; }
        if (dtEnd <= dtStart)
        { ShowResult("addResult", "addModal", "The end date must be after the start date."); return; }

        DataTable dup = ExecuteQuery(
            "SELECT ID FROM hrm_emp_contracts WHERE empID=@eid AND contractStatus='VALID' LIMIT 1",
            new MySqlParameter("@eid", empID));
        if (dup.Rows.Count > 0)
        { ShowResult("addResult", "addModal", "This employee already has a valid contract. Use Renew instead."); return; }

        ExecuteNonQuery(
            @"INSERT INTO hrm_emp_contracts
              (empID, jobID, departmentID, payscale, contract_type, contractStart, contractEnd,
               contractStatus, fixedamount, comments)
              VALUES (@eid,@jid,@did,@sid,@type,@start,@end,'VALID',@fixed,@cmnt)",
            new MySqlParameter("@eid",   empID),
            new MySqlParameter("@jid",   jobID),
            new MySqlParameter("@did",   deptID),
            new MySqlParameter("@sid",   scaleID),
            new MySqlParameter("@type",  string.IsNullOrEmpty(type) ? "FULL TIME" : type),
            new MySqlParameter("@start", dtStart),
            new MySqlParameter("@end",   dtEnd),
            new MySqlParameter("@fixed", fixed_ > 0 ? fixed_ : 0m),
            new MySqlParameter("@cmnt",  string.IsNullOrEmpty(comment) ? (object)DBNull.Value : comment));

        RedirectWithFlash("Contract added.", true);
    }

    // ─── Renew Contract ────────────────────────────────────────────────────────
    protected void btnRenewContract_Click(object sender, EventArgs e)
    {
        int    origID = SafeInt(hdnRenewContractID.Value);
        int    empID  = SafeInt(hdnRenewEmpID.Value);
        string start  = txtRenewStart.Text.Trim();
        string end    = txtRenewEnd.Text.Trim();
        string type   = Request.Form[ddlRenewType.UniqueID] ?? "";

        if (origID <= 0 || empID <= 0)
        { ShowResult("renewResult", "renewModal", "Choose the contract to renew again."); return; }

        DateTime dtStart, dtEnd;
        if (!DateTime.TryParse(start, out dtStart) || !DateTime.TryParse(end, out dtEnd))
        { ShowResult("renewResult", "renewModal", "Enter valid start and end dates."); return; }
        if (dtEnd <= dtStart)
        { ShowResult("renewResult", "renewModal", "The end date must be after the start date."); return; }

        DataTable orig = ExecuteQuery(
            "SELECT jobID, departmentID, payscale, fixedamount FROM hrm_emp_contracts WHERE ID=@id",
            new MySqlParameter("@id", origID));
        if (orig.Rows.Count == 0)
        { ShowResult("renewResult", "renewModal", "The original contract was not found."); return; }

        DataRow oRow   = orig.Rows[0];
        object  jobID  = oRow["jobID"]        == DBNull.Value ? DBNull.Value : oRow["jobID"];
        object  deptID = oRow["departmentID"] == DBNull.Value ? DBNull.Value : oRow["departmentID"];
        object  scale  = oRow["payscale"]     == DBNull.Value ? DBNull.Value : oRow["payscale"];
        object  fixed_ = oRow["fixedamount"]  == DBNull.Value ? DBNull.Value : oRow["fixedamount"];

        ExecuteNonQuery("UPDATE hrm_emp_contracts SET contractStatus='EXPIRED' WHERE ID=@id",
            new MySqlParameter("@id", origID));

        ExecuteNonQuery(
            @"INSERT INTO hrm_emp_contracts
              (empID, jobID, departmentID, payscale, contract_type, contractStart, contractEnd,
               contractStatus, fixedamount)
              VALUES (@eid,@jid,@did,@sid,@type,@start,@end,'VALID',@fixed)",
            new MySqlParameter("@eid",   empID),
            new MySqlParameter("@jid",   jobID),
            new MySqlParameter("@did",   deptID),
            new MySqlParameter("@sid",   scale),
            new MySqlParameter("@type",  string.IsNullOrEmpty(type) ? "FULL TIME" : type),
            new MySqlParameter("@start", dtStart),
            new MySqlParameter("@end",   dtEnd),
            new MySqlParameter("@fixed", fixed_));

        RedirectWithFlash("Contract renewed.", true);
    }

    // ─── Edit Contract ─────────────────────────────────────────────────────────
    protected void btnUpdateContract_Click(object sender, EventArgs e)
    {
        int     cid     = SafeInt(hdnEditContractID.Value);
        int     jobID   = SafeInt(Request.Form[ddlEditJob.UniqueID]);
        int     deptID  = SafeInt(Request.Form[ddlEditDept.UniqueID]);
        int     scaleID = SafeInt(Request.Form[ddlEditScale.UniqueID]);
        string  type    = Request.Form[ddlEditType.UniqueID]   ?? "";
        string  status  = Request.Form[ddlEditStatus.UniqueID] ?? "";
        string  start   = txtEditStart.Text.Trim();
        string  end     = txtEditEnd.Text.Trim();
        string  comment = txtEditComment.Text.Trim();
        decimal fixed_  = 0;
        decimal.TryParse(txtEditFixed.Text.Trim(), out fixed_);

        if (cid <= 0)     { ShowResult("editResult", "editModal", "Choose the contract to edit again."); return; }
        if (jobID <= 0)   { ShowResult("editResult", "editModal", "Choose a position."); return; }
        if (deptID <= 0)  { ShowResult("editResult", "editModal", "Choose a department."); return; }
        if (scaleID <= 0) { ShowResult("editResult", "editModal", "Choose a pay scale."); return; }
        if (Array.IndexOf(ContractStatuses, status) < 0) { ShowResult("editResult", "editModal", "Choose a status."); return; }

        DateTime dtStart, dtEnd;
        if (!DateTime.TryParse(start, out dtStart) || !DateTime.TryParse(end, out dtEnd))
        { ShowResult("editResult", "editModal", "Enter valid start and end dates."); return; }
        if (dtEnd <= dtStart)
        { ShowResult("editResult", "editModal", "The end date must be after the start date."); return; }

        ExecuteNonQuery(
            @"UPDATE hrm_emp_contracts SET
                jobID=@jid, departmentID=@did, payscale=@sid, contract_type=@type,
                contractStart=@start, contractEnd=@end, contractStatus=@status,
                fixedamount=@fixed, comments=@cmnt
              WHERE ID=@cid",
            new MySqlParameter("@jid",    jobID),
            new MySqlParameter("@did",    deptID),
            new MySqlParameter("@sid",    scaleID),
            new MySqlParameter("@type",   type),
            new MySqlParameter("@start",  dtStart),
            new MySqlParameter("@end",    dtEnd),
            new MySqlParameter("@status", status),
            new MySqlParameter("@fixed",  fixed_ > 0 ? fixed_ : 0m),
            new MySqlParameter("@cmnt",   string.IsNullOrEmpty(comment) ? (object)DBNull.Value : comment),
            new MySqlParameter("@cid",    cid));

        RedirectWithFlash("Contract updated.", true);
    }

    // ─── Delete Contract ───────────────────────────────────────────────────────
    protected void btnDeleteContract_Click(object sender, EventArgs e)
    {
        int cid = SafeInt(hdnDeleteContractID.Value);
        if (cid <= 0) return;
        ExecuteNonQuery("DELETE FROM hrm_emp_contracts WHERE ID=@id", new MySqlParameter("@id", cid));
        RedirectWithFlash("Contract deleted.", true);
    }

    #region Batch Operations

    protected void btnBatchDelete_Click(object sender, EventArgs e)
    {
        string[] ids = GetBatchIDs();
        if (ids.Length == 0) return;
        int n = 0;
        foreach (string id in ids)
        {
            int cid;
            if (int.TryParse(id.Trim(), out cid))
                n += ExecuteNonQuery("DELETE FROM hrm_emp_contracts WHERE ID=@id", new MySqlParameter("@id", cid));
        }
        RedirectWithFlash(n == 1 ? "1 contract deleted." : n + " contracts deleted.", true);
    }

    protected void btnBatchStatus_Click(object sender, EventArgs e)
    {
        string[] ids    = GetBatchIDs();
        string   status = (hdnBatchStatus.Value ?? "").Trim().ToUpperInvariant();
        if (ids.Length == 0 || Array.IndexOf(ContractStatuses, status) < 0) return;

        int n = 0;
        foreach (string id in ids)
        {
            int cid;
            if (int.TryParse(id.Trim(), out cid))
                n += ExecuteNonQuery("UPDATE hrm_emp_contracts SET contractStatus=@status WHERE ID=@id",
                    new MySqlParameter("@status", status), new MySqlParameter("@id", cid));
        }
        RedirectWithFlash((n == 1 ? "1 contract" : n + " contracts") + " set to " + StatusWord(status).ToLowerInvariant() + ".", true);
    }

    private string[] GetBatchIDs()
    {
        string raw = hdnBatchIDs.Value ?? "";
        if (string.IsNullOrEmpty(raw)) return new string[0];
        return raw.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    #endregion

    // ─── Words and badges ──────────────────────────────────────────────────────
    private static string TypeLabel(string t)
    {
        switch ((t ?? "").ToUpperInvariant())
        {
            case "FULL TIME": return "Full time";
            case "PART TIME": return "Part time";
            case "CONTRACT":  return "Contract";
            case "TEMPORARY": return "Temporary";
            default:          return t ?? "";
        }
    }

    private static string StatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "VALID":      return "Valid";
            case "EXPIRED":    return "Expired";
            case "TERMINATED": return "Terminated";
            case "RESIGNED":   return "Resigned";
            default:           return s ?? "";
        }
    }

    private static string StatusFilterWord(string s)
    {
        switch (s)
        {
            case "VALID":  return "Valid";
            case "ENDING": return "Ending in 90 days";
            case "ENDED":  return "Ended";
            case "NONE":   return "No contract";
            default:       return "";
        }
    }

    /// <summary>Status as shown in the list and the export.</summary>
    private static string StatusWords(object statusObj, object endObj, bool hasContract)
    {
        if (!hasContract) return "No contract";
        string s = statusObj == null || statusObj == DBNull.Value ? "" : statusObj.ToString().ToUpperInvariant();
        if (s != "VALID") return StatusWord(s);
        if (endObj == null || endObj == DBNull.Value) return "Valid";
        int days = (Convert.ToDateTime(endObj).Date - DateTime.Today).Days;
        if (days < 0) return "Past end date";
        if (days <= 90) return "Ending";
        return "Valid";
    }

    private static string StatusBadge(object statusObj, object endObj, bool hasContract)
    {
        string w = StatusWords(statusObj, endObj, hasContract);
        string kind;
        switch (w)
        {
            case "Valid":         kind = "ok"; break;
            case "Ending":        kind = "warn"; break;
            case "Past end date": kind = "bad"; break;
            case "No contract":   kind = "bad"; break;
            default:              kind = "neutral"; break;
        }
        if (w == "Ending" && endObj != null && endObj != DBNull.Value)
        {
            int days = (Convert.ToDateTime(endObj).Date - DateTime.Today).Days;
            w = "Ending in " + days + (days == 1 ? " day" : " days");
        }
        return "<span class='hr-badge hr-badge--" + kind + "'>" + HttpUtility.HtmlEncode(w) + "</span>";
    }

    private static string FormatMoney(object val)
    {
        decimal d;
        if (val == null || val == DBNull.Value || !decimal.TryParse(val.ToString(), out d)) return "";
        return d.ToString("#,##0", CultureInfo.InvariantCulture);
    }

    private static string D(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        if (v is DateTime) d = (DateTime)v; else if (!DateTime.TryParse(v.ToString(), out d)) return "";
        return d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }

    private const string IconDownload =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4\"/><polyline points=\"7 10 12 15 17 10\"/><line x1=\"12\" y1=\"15\" x2=\"12\" y2=\"3\"/></svg>";

    // ─── URLs and flash ────────────────────────────────────────────────────────
    private string BuildFilterUrl(string extra, bool keepPage)
    {
        List<string> parts = new List<string>();
        if (QsAll)            parts.Add("view=all");
        if (QsSearch != "")   parts.Add("q=" + HttpUtility.UrlEncode(QsSearch));
        if (QsStatus != "")   parts.Add("status=" + HttpUtility.UrlEncode(QsStatus));
        if (QsType != "")     parts.Add("type=" + HttpUtility.UrlEncode(QsType));
        if (QsDept != "")     parts.Add("dept=" + QsDept);
        if (QsJob != "")      parts.Add("job=" + QsJob);
        if (QsSize != 50)     parts.Add("sz=" + QsSize);
        if (keepPage && QsPage > 1) parts.Add("page=" + QsPage);
        if (!string.IsNullOrEmpty(extra)) parts.Add(extra);
        return "HRContracts.aspx" + (parts.Count > 0 ? "?" + string.Join("&", parts.ToArray()) : "");
    }

    private void RedirectWithFlash(string message, bool success)
    {
        Response.Redirect(BuildFilterUrl("msg=" + HttpUtility.UrlEncode(message) + "&ok=" + (success ? "1" : "0"), true), true);
    }

    /// <summary>Show the flash message from the query string after a post-redirect-get cycle.</summary>
    private void ShowFlashMessage()
    {
        string msg = (Request.QueryString["msg"] ?? "").Trim();
        if (string.IsNullOrEmpty(msg)) return;
        bool ok = (Request.QueryString["ok"] ?? "") == "1";
        ScriptManager.RegisterStartupScript(this, GetType(), "flash",
            "showToast('" + HttpUtility.JavaScriptStringEncode(HrExport.Clean(msg)) + "'," + (ok ? "false" : "true") + ");", true);
    }

    private void ShowResult(string resultId, string modalId, string message)
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "res_" + resultId,
            string.Format("(function(){{var r=document.getElementById('{0}');if(r){{r.textContent='{1}';}}openModal('{2}');}})();",
                resultId, HttpUtility.JavaScriptStringEncode(message), modalId),
            true);
        // A failed postback still needs the page content.
        LoadFilterDropdowns();
        LoadStats();
        BindGrid();
        LoadEmployeePicker();
        EmitEmployeeAutoFillScript();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int i; return int.TryParse(val.ToString(), out i) ? i : 0;
    }
    private static string N(object v) { long n; return v != null && v != DBNull.Value && long.TryParse(v.ToString(), out n) ? n.ToString("N0") : "0"; }
    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }

    private static MySqlParameter CloneParam(MySqlParameter p) { return new MySqlParameter(p.ParameterName, p.Value); }

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
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

    private int ExecuteNonQuery(string sql, params MySqlParameter[] parms)
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
