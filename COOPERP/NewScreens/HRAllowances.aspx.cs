using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using DevExpress.Web;

public partial class COOPERP_NewScreens_HRAllowances : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private static readonly string[] MONTHS = {
        "JANUARY","FEBRUARY","MARCH","APRIL","MAY","JUNE",
        "JULY","AUGUST","SEPTEMBER","OCTOBER","NOVEMBER","DECEMBER"
    };

    private static readonly string[] ALLOWED_TYPES = {
        "Transport Allowance",
        "Housing Allowance",
        "Acting Allowance",
        "Hardship Allowance",
        "Annual Bonus",
        "Performance Bonus",
        "Responsibility Allowance",
        "Uniform / Clothing",
        "Meal Allowance",
        "Overtime Pay",
        "Other"
    };

    // ===============================================================
    //  Page_Load
    // ===============================================================

    protected void Page_Load(object sender, EventArgs e)
    {
        // Handle AJAX  search requests
        if (Request.QueryString["ajax"] == "search_emp")
        {
            if (!HrAccess.RequireHr(true)) return;
            HandleEmployeeSearch();
            return;
        }

        if (!IsPostBack)
        {
            txtAddDateRecorded.Text = DateTime.Today.ToString("yyyy-MM-dd");
            txtAddYear.Text         = DateTime.Today.Year.ToString();
            txtBatchStartYear.Text  = DateTime.Today.Year.ToString();

            string curMonth = MONTHS[DateTime.Today.Month - 1];
            SetDropDownValue(ddlAddMonth,        curMonth);
            SetDropDownValue(ddlBatchStartMonth, curMonth);
        }

        BindGrid();
        LoadStats();
    }

    // ===============================================================
    //  AJAX: Employee Search
    // ===============================================================

    private void HandleEmployeeSearch()
    {
        Response.ContentType = "application/json";
        string query = (Request.QueryString["q"] ?? "").Trim();

        if (query.Length < 1)
        {
            Response.Write("{\"results\":[]}");
            Response.End();
            return;
        }

        try
        {
            DataTable dt = ExecuteQuery(
                @"SELECT e.empID, e.emp_name, e.EMP_CODE, 
                         IFNULL(j.jobname, 'Staff') AS emp_position
                FROM hrm_employee e
                LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID 
                    AND c.ID = (SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID)
                LEFT JOIN hrm_jobs j ON j.ID = c.jobID
                WHERE e.emp_name LIKE @q OR e.EMP_CODE LIKE @q 
                ORDER BY e.emp_name LIMIT 15",
                new MySqlParameter("@q", "%" + query + "%"));

            var list = new System.Collections.Generic.List<string>();
            foreach (DataRow row in dt.Rows)
            {
                var item = new
                {
                    empID = row["empID"].ToString(),
                    emp_name = row["emp_name"].ToString(),
                    EMP_CODE = row["EMP_CODE"].ToString(),
                    emp_position = row["emp_position"].ToString()
                };
                list.Add("{\"empID\":\"" + EscapeJson(item.empID) + 
                        "\",\"emp_name\":\"" + EscapeJson(item.emp_name) + 
                        "\",\"EMP_CODE\":\"" + EscapeJson(item.EMP_CODE) + 
                        "\",\"emp_position\":\"" + EscapeJson(item.emp_position) + "\"}");
            }
            Response.Write("{\"results\":[" + string.Join(",", list) + "]}");
            Response.End();
        }
        catch
        {
            Response.Write("{\"results\":[]}");
            Response.End();
        }
    }

    private string EscapeJson(object val)
    {
        if (val == null) return "";
        return val.ToString()
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    // ===============================================================
    //  Dropdowns
    // ===============================================================

    private void LoadFilterDropdowns()
    {
        // Removed - no employee filter dropdown needed anymore
    }

    private void LoadFormDropdowns()
    {
        // Removed - using autocomplete instead of dropdowns
    }

    private void SetDropDownValue(DropDownList ddl, string value)
    {
        ListItem li = ddl.Items.FindByValue(value);
        if (li != null) li.Selected = true;
    }

    // ===============================================================
    //  Stats
    // ===============================================================

    private void LoadStats()
    {
        try
        {
            DataTable dt = ExecuteQuery(@"
                SELECT
                    SUM(CASE WHEN status='PENDING'   THEN 1 ELSE 0 END) AS cnt_pending,
                    SUM(CASE WHEN status='PENDING'   THEN amount ELSE 0 END) AS amt_pending,
                    SUM(CASE WHEN status='SETTLED'
                              AND YEAR(payment_date) = YEAR(CURDATE())
                              THEN 1 ELSE 0 END) AS cnt_settled,
                    SUM(CASE WHEN status='CANCELLED' THEN 1 ELSE 0 END) AS cnt_cancelled
                FROM hrm_allowance_records");

            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                litPending.Text    = SafeInt(r["cnt_pending"]).ToString();
                litPendingAmt.Text = SafeDecimal(r["amt_pending"]).ToString("N0");
                litSettled.Text    = SafeInt(r["cnt_settled"]).ToString();
                litCancelled.Text  = SafeInt(r["cnt_cancelled"]).ToString();
            }
        }
        catch { /* table may not yet exist */ }
    }

    // ===============================================================
    //  Grid Binding
    // ===============================================================

    private void BindGrid()
    {
        string search  = txtSearch.Text.Trim();
        string month   = ddlFilterMonth.SelectedValue;
        string status  = ddlFilterStatus.SelectedValue;
        string yearStr = txtFilterYear.Text.Trim();

        var sql   = new StringBuilder();
        var parms = new List<MySqlParameter>();

        sql.Append(@"
            SELECT a.id, a.empID, a.allowance_type, a.amount,
                   a.to_add_month, a.to_add_year,
                   a.status, a.description, a.date_recorded,
                   a.payroll_id, a.payment_date,
                   e.EMP_CODE,
                   CONCAT(e.emp_name) AS emp_name,
                   p.payroll_title
            FROM hrm_allowance_records a
            JOIN hrm_employee e ON e.empID = a.empID
            LEFT JOIN hrm_payroll p ON p.ID = a.payroll_id
            WHERE 1=1");

        if (!string.IsNullOrEmpty(search))
        {
            sql.Append(" AND (e.emp_name LIKE @search OR e.EMP_CODE LIKE @search)");
            parms.Add(new MySqlParameter("@search", "%" + search + "%"));
        }
        if (!string.IsNullOrEmpty(month))
        {
            sql.Append(" AND a.to_add_month = @month");
            parms.Add(new MySqlParameter("@month", month));
        }
        if (!string.IsNullOrEmpty(status))
        {
            sql.Append(" AND a.status = @status");
            parms.Add(new MySqlParameter("@status", status));
        }
        int filterYear;
        if (!string.IsNullOrEmpty(yearStr) && int.TryParse(yearStr, out filterYear) && filterYear > 2000)
        {
            sql.Append(" AND a.to_add_year = @yr");
            parms.Add(new MySqlParameter("@yr", filterYear));
        }

        sql.Append(" ORDER BY a.to_add_year DESC, FIELD(a.to_add_month,'JANUARY','FEBRUARY','MARCH','APRIL','MAY','JUNE','JULY','AUGUST','SEPTEMBER','OCTOBER','NOVEMBER','DECEMBER') DESC, a.id DESC");

        try
        {
            DataTable dt = ExecuteQuery(sql.ToString(), parms.ToArray());

            int pageSize;
            if (int.TryParse(ddlPageSize.SelectedValue, out pageSize) && pageSize > 0)
                gvAllowances.SettingsPager.PageSize = pageSize;

            string countText = dt.Rows.Count.ToString();
            litFilterCount.Text    = countText;

            gvAllowances.DataSource = dt;
            gvAllowances.DataBind();
        }
        catch
        {
            litFilterCount.Text    = "0";
            gvAllowances.DataSource = new DataTable();
            gvAllowances.DataBind();
        }
    }

    protected void gvAllowances_HtmlRowCreated(object sender, ASPxGridViewTableRowEventArgs e)
    {
        if (e.RowType != GridViewRowType.Data) return;
        string status = (gvAllowances.GetRowValues(e.VisibleIndex, "status") ?? "").ToString().ToUpper();
        if      (status == "PENDING")   e.Row.CssClass = "pr-row-pending";
        else if (status == "SETTLED")   e.Row.CssClass = "pr-row-settled";
        else if (status == "CANCELLED") e.Row.CssClass = "pr-row-cancelled";
    }

    // ===============================================================
    //  Grid Editing
    // ===============================================================

    protected void gvAllowances_RowUpdating(object sender, DevExpress.Web.Data.ASPxDataUpdatingEventArgs e)
    {
        int    id   = Convert.ToInt32(e.Keys["id"]);
        string type = GetNewValue(e.NewValues, "allowance_type");
        string mon  = GetNewValue(e.NewValues, "to_add_month");
        string stat = GetNewValue(e.NewValues, "status");
        string desc = GetNewValue(e.NewValues, "description");

        decimal amount;
        decimal.TryParse(GetNewValue(e.NewValues, "amount"), out amount);

        int year;
        int.TryParse(GetNewValue(e.NewValues, "to_add_year"), out year);

        if (!Array.Exists(ALLOWED_TYPES, t => t == type))
        {
            e.Cancel = true;
            gvAllowances.CancelEdit();
            return;
        }
        if (!Array.Exists(MONTHS, m => m == mon))
        {
            e.Cancel = true;
            gvAllowances.CancelEdit();
            return;
        }
        if (stat != "PENDING" && stat != "CANCELLED")
        {
            e.Cancel = true;
            gvAllowances.CancelEdit();
            return;
        }
        if (amount < 0 || year < 2020 || year > 2099)
        {
            e.Cancel = true;
            gvAllowances.CancelEdit();
            return;
        }

        ExecuteNonQuery(
            "UPDATE hrm_allowance_records " +
            "SET allowance_type=@type, amount=@amount, to_add_month=@month, to_add_year=@year, " +
            "status=@status, description=@desc " +
            "WHERE id=@id AND status != 'SETTLED'",
            new MySqlParameter("@type",   type),
            new MySqlParameter("@amount", amount),
            new MySqlParameter("@month",  mon),
            new MySqlParameter("@year",   year),
            new MySqlParameter("@status", stat),
            new MySqlParameter("@desc",   desc),
            new MySqlParameter("@id",     id));

        e.Cancel = true;
        gvAllowances.CancelEdit();
        BindGrid();
        LoadStats();
    }

    protected void gvAllowances_RowDeleting(object sender, DevExpress.Web.Data.ASPxDataDeletingEventArgs e)
    {
        int id = Convert.ToInt32(e.Keys["id"]);
        ExecuteNonQuery(
            "DELETE FROM hrm_allowance_records WHERE id=@id AND status != 'SETTLED'",
            new MySqlParameter("@id", id));

        e.Cancel = true;
        BindGrid();
        LoadStats();
    }

    // ===============================================================
    //  Add Single Allowance
    // ===============================================================

    protected void btnAddAllowance_Click(object sender, EventArgs e)
    {
        string empID = hfSelectedEmpID.Value;
        string type  = ddlAddType.SelectedValue;

        if (string.IsNullOrEmpty(empID))
        {
            ShowResult("addResult", "addAllowanceModal", "Please select an employee.", false);
            return;
        }
        if (string.IsNullOrEmpty(type) || !Array.Exists(ALLOWED_TYPES, t => t == type))
        {
            ShowResult("addResult", "addAllowanceModal", "Please select a valid allowance type.", false);
            return;
        }

        decimal amount;
        if (!decimal.TryParse(txtAddAmount.Text, out amount) || amount <= 0)
        {
            ShowResult("addResult", "addAllowanceModal", "Please enter a valid amount greater than 0.", false);
            return;
        }

        int year;
        if (!int.TryParse(txtAddYear.Text, out year) || year < 2020 || year > 2099)
        {
            ShowResult("addResult", "addAllowanceModal", "Please enter a valid year (2020-2099).", false);
            return;
        }

        string month = ddlAddMonth.SelectedValue;
        if (!Array.Exists(MONTHS, m => m == month))
        {
            ShowResult("addResult", "addAllowanceModal", "Please select a valid month.", false);
            return;
        }

        DateTime dateRecorded;
        if (!DateTime.TryParse(txtAddDateRecorded.Text, out dateRecorded))
            dateRecorded = DateTime.Today;

        string desc       = txtAddDescription.Text.Trim();
        string recordedBy = (Session["ScreenName"] ?? Session["username"] ?? "").ToString();

        ExecuteNonQuery(
            "INSERT INTO hrm_allowance_records " +
            "(empID, allowance_type, amount, description, date_recorded, status, to_add_month, to_add_year, recorded_by) " +
            "VALUES (@eid, @type, @amount, @desc, @date, 'PENDING', @month, @year, @by)",
            new MySqlParameter("@eid",    empID),
            new MySqlParameter("@type",   type),
            new MySqlParameter("@amount", amount),
            new MySqlParameter("@desc",   desc),
            new MySqlParameter("@date",   dateRecorded),
            new MySqlParameter("@month",  month),
            new MySqlParameter("@year",   year),
            new MySqlParameter("@by",     recordedBy));

        hfSelectedEmpID.Value           = "";
        ddlAddType.SelectedIndex        = 0;
        txtAddAmount.Text               = "0";
        txtAddDateRecorded.Text         = DateTime.Today.ToString("yyyy-MM-dd");
        txtAddDescription.Text          = "";

        BindGrid();
        LoadStats();

        ScriptManager.RegisterStartupScript(this, GetType(), "closeAdd",
            "document.getElementById('addAllowanceModal').style.display='none';", true);
    }

    // ===============================================================
    //  Batch / Recurring Allowances
    // ===============================================================

    protected void btnBatchCreate_Click(object sender, EventArgs e)
    {
        string empID = hfSelectedEmpID.Value;
        string type  = ddlBatchType.SelectedValue;

        if (string.IsNullOrEmpty(empID))
        {
            ShowResult("batchResult", "batchAllowanceModal", "Please select an employee.", false);
            return;
        }
        if (string.IsNullOrEmpty(type) || !Array.Exists(ALLOWED_TYPES, t => t == type))
        {
            ShowResult("batchResult", "batchAllowanceModal", "Please select a valid allowance type.", false);
            return;
        }

        decimal amount;
        if (!decimal.TryParse(txtBatchAmount.Text, out amount) || amount <= 0)
        {
            ShowResult("batchResult", "batchAllowanceModal", "Please enter a valid amount per month.", false);
            return;
        }

        int months;
        if (!int.TryParse(txtBatchMonths.Text, out months) || months < 1 || months > 60)
        {
            ShowResult("batchResult", "batchAllowanceModal", "Number of months must be between 1 and 60.", false);
            return;
        }

        string startMonthStr = ddlBatchStartMonth.SelectedValue;
        int startMonthIndex  = Array.IndexOf(MONTHS, startMonthStr);
        if (startMonthIndex < 0)
        {
            ShowResult("batchResult", "batchAllowanceModal", "Please select a valid start month.", false);
            return;
        }

        int startYear;
        if (!int.TryParse(txtBatchStartYear.Text, out startYear) || startYear < 2020 || startYear > 2099)
        {
            ShowResult("batchResult", "batchAllowanceModal", "Please enter a valid start year (2020-2099).", false);
            return;
        }

        string descTemplate = txtBatchDescription.Text.Trim();
        string recordedBy   = (Session["ScreenName"] ?? Session["username"] ?? "").ToString();
        int    created      = 0;

        for (int i = 0; i < months; i++)
        {
            int monthIndex = (startMonthIndex + i) % 12;
            int yearCarry  = (startMonthIndex + i) / 12;
            string month   = MONTHS[monthIndex];
            int    year    = startYear + yearCarry;

            if (year > 2099) break;

            string desc = string.IsNullOrEmpty(descTemplate)
                ? string.Empty
                : descTemplate.Replace("{n}", (i + 1).ToString());

            ExecuteNonQuery(
                "INSERT INTO hrm_allowance_records " +
                "(empID, allowance_type, amount, description, date_recorded, status, to_add_month, to_add_year, recorded_by) " +
                "VALUES (@eid, @type, @amount, @desc, CURDATE(), 'PENDING', @month, @year, @by)",
                new MySqlParameter("@eid",    empID),
                new MySqlParameter("@type",   type),
                new MySqlParameter("@amount", amount),
                new MySqlParameter("@desc",   desc),
                new MySqlParameter("@month",  month),
                new MySqlParameter("@year",   year),
                new MySqlParameter("@by",     recordedBy));

            created++;
        }

        hfSelectedEmpID.Value          = "";
        ddlBatchType.SelectedIndex     = 0;
        txtBatchAmount.Text            = "0";
        txtBatchMonths.Text            = "1";
        txtBatchDescription.Text       = "";

        BindGrid();
        LoadStats();

        ScriptManager.RegisterStartupScript(this, GetType(), "closeBatch",
            string.Format(
                "document.getElementById('batchAllowanceModal').style.display='none';" +
                "alert('{0} allowance record(s) created successfully.');",
                created),
            true);
    }

    // ===============================================================
    //  Batch Toolbar Actions
    // ===============================================================

    protected void btnBatchDelete_Click(object sender, EventArgs e)
    {
        string[] ids = GetBatchIDs();
        if (ids.Length == 0) return;

        foreach (string id in ids)
        {
            int cid;
            if (int.TryParse(id.Trim(), out cid))
                ExecuteNonQuery(
                    "DELETE FROM hrm_allowance_records WHERE id=@id AND status != 'SETTLED'",
                    new MySqlParameter("@id", cid));
        }

        hdnBatchIDs.Value = "";
        BindGrid();
        LoadStats();
    }

    protected void btnBatchCancel_Click(object sender, EventArgs e)
    {
        string[] ids = GetBatchIDs();
        if (ids.Length == 0) return;

        foreach (string id in ids)
        {
            int cid;
            if (int.TryParse(id.Trim(), out cid))
                ExecuteNonQuery(
                    "UPDATE hrm_allowance_records SET status='CANCELLED' WHERE id=@id AND status='PENDING'",
                    new MySqlParameter("@id", cid));
        }

        hdnBatchIDs.Value = "";
        BindGrid();
        LoadStats();
    }

    private string[] GetBatchIDs()
    {
        string raw = hdnBatchIDs.Value ?? "";
        if (string.IsNullOrEmpty(raw)) return new string[0];
        return raw.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    // ===============================================================
    //  Filter & Pager Events
    // ===============================================================

    protected void ddlFilter_Changed(object sender, EventArgs e) { BindGrid(); }
    protected void btnSearch_Click(object sender, EventArgs e)   { BindGrid(); }

    protected void ddlPageSize_Changed(object sender, EventArgs e)
    {
        int size;
        if (int.TryParse(ddlPageSize.SelectedValue, out size) && size > 0)
            gvAllowances.SettingsPager.PageSize = size;
        BindGrid();
    }

    protected void btnReset_Click(object sender, EventArgs e)
    {
        txtSearch.Text                    = "";
        ddlFilterMonth.SelectedIndex      = 0;
        ddlFilterStatus.SelectedIndex     = 0;
        txtFilterYear.Text                = "";
        BindGrid();
    }

    // ===============================================================
    //  Template Helpers
    // ===============================================================

    protected string GetStatusBadge(object status)
    {
        if (status == null || status == DBNull.Value)
            return "<span class='hr-badge hr-badge--pending'>PENDING</span>";
        string s   = status.ToString().ToUpper();
        string css = s == "SETTLED"   ? "hr-badge--settled"
                   : s == "CANCELLED" ? "hr-badge--cancelled"
                   :                    "hr-badge--pending";
        return string.Format("<span class='hr-badge {0}'>{1}</span>", css, HttpUtility.HtmlEncode(s));
    }

    protected string GetActionHtml(object idObj, object statusObj)
    {
        string id     = idObj     != null ? idObj.ToString()     : "0";
        string status = statusObj != null ? statusObj.ToString() : "";

        string dotsSvg = "<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='5' r='1'/><circle cx='12' cy='12' r='1'/><circle cx='12' cy='19' r='1'/></svg>";
        string editSvg = "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M12 20h9'/><path d='M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z'/></svg>";
        string delSvg  = "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='3 6 5 6 21 6'/><path d='M19 6l-1 14H6L5 6'/><path d='M9 6V4h6v2'/></svg>";
        string canSvg  = "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='4.93' y1='4.93' x2='19.07' y2='19.07'/></svg>";

        bool isPending = status.ToUpper() == "PENDING";
        bool isSettled = status.ToUpper() == "SETTLED";

        string editItem = !isSettled
            ? "<li class='cd-action-popover__item'><button type='button' class='cd-action-popover__btn' " +
              "onclick='gridEdit(\"gvAllowances\"," + id + ")'>" + editSvg + " Edit</button></li>"
            : "";

        string cancelItem = isPending
            ? "<li class='cd-action-popover__item'><button type='button' class='cd-action-popover__btn cd-action-popover__btn--warning' " +
              "onclick='singleCancel(" + id + ")'>" + canSvg + " Cancel</button></li>" +
              "<li class='cd-action-popover__divider'></li>"
            : "";

        string deleteItem = !isSettled
            ? "<li class='cd-action-popover__item'><button type='button' class='cd-action-popover__btn cd-action-popover__btn--danger' " +
              "onclick='gridDelete(\"gvAllowances\"," + id + ")'>" + delSvg + " Delete</button></li>"
            : "<li class='cd-action-popover__item'><button type='button' class='cd-action-popover__btn' disabled style='opacity:.4;cursor:default;'>" + delSvg + " Settled - locked</button></li>";

        return
            "<div class='cd-action-wrapper'>" +
            "<button type='button' class='cd-action-trigger' onclick='toggleActionPopover(this,event)'>" + dotsSvg + "</button>" +
            "<div class='cd-action-popover'><ul class='cd-action-popover__menu'>" +
            editItem + cancelItem + deleteItem +
            "</ul></div></div>";
    }

    protected string FormatAmount(object val)
    {
        if (val == null || val == DBNull.Value) return "-";
        decimal d;
        if (decimal.TryParse(val.ToString(), out d)) return d.ToString("N0");
        return val.ToString();
    }

    protected string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "-";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt)) return dt.ToString("dd MMM yyyy");
        return val.ToString();
    }

    protected string GetPayrollRef(object titleObj, object paymentDateObj)
    {
        if (titleObj == null || titleObj == DBNull.Value)
            return "<span style='color:#bbb;font-size:10px;font-style:italic;'>-</span>";

        string title = titleObj.ToString();
        string date  = (paymentDateObj != null && paymentDateObj != DBNull.Value)
            ? " <span style='color:#aaa;'>(" + FormatDate(paymentDateObj) + ")</span>"
            : "";

        return string.Format("<span class='pr-payref'>{0}{1}</span>",
            HttpUtility.HtmlEncode(title), date);
    }

    protected string TruncStr(object val, int maxLen)
    {
        if (val == null || val == DBNull.Value) return "";
        string s = val.ToString();
        return s.Length <= maxLen ? HttpUtility.HtmlEncode(s) : HttpUtility.HtmlEncode(s.Substring(0, maxLen)) + "…";
    }

    // ===============================================================
    //  Helpers
    // ===============================================================

    private void ShowResult(string resultId, string modalId, string message, bool success)
    {
        string css = success ? "hr-result hr-result--ok" : "hr-result hr-result--err";
        ScriptManager.RegisterStartupScript(this, GetType(), "res_" + resultId,
            string.Format(
                "(function(){{var r=document.getElementById('{0}');r.className='{1}';r.innerHTML='{2}';" +
                "document.getElementById('{3}').style.display='flex';}})()",
                resultId, css,
                HttpUtility.JavaScriptStringEncode(message),
                modalId),
            true);
    }

    private string GetNewValue(System.Collections.Specialized.IOrderedDictionary dict, string key)
    {
        if (dict == null || !dict.Contains(key)) return "";
        var v = dict[key];
        return v == null || v == DBNull.Value ? "" : v.ToString().Trim();
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int i;
        return int.TryParse(val.ToString(), out i) ? i : 0;
    }

    private decimal SafeDecimal(object val)
    {
        if (val == null || val == DBNull.Value) return 0m;
        decimal d;
        return decimal.TryParse(val.ToString(), out d) ? d : 0m;
    }

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (var p in parms) cmd.Parameters.Add(p);
                using (var da = new MySqlDataAdapter(cmd)) da.Fill(dt);
            }
        }
        return dt;
    }

    private int ExecuteNonQuery(string sql, params MySqlParameter[] parms)
    {
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (var p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
