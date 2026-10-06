using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Leave balances: annual leave days allocated per employee and year (hrm_annual_leave) and the
/// leave recorded against them (hrm_leave_taken). Filters are GET parameters; every change is a
/// ?action= POST that returns JSON. The balance model itself is unchanged (pending HR's decision
/// on linking granted applications to leave taken).
/// </summary>
public partial class COOPERP_NewScreens_HRLeaveManagement : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsYear
    {
        get
        {
            int y;
            return int.TryParse(Request.QueryString["year"], out y) && y > 2000 && y < 2100 ? y : DateTime.Now.Year;
        }
    }
    private string QsDept { get { int d; return int.TryParse(Request.QueryString["dept"], out d) && d > 0 ? d.ToString() : ""; } }
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }

    protected string SearchValue = "";
    protected int CurrentYear = DateTime.Now.Year;

    // Current contract per employee, for the department column and filter.
    private const string DeptJoin =
        " LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)" +
        " LEFT JOIN hrm_departments d ON d.ID = c.departmentID ";

    protected void Page_Load(object sender, EventArgs e)
    {
        string action = (Request.QueryString["action"] ?? "").Trim().ToLowerInvariant();
        if (action != "")
        {
            if (!HrAccess.RequireHr(true)) return;
            HandleAction(action);
            return;
        }

        if (!HrAccess.RequireHr(false)) return;

        string export = (Request.QueryString["export"] ?? "").Trim().ToLowerInvariant();
        if (export == "xlsx" || export == "csv") { SendExport(export); return; }

        LoadPage();
    }

    // ══════════════════════════════════════════════════════════════════
    //  PAGE
    // ══════════════════════════════════════════════════════════════════

    private void LoadPage()
    {
        int year = QsYear;
        SearchValue = QsSearch;
        litYear.Text = year.ToString();
        litYear2.Text = year.ToString();

        try
        {
            StringBuilder years = new StringBuilder();
            for (int y = DateTime.Now.Year + 1; y >= DateTime.Now.Year - 5; y--)
                years.AppendFormat("<option value=\"{0}\"{1}>{0}</option>", y, y == year ? " selected=\"selected\"" : "");
            litYearOptions.Text = years.ToString();

            DataTable depts = Query("SELECT ID, dept_name FROM hrm_departments WHERE dept_name IS NOT NULL AND dept_name <> '' ORDER BY dept_name");
            StringBuilder dsb = new StringBuilder("<option value=\"\">All departments</option>");
            foreach (DataRow r in depts.Rows)
                dsb.AppendFormat("<option value=\"{0}\"{1}>{2}</option>", r["ID"], r["ID"].ToString() == QsDept ? " selected=\"selected\"" : "", Enc(r["dept_name"]));
            litDeptOptions.Text = dsb.ToString();

            LoadStats(year);
            BindBalances(year);
            BindTaken(year);
            LoadPickers();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HRLeaveManagement: " + ex);
            litError.Text = "<div class='hr-notice hr-notice--bad'>Leave balances could not be loaded. Refresh the page, and contact MIS if it keeps happening.</div>";
        }

        litExport.Text = "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" +
            HttpUtility.HtmlAttributeEncode(FilterUrl("export=xlsx")) + "\">" + IconDownload + "Excel</a>";
    }

    private void LoadStats(int year)
    {
        DataTable dt = Query(@"
            SELECT COUNT(DISTINCT al.empID) AS total_staff,
                   COALESCE(SUM(lt_sum.total_taken),0) AS total_days_taken,
                   (SELECT COUNT(*) FROM hrm_leave_taken lt2
                     JOIN hrm_annual_leave al2 ON al2.ID = lt2.leaveID
                    WHERE al2.leave_year = @yr AND lt2.startDate <= CURDATE() AND lt2.endDate >= CURDATE()) AS on_leave,
                   SUM(CASE WHEN al.default_days <= COALESCE(lt_sum.total_taken,0) THEN 1 ELSE 0 END) AS exhausted
            FROM hrm_annual_leave al
            LEFT JOIN (SELECT leaveID, SUM(no_days) AS total_taken FROM hrm_leave_taken GROUP BY leaveID) lt_sum ON lt_sum.leaveID = al.ID
            WHERE al.leave_year = @yr",
            new MySqlParameter("@yr", year.ToString()));
        if (dt.Rows.Count == 0) return;
        DataRow r = dt.Rows[0];
        litTotalStaff.Text = N(r["total_staff"]);
        litOnLeave.Text = N(r["on_leave"]);
        litTotalDaysTaken.Text = N(r["total_days_taken"]);
        litExhausted.Text = N(r["exhausted"]);
    }

    private DataTable BalancesTable(int year)
    {
        StringBuilder sql = new StringBuilder(@"
            SELECT al.ID AS allocID, al.leave_year, al.default_days,
                   e.empID, e.EMP_CODE, e.emp_name, d.dept_name,
                   COALESCE(lt_sum.total_taken, 0) AS taken_days,
                   (al.default_days - COALESCE(lt_sum.total_taken, 0)) AS remaining
            FROM hrm_annual_leave al
            JOIN hrm_employee e ON e.empID = al.empID
            LEFT JOIN (SELECT leaveID, SUM(no_days) AS total_taken FROM hrm_leave_taken GROUP BY leaveID) lt_sum ON lt_sum.leaveID = al.ID"
            + DeptJoin + " WHERE al.leave_year = @yr ");
        List<MySqlParameter> p = new List<MySqlParameter>();
        p.Add(new MySqlParameter("@yr", year.ToString()));
        if (QsSearch != "")
        {
            sql.Append(" AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q) ");
            p.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }
        if (QsDept != "")
        {
            sql.Append(" AND c.departmentID = @dept ");
            p.Add(new MySqlParameter("@dept", QsDept));
        }
        sql.Append(" ORDER BY e.emp_name");
        return Query(sql.ToString(), p.ToArray());
    }

    private void BindBalances(int year)
    {
        DataTable dt = BalancesTable(year);
        litBalanceCount.Text = dt.Rows.Count == 1 ? "1 employee" : dt.Rows.Count.ToString("N0") + " employees";
        if (dt.Rows.Count == 0)
        {
            litBalances.Text = "<tr><td colspan='7' class='hr-empty'>No leave has been allocated for " + year + " with these filters.</td></tr>";
            return;
        }
        StringBuilder sb = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            int alloc = I(r["default_days"]), taken = I(r["taken_days"]), rem = alloc - taken;
            string id = r["allocID"].ToString();
            string nameJs = HttpUtility.JavaScriptStringEncode(Str(r["emp_name"]));
            sb.Append("<tr>")
              .Append("<td>").Append(Enc(r["emp_name"])).Append("<span class='hr-sub'>").Append(Enc(r["EMP_CODE"])).Append("</span></td>")
              .Append("<td>").Append(Enc(r["dept_name"])).Append("</td>")
              .Append("<td class='hr-num'>").Append(alloc).Append("</td>")
              .Append("<td class='hr-num'>").Append(taken).Append("</td>")
              .Append("<td class='hr-num'><strong>").Append(rem).Append("</strong></td>")
              .Append("<td>").Append(BalanceBadge(rem)).Append("</td>")
              .Append("<td class='hr-right' style='white-space:nowrap'>")
              .Append("<button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick=\"openEditDays(").Append(id).Append(",'").Append(nameJs).Append("',").Append(alloc).Append(")\">Edit days</button> ")
              .Append("<button type='button' class='hr-btn hr-btn--danger hr-btn--sm' onclick=\"deleteAlloc(").Append(id).Append(",'").Append(nameJs).Append("')\">Delete</button>")
              .Append("</td></tr>");
        }
        litBalances.Text = sb.ToString();
    }

    private void BindTaken(int year)
    {
        DataTable dt = Query(@"
            SELECT lt.ID AS recID, lt.startDate, lt.endDate, lt.no_days, e.EMP_CODE, e.emp_name, d.dept_name
            FROM hrm_leave_taken lt
            JOIN hrm_annual_leave al ON al.ID = lt.leaveID
            JOIN hrm_employee e ON e.empID = al.empID" + DeptJoin + @"
            WHERE al.leave_year = @yr
            ORDER BY lt.startDate DESC",
            new MySqlParameter("@yr", year.ToString()));
        if (dt.Rows.Count == 0)
        {
            litTaken.Text = "<tr><td colspan='6' class='hr-empty'>No leave has been recorded for " + year + ".</td></tr>";
            return;
        }
        StringBuilder sb = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            sb.Append("<tr>")
              .Append("<td>").Append(Enc(r["emp_name"])).Append("<span class='hr-sub'>").Append(Enc(r["EMP_CODE"])).Append("</span></td>")
              .Append("<td>").Append(Enc(r["dept_name"])).Append("</td>")
              .Append("<td style='white-space:nowrap'>").Append(D(r["startDate"])).Append("</td>")
              .Append("<td style='white-space:nowrap'>").Append(D(r["endDate"])).Append("</td>")
              .Append("<td class='hr-num'>").Append(I(r["no_days"])).Append("</td>")
              .Append("<td class='hr-right'><button type='button' class='hr-btn hr-btn--danger hr-btn--sm' onclick=\"deleteRecord(")
              .Append(r["recID"]).Append(",'").Append(HttpUtility.JavaScriptStringEncode(Str(r["emp_name"]))).Append("')\">Delete</button></td></tr>");
        }
        litTaken.Text = sb.ToString();
    }

    /// <summary>Employee options for both pickers and this year's balances for the record dialog.</summary>
    private void LoadPickers()
    {
        DataTable emps = Query("SELECT empID, emp_name, EMP_CODE FROM hrm_employee ORDER BY emp_name");
        StringBuilder sb = new StringBuilder("<option value=\"\">Select employee</option>");
        foreach (DataRow r in emps.Rows)
        {
            string code = Str(r["EMP_CODE"]);
            sb.AppendFormat("<option value=\"{0}\">{1}</option>", r["empID"],
                Enc(Str(r["emp_name"]) + (code == "" || code == "-" ? "" : " (" + code + ")")));
        }
        litEmpOptions.Text = sb.ToString();

        DataTable bal = Query(@"
            SELECT al.empID, al.default_days, COALESCE(SUM(lt.no_days),0) AS taken
            FROM hrm_annual_leave al
            LEFT JOIN hrm_leave_taken lt ON lt.leaveID = al.ID
            WHERE al.leave_year = @yr
            GROUP BY al.ID, al.empID, al.default_days",
            new MySqlParameter("@yr", DateTime.Now.Year.ToString()));
        Dictionary<string, object> map = new Dictionary<string, object>();
        foreach (DataRow r in bal.Rows)
            map[r["empID"].ToString()] = new int[] { I(r["default_days"]), I(r["taken"]) };
        litBalanceJson.Text = "<script>window.LV_BAL=" + new JavaScriptSerializer().Serialize(map) + ";</script>";
    }

    // ══════════════════════════════════════════════════════════════════
    //  ACTIONS (JSON)
    // ══════════════════════════════════════════════════════════════════

    private void HandleAction(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        Response.Cache.SetNoStore();
        try
        {
            if (Request.HttpMethod != "POST") { Json(false, "Send the form again."); }
            else switch (action)
            {
                case "allocate":      DoAllocate(); break;
                case "record":        DoRecord(); break;
                case "update_days":   DoUpdateDays(); break;
                case "delete_alloc":  DoDeleteAlloc(); break;
                case "delete_record": DoDeleteRecord(); break;
                default:              Json(false, "Unknown action."); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HRLeaveManagement action " + action + ": " + ex);
            Response.Clear();
            Json(false, "The change could not be saved. Try again, and contact MIS if it keeps happening.");
        }
        Response.End();
    }

    private void DoAllocate()
    {
        int empID = FormInt("emp_id"), year = FormInt("year"), days;
        string daysStr = (Request.Form["days"] ?? "").Trim();
        if (empID <= 0 || year <= 0 || daysStr == "") { Json(false, "Choose an employee and enter the year and days."); return; }
        if (!int.TryParse(daysStr, out days) || year < 2000 || year > 2100) { Json(false, "Enter a valid year and number of days."); return; }

        DataTable existing = Query("SELECT ID FROM hrm_annual_leave WHERE empID = @eid AND leave_year = @yr",
            new MySqlParameter("@eid", empID), new MySqlParameter("@yr", year.ToString()));
        if (existing.Rows.Count > 0) { Json(false, "This employee already has an allocation for " + year + ". Use Edit days in the list."); return; }

        Exec("INSERT INTO hrm_annual_leave (empID, leave_year, default_days) VALUES (@eid, @yr, @days)",
            new MySqlParameter("@eid", empID), new MySqlParameter("@yr", year.ToString()), new MySqlParameter("@days", days));
        Json(true, "Leave allocated.");
    }

    private void DoRecord()
    {
        int empID = FormInt("emp_id"), noDays;
        int year = DateTime.Now.Year;
        DataTable al = Query(@"
            SELECT al.ID, al.default_days, COALESCE(SUM(lt.no_days),0) AS taken
            FROM hrm_annual_leave al
            LEFT JOIN hrm_leave_taken lt ON lt.leaveID = al.ID
            WHERE al.empID = @eid AND al.leave_year = @yr
            GROUP BY al.ID, al.default_days",
            new MySqlParameter("@eid", empID), new MySqlParameter("@yr", year.ToString()));
        if (empID <= 0 || al.Rows.Count == 0) { Json(false, "This employee has no leave allocated for " + year + ". Allocate leave first."); return; }

        DateTime start, end;
        if (!DateTime.TryParse(Request.Form["start"], out start) || !DateTime.TryParse(Request.Form["end"], out end))
        { Json(false, "Enter valid first and last days."); return; }
        if (!int.TryParse(Request.Form["days"], out noDays) || noDays <= 0) { Json(false, "Enter a valid number of days."); return; }
        if (end < start) { Json(false, "The last day cannot be before the first day."); return; }

        int allocated = I(al.Rows[0]["default_days"]), taken = I(al.Rows[0]["taken"]);
        if (taken + noDays > allocated)
        { Json(false, string.Format("Cannot record {0} days. Only {1} days remain.", noDays, allocated - taken)); return; }

        Exec("INSERT INTO hrm_leave_taken (leaveID, startDate, endDate, no_days) VALUES (@lid, @start, @end, @days)",
            new MySqlParameter("@lid", al.Rows[0]["ID"]), new MySqlParameter("@start", start),
            new MySqlParameter("@end", end), new MySqlParameter("@days", noDays));
        Json(true, "Leave recorded.");
    }

    private void DoUpdateDays()
    {
        int id = FormInt("id"), days;
        if (id <= 0 || !int.TryParse(Request.Form["days"], out days) || days < 0 || days > 365)
        { Json(false, "Enter a number of days from 0 to 365."); return; }
        Exec("UPDATE hrm_annual_leave SET default_days = @days WHERE ID = @id",
            new MySqlParameter("@days", days), new MySqlParameter("@id", id));
        Json(true, "Allocated days updated.");
    }

    private void DoDeleteAlloc()
    {
        int id = FormInt("id");
        if (id <= 0) { Json(false, "Choose an allocation."); return; }
        // Leave taken against the allocation goes with it.
        Exec("DELETE FROM hrm_leave_taken WHERE leaveID = @id", new MySqlParameter("@id", id));
        Exec("DELETE FROM hrm_annual_leave WHERE ID = @id", new MySqlParameter("@id", id));
        Json(true, "Allocation deleted.");
    }

    private void DoDeleteRecord()
    {
        int id = FormInt("id");
        if (id <= 0) { Json(false, "Choose a leave record."); return; }
        Exec("DELETE FROM hrm_leave_taken WHERE ID = @id", new MySqlParameter("@id", id));
        Json(true, "Leave record deleted.");
    }

    private void Json(bool ok, string message)
    {
        Response.Write(new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
            { "ok", ok }, { "success", ok }, { "message", message }, { "error", ok ? "" : message } }));
    }

    // ══════════════════════════════════════════════════════════════════
    //  EXPORT
    // ══════════════════════════════════════════════════════════════════

    private void SendExport(string fmt)
    {
        int year = QsYear;
        HrExport.Report r = new HrExport.Report("Leave balances " + year, "leave-balances");
        r.PreparedBy = HrAccess.Username();
        r.AddScope("Year", year.ToString());
        if (QsDept != "")
        {
            DataTable d = Query("SELECT dept_name FROM hrm_departments WHERE ID = @id", new MySqlParameter("@id", QsDept));
            r.AddScope("Department", d.Rows.Count > 0 ? Str(d.Rows[0][0]) : "");
        }
        r.AddScope("Search", QsSearch);

        HrExport.Sheet sh = r.NewSheet("Balances");
        sh.Add("Staff No").Add("Name").Add("Department").Add("Year")
          .Add("Allocated days", HrExport.Kind.Number, true).Add("Days taken", HrExport.Kind.Number, true)
          .Add("Days remaining", HrExport.Kind.Number, true).Add("Status");
        foreach (DataRow row in BalancesTable(year).Rows)
        {
            int alloc = I(row["default_days"]), taken = I(row["taken_days"]);
            sh.Row(Str(row["EMP_CODE"]), Str(row["emp_name"]), Str(row["dept_name"]), Str(row["leave_year"]),
                alloc, taken, alloc - taken, BalanceLabel(alloc - taken));
        }

        if (fmt == "csv") HrExport.SendCsv(Response, r, 0);
        else HrExport.SendXlsx(Response, r);
        Response.End();
    }

    // ══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════

    private static string BalanceLabel(int remaining)
    {
        if (remaining <= 0) return "Used up";
        if (remaining <= 5) return "Low";
        return "Available";
    }

    private static string BalanceBadge(int remaining)
    {
        string kind = remaining <= 0 ? "bad" : remaining <= 5 ? "warn" : "ok";
        return "<span class='hr-badge hr-badge--" + kind + "'>" + BalanceLabel(remaining) + "</span>";
    }

    private string FilterUrl(string extra)
    {
        List<string> p = new List<string>();
        p.Add("year=" + QsYear);
        if (QsDept != "") p.Add("dept=" + QsDept);
        if (QsSearch != "") p.Add("q=" + HttpUtility.UrlEncode(QsSearch));
        if (!string.IsNullOrEmpty(extra)) p.Add(extra);
        return "HRLeaveManagement.aspx?" + string.Join("&", p.ToArray());
    }

    private const string IconDownload =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4\"/><polyline points=\"7 10 12 15 17 10\"/><line x1=\"12\" y1=\"15\" x2=\"12\" y2=\"3\"/></svg>";

    private int FormInt(string key) { int v; return int.TryParse((Request.Form[key] ?? "").Trim(), out v) ? v : 0; }
    private static int I(object v) { int n; return v != null && v != DBNull.Value && int.TryParse(Convert.ToDecimal(v).ToString("0"), out n) ? n : 0; }
    private static string N(object v) { decimal n; return v != null && v != DBNull.Value && decimal.TryParse(v.ToString(), out n) ? n.ToString("N0") : "0"; }
    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string Enc(object v) { return HttpUtility.HtmlEncode(HrExport.Clean(Str(v))); }
    private static string D(object v)
    {
        DateTime d;
        if (v == null || v == DBNull.Value) return "";
        if (v is DateTime) d = (DateTime)v; else if (!DateTime.TryParse(v.ToString(), out d)) return "";
        return d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }

    private DataTable Query(string sql, params MySqlParameter[] parms)
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

    private int Exec(string sql, params MySqlParameter[] parms)
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
